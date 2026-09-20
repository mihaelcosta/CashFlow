import type { Page, Transaction } from '../api/types'

export const accountId = '0199f4a0-0000-7000-8000-000000000001'

export function transaction(overrides: Partial<Transaction> = {}): Transaction {
  return {
    id: crypto.randomUUID(),
    accountId,
    sequence: 1,
    type: 'Credit',
    amount: 100,
    balanceAfter: 100,
    description: null,
    occurredAt: '2026-09-20T12:00:00+00:00',
    ...overrides,
  }
}

export function page(items: Transaction[], overrides: Partial<Page<Transaction>> = {}): Page<Transaction> {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    ...overrides,
  }
}

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}
