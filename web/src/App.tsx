import { BalanceCard } from './components/BalanceCard'
import { MovementForm } from './components/MovementForm'
import { TransactionsTable } from './components/TransactionsTable'
import { defaultAccountId } from './config'
import { useAccount } from './hooks/useAccount'

export default function App() {
  const account = useAccount(defaultAccountId)

  return (
    <main className="layout">
      <header className="layout__header">
        <h1>CashFlow</h1>
        <p className="muted">
          Conta <code>{defaultAccountId}</code>
        </p>
      </header>

      {account.error && (
        <p className="feedback feedback--error" role="alert">
          {account.error}{' '}
          <button type="button" className="link" onClick={account.refresh}>
            Tentar novamente
          </button>
        </p>
      )}

      <div className="layout__grid">
        <BalanceCard balance={account.balance} loading={account.loading} />
        <MovementForm onSubmit={account.move} />
      </div>

      <TransactionsTable
        history={account.history}
        filter={account.filter}
        loading={account.loading}
        onFilterChange={account.changeFilter}
      />
    </main>
  )
}
