# Decisões técnicas

Este arquivo registra as decisões que moldaram a solução, o que foi considerado no lugar de cada uma e o que elas custam. O critério geral foi resolver o problema com o mínimo de peças, mas conseguir justificar cada peça.

## 1. Controllers em vez de Minimal APIs

O desafio pede uma API em C#. Escolhi Controllers com `[ApiController]` porque é o que predomina em bases corporativas de médio e grande porte: filtros, convenções, model binding declarativo e a validação por DataAnnotations com resposta `ValidationProblemDetails` já prontos.

Minimal APIs teriam menos código, mas Controllers ainda são o padrão na maioria dos projetos corporativos e deixam a estrutura mais familiar para quem for ler.

Os controllers são finos. Traduzem HTTP para caso de uso e nada mais.

## 2. Quatro projetos, dependências apontando para dentro

`Api -> Infrastructure -> Application -> Domain`. O `Domain` declara os ports (`IAccountRepository`, `IUnitOfWork`) e não referencia nenhum pacote. `Infrastructure` implementa os ports. `Api` é o único projeto que conhece todos, porque é onde o grafo de objetos é montado.

Considerei um projeto único com pastas. Funciona, mas a regra de dependência vira convenção em vez de restrição de compilação. Considerei também a Clean Architecture completa, com SharedKernel, Persistence separado de Infrastructure e afins; seriam camadas sem conteúdo para preenchê-las.

A direção das dependências é verificada por `LayerDependencyTests` (NetArchTest), então não degrada sem alguém perceber.

Dentro do `Domain`, os arquivos estão organizados por agregado (`Accounts/`), não por tipo técnico (`Entities/`, `Exceptions/`). A pasta é a fronteira do agregado: quando a regra de conta muda, tudo que muda está num lugar só. Subpastas por tipo aparecem quando um tipo se multiplica, como aconteceu com `Accounts/Exceptions/`.

## 3. Casos de uso com um contrato genérico e retry como decorator

Cada operação tem uma classe que implementa `IUseCase<TRequest, TResponse>`. Os controllers dependem de `IUseCase<DepositCommand, TransactionResult>` e assim por diante.

Isso substitui o MediatR. O formato é o mesmo (um handler por request), mas sem a dependência e sem a indireção do `Send`. Se um dia precisar de pipeline em escala, a migração é mecânica porque os handlers já têm o formato.

O contrato único permite compor comportamento por decorator. O `ConcurrencyRetryDecorator<TRequest, TResponse>` envolve só os casos de uso de escrita e reexecuta em `ConcurrencyConflictException`, até 3 vezes. O registro é manual no container (`AddCommandWithConcurrencyRetry`), sem Scrutor. Polly resolveria, mas é uma dependência para um loop de três linhas.

O retry fora do caso de uso mantém o `WithdrawUseCase` testável sem ele e sem misturar infraestrutura com orquestração. O limite de 3 tentativas é uma constante. Não vi motivo para torná-lo configurável agora.

## 4. Saldo nunca negativo: concorrência otimista

A regra de domínio em `Account.Withdraw` cobre o caso sequencial. O problema é duas requisições lerem o mesmo saldo e as duas passarem na regra.

`Account.Version` é o concurrency token do EF Core, incrementado pelo domínio a cada movimento. O `UPDATE` só afeta a linha se a versão lida ainda for a atual. Quem perde recebe `DbUpdateConcurrencyException`; a infraestrutura converte em `ConcurrencyConflictException`, o decorator retenta, e se persistir a resposta é 409.

Alternativas que descartei:

- Saldo derivado (`SUM(transactions)` na hora do saque). Auditável, mas exige isolamento serializável ou lock na escrita e o custo cresce com o histórico. É o caminho para event sourcing, não o ponto de partida.
- Lock pessimista (`SELECT ... FOR UPDATE`). Acopla ao banco, e SQLite não tem lock de linha.
- `rowversion`. SQLite não tem. Um inteiro incrementado pelo domínio funciona em qualquer provider.

O `CashFlowDbContext` implementa `IUnitOfWork` diretamente (o DbContext já é um Unit of Work, criar um `EfUnitOfWork` por cima seria abstração sobre abstração) e, ao capturar o conflito, chama `ChangeTracker.Clear()` antes de lançar. Sem isso o retry releria a entidade desatualizada do cache do contexto e o conflito se repetiria. O teste `AfterAConflict_TheUnitOfWorkIsClean_...` cobre esse cenário.

## 5. Agregado sem coleção de transações, extrato ordenado por sequência

Um saque precisa do saldo, não do histórico. Se `Account` tivesse `Transactions` como coleção navegável, cada movimento carregaria tudo ou dependeria de lazy loading, e isso cresce sem limite.

`Account` mantém `Balance` e `Version`. `Deposit` e `Withdraw` devolvem a `Transaction` gerada, e o repositório a persiste. A leitura do histórico passa por `ITransactionHistoryReader` (contrato em `Application`, implementação em `Infrastructure`), com projeção direta para DTO e `AsNoTracking`. É uma separação de escrita e leitura sem CQRS formal.

Cada `Transaction` guarda `BalanceAfter` (o saldo resultante) e `Sequence`, que é o `Account.Version` no momento do movimento. O extrato ordena por `Sequence`, não por `OccurredAt`. Timestamps colidem em rajadas e, nos testes, com relógio congelado; GUID v7 como desempate também não é garantido no mesmo milissegundo. O `Sequence` é monotônico, sem lacunas, único por conta (índice `(AccountId, Sequence)`). Cheguei nesse modelo depois que os testes de integração com relógio fixo expuseram o problema da ordenação.

## 6. SQLite com EF Core

O desafio não impõe banco, e quem clonar o repositório precisa rodar sem instalar nada. SQLite em arquivo, migrations aplicadas no boot e uma conta padrão semeada.

EF InMemory não serve: não é relacional, não tem transação real nem concurrency token confiável. PostgreSQL via Docker seria mais próximo de produção, mas cria uma dependência de ambiente para avaliar a solução.

Dois ajustes específicos do provider ficaram isolados em `Infrastructure`: `DateTimeOffset` convertido para `long` (`DateTimeOffsetToBinaryConverter`), porque SQLite não ordena nem compara `DateTimeOffset`; e `decimal(18,2)` armazenado como `TEXT`, com precisão preservada pelo EF. Trocar por PostgreSQL é mudar provider e connection string.

## 7. Injeção de dependência

Cada camada expõe um método de composição (`AddApplication()`, `AddInfrastructure()`) e o `Program.cs` só os encadeia.

Lifetimes:

| Serviço | Lifetime | Motivo |
|---|---|---|
| `CashFlowDbContext` / `IUnitOfWork` | Scoped | Uma unidade de trabalho por request; a interface resolve para a mesma instância do contexto |
| `IAccountRepository`, `ITransactionHistoryReader` | Scoped | Dependem do contexto |
| Casos de uso e decorators | Scoped | Dependem do repositório, sem estado próprio |
| `TimeProvider` | Singleton | Sem estado, thread-safe |

Nenhum singleton recebe dependência scoped (captive dependency). Nenhum service locator fora do composition root.

O tempo é lido via `TimeProvider`, a abstração nativa do .NET 8 em diante, em vez de `DateTimeOffset.UtcNow`. Não criei um `IClock` próprio porque o runtime já fornece um. Nos testes de integração o `WebApplicationFactory` substitui o singleton por `FakeTimeProvider`, e o teste de filtro por período avança o relógio 31 dias entre depósitos sem `Task.Delay`.

A connection string vem por Options pattern (`DatabaseOptions`) com `ValidateDataAnnotations().ValidateOnStart()`. Configuração ausente derruba a aplicação no boot, com mensagem clara, em vez de falhar no primeiro request.

Interfaces só existem onde há uma segunda implementação real: `IUseCase<,>` por causa do decorator, `IAccountRepository` porque é mockado, `ITransactionHistoryReader` porque isola a leitura do EF. Os casos de uso concretos não têm interface própria. Quando um segundo implementador aparecer, extrair a interface é uma refatoração mecânica.

## 8. Erros como ProblemDetails

Um `IExceptionHandler` mapeia `NotFoundException` para 404, `ConcurrencyConflictException` para 409 e `DomainException` para 422. A resposta segue a RFC 9457 com um campo extra `errorType`. Erros de validação de contrato saem como 400 pelo `[ApiController]`.

Controllers não conhecem exceções. O que não está mapeado cai no handler padrão como 500 genérico, sem vazar detalhes. Um middleware próprio ou `try/catch` nos controllers dariam o mesmo resultado com mais código; `IExceptionHandler` é a extensão que o framework prevê para isso.

## 9. Múltiplas contas com uma conta padrão

O enunciado fala em "uma conta empresarial". Mesmo assim modelei `accounts/{id}` com criação, e semeei uma conta padrão para o avaliador não precisar criar nada.

Endpoints sem `{id}` sobre uma conta única seriam menores, mas a primeira evolução realista (filiais, centros de custo) exigiria quebrar o contrato. O custo agora é quase zero, e nos testes de integração cada teste cria a própria conta e fica isolado sem limpar o banco.

## 10. Bibliotecas e padrões que deixei de fora

AutoMapper: são três DTOs com mapeamento trivial. Um método estático `From(...)` em cada um resolve e fica mais fácil de depurar do que configuração de profile.

FluentValidation: a validação de formato (obrigatório, tamanho, faixa) está em DataAnnotations nos contratos de entrada. A validação de negócio (saldo, valor positivo) está na entidade, que é onde a regra pertence. Um validator externo separaria a regra do objeto que ela protege.

Domain events e outbox: não há nenhum consumidor para eventos hoje. Se aparecer um requisito como notificação por saldo baixo, o ponto de extensão é o `Record` do `Account`.

Repositório genérico `IRepository<T>`: expõe `IQueryable` e acaba deixando a query espalhada por quem consome. Preferi um repositório por agregado, com os métodos que o negócio realmente usa.

## 11. Estratégia de testes

Cada camada tem um estilo de teste diferente, de acordo com o que ela faz.

- Domain: estado, sem dublês. Cenários de negócio (folha de pagamento, extrato com saldo corrente, precisão de centavos).
- Application: interação, com NSubstitute. `Add` antes de `Commit`, nada persistido quando a regra falha, `CancellationToken` propagado. `FakeLogger` verifica os warnings do retry.
- Infrastructure: SQLite in-memory real com as migrations reais. Mockar `DbContext` não prova nada sobre concorrência.
- Architecture: direção das dependências.
- Integration: contrato HTTP completo, banco isolado por classe de teste, relógio controlado. Os testes paralelos afirmam só invariantes (saldo maior ou igual a zero, saldo igual a créditos menos débitos, nenhum 5xx), nunca contagens exatas que dependeriam de timing.

Os dublês são escolhidos por intenção: builders para estado, mocks para interação. `dotnet test --filter "Category!=Integration"` roda só os rápidos.

## 12. Interface web sem bibliotecas além do React

O diferencial pede uma interface em React. É uma tela: saldo, formulário de movimentação e histórico.

Vite + React 19 + TypeScript. `fetch` nativo com um wrapper que converte ProblemDetails em `ApiError` tipado. Estado local em um hook (`useAccount`), CSS puro. O Vite faz proxy de `/api`, então não há CORS em desenvolvimento. O fetch do histórico é cancelado quando o filtro muda antes da resposta chegar. Testes com Vitest e Testing Library mockam `fetch` na borda; o resto roda de verdade.

React Query resolveria cache e revalidação, mas para uma tela com dois requests o hook manual é menor e legível. Axios não traria nada que o `fetch` não faça aqui. UI kit e Redux seriam desproporcionais para três componentes sem estado compartilhado.

O front reflete o contrato de erros da API: 422 vira "Saldo insuficiente", 400 mostra as mensagens de validação, falha de rede oferece "Tentar novamente". O id da conta padrão tem fallback em código e override por `VITE_DEFAULT_ACCOUNT_ID`, o que evita versionar um `.env`. Se a tela crescer (multiconta, filtro por período), React Query seria a primeira dependência a entrar.
