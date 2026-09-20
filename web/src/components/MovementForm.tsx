import { useState, type FormEvent } from 'react'
import { ApiError } from '../api/client'
import type { MovementRequest, Transaction } from '../api/types'
import type { MovementKind } from '../hooks/useAccount'
import { formatCurrency } from '../lib/format'

interface MovementFormProps {
  onSubmit: (kind: MovementKind, movement: MovementRequest) => Promise<Transaction>
}

type Feedback = { tone: 'success' | 'error'; message: string }

export function MovementForm({ onSubmit }: MovementFormProps) {
  const [kind, setKind] = useState<MovementKind>('deposit')
  const [amount, setAmount] = useState('')
  const [description, setDescription] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [feedback, setFeedback] = useState<Feedback | null>(null)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const value = Number(amount)
    if (!Number.isFinite(value) || value <= 0) {
      setFeedback({ tone: 'error', message: 'Informe um valor maior que zero.' })
      return
    }

    setSubmitting(true)
    setFeedback(null)

    try {
      const transaction = await onSubmit(kind, {
        amount: value,
        description: description.trim() || undefined,
      })

      setFeedback({
        tone: 'success',
        message: `${kind === 'deposit' ? 'Entrada' : 'Saída'} de ${formatCurrency(transaction.amount)} registrada. Saldo: ${formatCurrency(transaction.balanceAfter)}.`,
      })
      setAmount('')
      setDescription('')
    } catch (error) {
      setFeedback({ tone: 'error', message: describe(error) })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="card movement" onSubmit={handleSubmit} noValidate>
      <h2 className="card__label">Nova movimentação</h2>

      <fieldset className="movement__kind" disabled={submitting}>
        <legend className="sr-only">Tipo</legend>
        <label className={kind === 'deposit' ? 'chip chip--active' : 'chip'}>
          <input
            type="radio"
            name="kind"
            value="deposit"
            checked={kind === 'deposit'}
            onChange={() => setKind('deposit')}
          />
          Entrada
        </label>
        <label className={kind === 'withdraw' ? 'chip chip--active chip--debit' : 'chip'}>
          <input
            type="radio"
            name="kind"
            value="withdraw"
            checked={kind === 'withdraw'}
            onChange={() => setKind('withdraw')}
          />
          Saída
        </label>
      </fieldset>

      <label className="field">
        <span>Valor (R$)</span>
        <input
          type="number"
          inputMode="decimal"
          min="0.01"
          step="0.01"
          placeholder="0,00"
          value={amount}
          onChange={(event) => setAmount(event.target.value)}
          onWheel={(event) => event.currentTarget.blur()}
          disabled={submitting}
          required
        />
      </label>

      <label className="field">
        <span>Descrição (opcional)</span>
        <input
          type="text"
          maxLength={200}
          placeholder="Ex.: Recebimento NF 1042"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          disabled={submitting}
        />
      </label>

      <button type="submit" className={kind === 'withdraw' ? 'button button--debit' : 'button'} disabled={submitting}>
        {submitting ? 'Registrando…' : kind === 'deposit' ? 'Registrar entrada' : 'Registrar saída'}
      </button>

      {feedback && (
        <p className={`feedback feedback--${feedback.tone}`} role={feedback.tone === 'error' ? 'alert' : 'status'}>
          {feedback.message}
        </p>
      )}
    </form>
  )
}

function describe(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isInsufficientFunds) {
      return 'Saldo insuficiente para esta saída.'
    }

    if (error.validationMessages.length > 0) {
      return error.validationMessages.join(' ')
    }

    return error.message
  }

  return 'Não foi possível registrar a movimentação. Tente novamente.'
}
