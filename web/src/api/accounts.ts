import { request } from './client'
import type { Balance, MovementRequest, Page, Transaction, TransactionsQuery } from './types'

const base = (accountId: string) => `/api/accounts/${accountId}`

export function getBalance(accountId: string): Promise<Balance> {
  return request<Balance>(`${base(accountId)}/balance`)
}

export function deposit(accountId: string, movement: MovementRequest): Promise<Transaction> {
  return request<Transaction>(`${base(accountId)}/deposits`, {
    method: 'POST',
    body: JSON.stringify(movement),
  })
}

export function withdraw(accountId: string, movement: MovementRequest): Promise<Transaction> {
  return request<Transaction>(`${base(accountId)}/withdrawals`, {
    method: 'POST',
    body: JSON.stringify(movement),
  })
}

export function getTransactions(accountId: string, query: TransactionsQuery = {}): Promise<Page<Transaction>> {
  const params = new URLSearchParams()
  if (query.type) params.set('type', query.type)
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))

  const suffix = params.size > 0 ? `?${params}` : ''
  return request<Page<Transaction>>(`${base(accountId)}/transactions${suffix}`)
}
