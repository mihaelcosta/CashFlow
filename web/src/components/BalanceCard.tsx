import { formatCurrency } from '../lib/format'

interface BalanceCardProps {
  balance: number | null
  loading: boolean
}

export function BalanceCard({ balance, loading }: BalanceCardProps) {
  return (
    <section className="card balance" aria-live="polite">
      <span className="card__label">Saldo disponível</span>
      <strong className="balance__value" data-testid="balance">
        {balance === null ? (loading ? 'Carregando…' : '—') : formatCurrency(balance)}
      </strong>
    </section>
  )
}
