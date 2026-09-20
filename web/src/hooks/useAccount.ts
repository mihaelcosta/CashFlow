import { useCallback, useEffect, useState } from 'react'
import * as accounts from '../api/accounts'
import type { MovementRequest, Page, Transaction, TransactionType } from '../api/types'

export type MovementKind = 'deposit' | 'withdraw'

export interface HistoryFilter {
  type?: TransactionType
  page: number
  pageSize: number
}

interface AccountState {
  balance: number | null
  history: Page<Transaction> | null
  loading: boolean
  error: string | null
}

const initialState: AccountState = { balance: null, history: null, loading: true, error: null }

export function useAccount(accountId: string) {
  const [state, setState] = useState<AccountState>(initialState)
  const [filter, setFilter] = useState<HistoryFilter>({ page: 1, pageSize: 10 })
  const [reloadToken, setReloadToken] = useState(0)

  useEffect(() => {
    let cancelled = false

    Promise.all([accounts.getBalance(accountId), accounts.getTransactions(accountId, filter)])
      .then(([balance, history]) => {
        if (cancelled) return
        setState({ balance: balance.balance, history, loading: false, error: null })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setState((current) => ({
          ...current,
          loading: false,
          error: error instanceof Error ? error.message : 'Não foi possível carregar a conta.',
        }))
      })

    return () => {
      cancelled = true
    }
  }, [accountId, filter, reloadToken])

  const refresh = useCallback(() => {
    setState((current) => ({ ...current, loading: true, error: null }))
    setReloadToken((token) => token + 1)
  }, [])

  const changeFilter = useCallback((changes: Partial<HistoryFilter>) => {
    setState((current) => ({ ...current, loading: true, error: null }))
    setFilter((current) => ({ ...current, ...changes }))
  }, [])

  const move = useCallback(
    async (kind: MovementKind, movement: MovementRequest): Promise<Transaction> => {
      const action = kind === 'deposit' ? accounts.deposit : accounts.withdraw
      const transaction = await action(accountId, movement)

      if (filter.page === 1) {
        refresh()
      } else {
        changeFilter({ page: 1 })
      }

      return transaction
    },
    [accountId, filter.page, refresh, changeFilter],
  )

  return { ...state, filter, changeFilter, move, refresh }
}
