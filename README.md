# CashFlow

[![CI](https://github.com/mihaelcosta/CashFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/mihaelcosta/CashFlow/actions/workflows/ci.yml)

API para controle de movimentações de uma conta empresarial: registro de entradas e saídas, consulta de saldo e histórico. O saldo nunca fica negativo, mesmo com requisições concorrentes.

Solução para o desafio técnico de Engenheiro de Software .NET da act digital.

## Stack

| | |
|---|---|
| Runtime | .NET 10 (LTS), ASP.NET Core com Controllers |
| Persistência | EF Core 10 + SQLite (arquivo local, sem configuração) |
| Documentação da API | OpenAPI nativo + Scalar UI |
| Testes | xUnit, NSubstitute, FakeTimeProvider, FakeLogger, WebApplicationFactory, NetArchTest |
| Interface (diferencial) | React 19 + TypeScript + Vite; Vitest + Testing Library |

Não usei MediatR, AutoMapper nem FluentValidation no back-end, e no front-end não há biblioteca de estado, UI kit ou cliente HTTP. Os motivos estão em [docs/decisions.md](docs/decisions.md).

## Como executar

Pré-requisito: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet run --project src/CashFlow.Api
```

- API em `http://localhost:5013`
- Documentação interativa em `http://localhost:5013/scalar`
- O banco `cashflow.db` é criado automaticamente na primeira execução (migrations aplicadas no boot)
- Uma conta padrão já vem criada: `0199f4a0-0000-7000-8000-000000000001` ("Conta Empresarial")

### Interface web

Com a API rodando, em outro terminal (pré-requisito: Node 20 ou superior):

```bash
cd web && npm install && npm run dev
```

Abre em `http://localhost:5173`. O Vite faz proxy de `/api` para `http://localhost:5013`, então não precisa de CORS em desenvolvimento. A tela trabalha sobre a conta padrão: registra entradas e saídas, mostra o saldo e o histórico com filtro por tipo e paginação. Saldo insuficiente (422) e erros de validação (400) aparecem como mensagem no formulário.

Variáveis opcionais: `VITE_API_URL` (destino do proxy) e `VITE_DEFAULT_ACCOUNT_ID` (conta exibida).

### Docker

Sobe API e interface sem precisar de .NET nem Node instalados:

```bash
docker compose up --build
```

- Interface em `http://localhost:5173`
- API em `http://localhost:5013` (Scalar em `/scalar`, health check em `/health`)
- O banco SQLite fica no volume `cashflow-data`, então os dados sobrevivem a `docker compose down`

A imagem da API é multi-stage (SDK para publicar, `aspnet` para rodar), executa como usuário não-root e recebe a connection string por variável de ambiente (`Database__ConnectionString`). A imagem do front serve o build estático com nginx e faz proxy de `/api` para o container da API; o destino é configurável por `API_UPSTREAM`.

## Endpoints

Há também um `GET /health` para health checks de infraestrutura.

| Método | Rota | Descrição | Sucesso |
|---|---|---|---|
| `POST` | `/api/accounts` | Cria uma conta | `201` |
| `GET` | `/api/accounts/{id}/balance` | Saldo disponível | `200` |
| `POST` | `/api/accounts/{id}/deposits` | Registra uma entrada | `201` |
| `POST` | `/api/accounts/{id}/withdrawals` | Registra uma saída | `201` |
| `GET` | `/api/accounts/{id}/transactions` | Histórico paginado | `200` |

### Exemplos

```bash
ACCOUNT=0199f4a0-0000-7000-8000-000000000001
BASE=http://localhost:5013/api/accounts
```

Entrada:

```bash
curl -s -X POST $BASE/$ACCOUNT/deposits \
  -H "Content-Type: application/json" \
  -d '{"amount": 1500.00, "description": "Recebimento NF 1042"}'
```

```json
{
  "id": "01a0bf43-5d96-7451-8ec6-b77218879fbc",
  "accountId": "0199f4a0-0000-7000-8000-000000000001",
  "sequence": 1,
  "type": "Credit",
  "amount": 1500.00,
  "balanceAfter": 1500.00,
  "description": "Recebimento NF 1042",
  "occurredAt": "2026-09-20T14:40:58.2617162+00:00"
}
```

Saída:

```bash
curl -s -X POST $BASE/$ACCOUNT/withdrawals \
  -H "Content-Type: application/json" \
  -d '{"amount": 350.00, "description": "Fornecedor"}'
```

Saldo:

```bash
curl -s $BASE/$ACCOUNT/balance
```

```json
{ "accountId": "0199f4a0-0000-7000-8000-000000000001", "balance": 1150.00 }
```

Histórico. Filtros opcionais: `type=Credit|Debit`, `from` e `to` em ISO 8601, `page`, `pageSize` (máximo 100).

```bash
curl -s "$BASE/$ACCOUNT/transactions?type=Debit&page=1&pageSize=20"
```

```json
{
  "items": [ { "...": "..." } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Criar outra conta:

```bash
curl -s -X POST $BASE -H "Content-Type: application/json" -d '{"name": "Filial Centro"}'
```

### Erros

Todos os erros seguem a [RFC 9457 (Problem Details)](https://www.rfc-editor.org/rfc/rfc9457), com um campo extra `errorType`.

| Status | Quando |
|---|---|
| `400 Bad Request` | Payload inválido: valor menor ou igual a zero, descrição acima de 200 caracteres, paginação fora do intervalo, JSON malformado |
| `404 Not Found` | Conta inexistente |
| `409 Conflict` | Conflito de concorrência que persistiu após 3 tentativas (raro, ver abaixo) |
| `422 Unprocessable Entity` | Regra de negócio violada. Na prática, saldo insuficiente |

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Business rule violated",
  "status": 422,
  "detail": "Insufficient funds. Balance: 1150.00, requested: 5000.",
  "errorType": "InsufficientFundsException",
  "traceId": "00-151a836745f9859fd7526cf419fd33b4-509f125a2860ecc3-00"
}
```

## Estrutura da solução

```
src/
├── CashFlow.Domain          Entidades, regras de negócio, exceções de domínio e ports (IAccountRepository, IUnitOfWork)
├── CashFlow.Application     Casos de uso (um por operação), DTOs de saída, decorator de retry, contrato do read model
├── CashFlow.Infrastructure  EF Core + SQLite: DbContext, mapeamentos, repositório, leitor de histórico, migrations, seed
└── CashFlow.Api             Controllers, contratos de entrada, tratamento de erros, composição do DI (Program.cs)
tests/
└── CashFlow.Tests           Unitários (Domain, Application), infra (SQLite real), arquitetura e integração HTTP
web/
└── src/                     React: api/ (cliente tipado), hooks/useAccount, components/, testes ao lado do código
docs/
└── decisions.md             Decisões técnicas e alternativas descartadas
```

As dependências apontam sempre para dentro: `Api -> Infrastructure -> Application -> Domain`. O projeto `Domain` não referencia nenhum pacote. Essa regra é verificada por um teste (`LayerDependencyTests`).

Cada camada tem seu próprio ponto de composição, `AddApplication()` e `AddInfrastructure()`, e o `Program.cs` só encadeia os dois.

### Fluxo de uma saída

```
TransactionsController.Withdraw
  -> IUseCase<WithdrawCommand, TransactionResult>       (resolvido como ConcurrencyRetryDecorator)
      -> WithdrawUseCase
          -> IAccountRepository.GetByIdAsync             carrega Account (Version = N)
          -> Account.Withdraw(amount)                    lança InsufficientFundsException se saldo < valor
          -> IAccountRepository.Add(transaction)
          -> IUnitOfWork.CommitAsync                     UPDATE Accounts ... WHERE Id = @id AND Version = N
```

## Como o saldo nunca fica negativo

Há duas proteções.

A primeira é a regra de domínio: `Account.Withdraw` recusa qualquer saque maior que o saldo em memória. Isso resolve o caso sequencial.

A segunda é concorrência otimista. Duas requisições simultâneas podem carregar a conta com o mesmo saldo e as duas passarem na regra. Para cobrir isso, `Account.Version` é um concurrency token: o `UPDATE` só afeta a linha se a versão no banco ainda for a que foi lida. Quem perde a corrida recebe `DbUpdateConcurrencyException`, que a infraestrutura converte em `ConcurrencyConflictException` depois de descartar o change tracker. O `ConcurrencyRetryDecorator` então executa o caso de uso de novo, do zero: recarrega a conta com o saldo atualizado e reaplica a regra. Se falhar 3 vezes, a resposta é 409.

Três testes cobrem isso, cada um de um ângulo:

- `OptimisticConcurrencyTests`: determinístico. Dois `DbContext` leem o mesmo snapshot, o segundo commit falha e a unidade de trabalho fica limpa.
- `ConcurrencyRetryDecoratorTests`: o decorator só retenta em conflito, loga um warning por tentativa e desiste no limite.
- `ConcurrentMovementsTests`: 25 saques paralelos via HTTP. Saldo final maior ou igual a zero, saldo igual a créditos menos débitos, nenhum 5xx.

## Testes

```bash
dotnet test
```

O mesmo roda no GitHub Actions a cada push e pull request ([ci.yml](.github/workflows/ci.yml)): testes da API, lint/testes/build do front e build das duas imagens Docker com smoke test da API em container.

Só os rápidos (sem subir a API):

```bash
dotnet test --filter "Category!=Integration"
```

| Camada | O que se testa | Como |
|---|---|---|
| Domain | Estado e regras: folha de pagamento, extrato com saldo corrente, centavos, versão | Sem dublês |
| Application | Interação: `Add` antes de `Commit`, nada persistido quando a regra falha, token de cancelamento propagado | NSubstitute, FakeTimeProvider, FakeLogger |
| Infrastructure | Conflito de concorrência real, filtros e paginação do histórico | SQLite in-memory com conexão compartilhada e migrations reais |
| Architecture | Direção das dependências entre camadas | NetArchTest |
| Integration | Contrato HTTP (status, ProblemDetails, enum como string), um mês de operação de uma empresa pequena, stress paralelo | WebApplicationFactory com banco isolado por classe e relógio controlado |
| Web | Cliente HTTP (mapeamento de ProblemDetails), formulário (validação, 422), tela completa com `fetch` mockado | Vitest + Testing Library (`cd web && npm test`) |

## Decisões técnicas

Estão em [docs/decisions.md](docs/decisions.md), cada uma com contexto, alternativas consideradas e consequências. As que costumam gerar mais perguntas:

- Controllers em vez de Minimal APIs
- Casos de uso com um contrato genérico (`IUseCase<TRequest, TResponse>`) em vez de MediatR
- Concorrência otimista em vez de saldo derivado ou lock pessimista
- Agregado `Account` sem a coleção de transações; histórico como read model separado
- `Transaction.Sequence` como ordem do extrato, em vez de timestamp
- Interfaces só onde existe uma segunda implementação
- `TimeProvider` injetado em vez de `DateTime.UtcNow`

## Não implementado e evoluções possíveis

Ficaram fora do escopo. Como cada um entraria:

- Autenticação e autorização. A API é aberta. O próximo passo seria JWT com a conta vinculada ao tenant do token.
- Idempotência. Um header `Idempotency-Key` nas movimentações evitaria duplicidade quando o cliente faz retry de rede.
- Transferência entre contas. Um caso de uso que toca dois agregados na mesma unidade de trabalho. O modelo atual suporta sem mudança estrutural.
- Multi-moeda. Hoje o valor é `decimal(18,2)` sem moeda. Um value object `Money` entraria em `Domain`.
- Banco de produção. Trocar SQLite por PostgreSQL mexe só em `Infrastructure` (provider e connection string). A estratégia de concorrência não depende do banco.
- Event sourcing. Se o histórico passar a ser a fonte da verdade, `Account` seria reconstruído a partir das transações. Hoje o saldo é materializado por performance e simplicidade.
- Observabilidade. OpenTelemetry para traces e métricas. O `traceId` já sai nos erros.
- Interface web. A tela atual cobre o fluxo do desafio. Multiconta, filtro por período e autenticação viriam junto com as evoluções da API.
