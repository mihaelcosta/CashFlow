import { ApiError, request } from './client'
import { jsonResponse } from '../test/fixtures'

describe('request', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns the parsed body on success', async () => {
    // Arrange
    fetchMock.mockResolvedValue(jsonResponse({ balance: 42 }))

    // Act
    const result = await request<{ balance: number }>('/api/x')

    // Assert
    expect(result).toEqual({ balance: 42 })
  })

  it('sends JSON content type only when there is a body', async () => {
    // Arrange
    fetchMock.mockImplementation(() => Promise.resolve(jsonResponse({})))

    // Act
    await request('/api/get')
    await request('/api/post', { method: 'POST', body: '{}' })

    // Assert
    const [, getInit] = fetchMock.mock.calls[0]
    const [, postInit] = fetchMock.mock.calls[1]
    expect(getInit?.headers).not.toHaveProperty('Content-Type')
    expect(postInit?.headers).toMatchObject({ 'Content-Type': 'application/json' })
  })

  it('maps a 422 insufficient funds problem to a typed ApiError', async () => {
    // Arrange
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'Business rule violated',
          status: 422,
          detail: 'Insufficient funds. Balance: 50, requested: 80.',
          errorType: 'InsufficientFundsException',
        },
        422,
      ),
    )

    // Act
    const failure = request('/api/x').catch((error: unknown) => error)

    // Assert
    const error = (await failure) as ApiError
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(422)
    expect(error.isInsufficientFunds).toBe(true)
    expect(error.message).toBe('Insufficient funds. Balance: 50, requested: 80.')
  })

  it('flattens validation errors from a 400 problem', async () => {
    // Arrange
    fetchMock.mockResolvedValue(
      jsonResponse({ status: 400, errors: { Amount: ['Amount must be greater than zero.'] } }, 400),
    )

    // Act
    const error = (await request('/api/x').catch((e: unknown) => e)) as ApiError

    // Assert
    expect(error.validationMessages).toEqual(['Amount must be greater than zero.'])
    expect(error.isInsufficientFunds).toBe(false)
  })

  it('still produces an ApiError when the error body is not JSON', async () => {
    // Arrange
    fetchMock.mockResolvedValue(new Response('Bad Gateway', { status: 502, statusText: 'Bad Gateway' }))

    // Act
    const error = (await request('/api/x').catch((e: unknown) => e)) as ApiError

    // Assert
    expect(error.status).toBe(502)
    expect(error.message).toBe('Bad Gateway')
  })
})
