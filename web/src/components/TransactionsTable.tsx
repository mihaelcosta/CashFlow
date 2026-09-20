import type { Page, Transaction, TransactionType } from '../api/types'
import type { HistoryFilter } from '../hooks/useAccount'
import { formatCurrency, formatDateTime } from '../lib/format'

interface TransactionsTableProps {
  history: Page<Transaction> | null
  filter: HistoryFilter
  loading: boolean
  onFilterChange: (changes: Partial<HistoryFilter>) => void
}

const typeOptions: { value: TransactionType | ''; label: string }[] = [
  { value: '', label: 'Todas' },
  { value: 'Credit', label: 'Entradas' },
  { value: 'Debit', label: 'Saídas' },
]

export function TransactionsTable({ history, filter, loading, onFilterChange }: TransactionsTableProps) {
  const items = history?.items ?? []
  const totalPages = history?.totalPages ?? 0

  return (
    <section className="card history">
      <header className="history__header">
        <h2 className="card__label">Histórico</h2>

        <label className="field field--inline">
          <span>Tipo</span>
          <select
            value={filter.type ?? ''}
            onChange={(event) =>
              onFilterChange({ type: (event.target.value || undefined) as TransactionType | undefined, page: 1 })
            }
          >
            {typeOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      </header>

      {items.length === 0 ? (
        <p className="history__empty">{loading ? 'Carregando…' : 'Nenhuma movimentação encontrada.'}</p>
      ) : (
        <table className="history__table">
          <thead>
            <tr>
              <th scope="col">Data</th>
              <th scope="col">Descrição</th>
              <th scope="col" className="numeric">Valor</th>
              <th scope="col" className="numeric">Saldo após</th>
            </tr>
          </thead>
          <tbody>
            {items.map((transaction) => (
              <tr key={transaction.id} data-type={transaction.type}>
                <td>{formatDateTime(transaction.occurredAt)}</td>
                <td>{transaction.description ?? <span className="muted">—</span>}</td>
                <td className={`numeric amount amount--${transaction.type.toLowerCase()}`}>
                  {transaction.type === 'Credit' ? '+' : '−'} {formatCurrency(transaction.amount)}
                </td>
                <td className="numeric">{formatCurrency(transaction.balanceAfter)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {totalPages > 1 && (
        <nav className="pagination" aria-label="Paginação do histórico">
          <button
            type="button"
            className="button button--ghost"
            disabled={filter.page <= 1 || loading}
            onClick={() => onFilterChange({ page: filter.page - 1 })}
          >
            Anterior
          </button>
          <span>
            Página {filter.page} de {totalPages}
          </span>
          <button
            type="button"
            className="button button--ghost"
            disabled={filter.page >= totalPages || loading}
            onClick={() => onFilterChange({ page: filter.page + 1 })}
          >
            Próxima
          </button>
        </nav>
      )}
    </section>
  )
}
