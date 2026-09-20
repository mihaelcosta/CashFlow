export type TransactionType = 'Credit' | 'Debit'

export interface Balance {
  accountId: string
  balance: number
}

export interface Transaction {
  id: string
  accountId: string
  sequence: number
  type: TransactionType
  amount: number
  balanceAfter: number
  description: string | null
  occurredAt: string
}

export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface MovementRequest {
  amount: number
  description?: string
}

export interface TransactionsQuery {
  type?: TransactionType
  page?: number
  pageSize?: number
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errorType?: string
  errors?: Record<string, string[]>
}
