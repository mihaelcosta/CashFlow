import type { ProblemDetails } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(ApiError.describe(status, problem))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get isInsufficientFunds(): boolean {
    return this.problem.errorType === 'InsufficientFundsException'
  }

  get validationMessages(): string[] {
    return Object.values(this.problem.errors ?? {}).flat()
  }

  private static describe(status: number, problem: ProblemDetails): string {
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(' ')
    }

    return problem.detail ?? problem.title ?? `Request failed with status ${status}`
  }
}

export async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  return (await response.json()) as T
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return { status: response.status, title: response.statusText }
  }
}
