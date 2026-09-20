import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import App from './App'
import { accountId, jsonResponse, page, transaction } from './test/fixtures'

describe('App', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads balance and history for the default account', async () => {
    // Arrange
    fetchMock.mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/balance')) return Promise.resolve(jsonResponse({ accountId, balance: 1150 }))
      return Promise.resolve(
        jsonResponse(
          page([
            transaction({ sequence: 2, type: 'Debit', amount: 350, balanceAfter: 1150, description: 'Fornecedor' }),
            transaction({ sequence: 1, type: 'Credit', amount: 1500, balanceAfter: 1500, description: 'NF 1042' }),
          ]),
        ),
      )
    })

    // Act
    render(<App />)

    // Assert
    expect(await screen.findByTestId('balance')).toHaveTextContent('R$ 1.150,00')

    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1)
    expect(rows).toHaveLength(2)
    expect(rows[0]).toHaveTextContent('Fornecedor')
    expect(rows[0]).toHaveTextContent('− R$ 350,00')
    expect(rows[1]).toHaveTextContent('+ R$ 1.500,00')

    const urls = fetchMock.mock.calls.map(([input]) => String(input))
    expect(urls).toContain(`/api/accounts/${accountId}/balance`)
    expect(urls).toContain(`/api/accounts/${accountId}/transactions?page=1&pageSize=10`)
  })

  it('refreshes balance and history after a movement is registered', async () => {
    // Arrange
    let balance = 100
    const items = [transaction({ sequence: 1, amount: 100, balanceAfter: 100 })]

    fetchMock.mockImplementation((input, init) => {
      const url = String(input)
      if (url.endsWith('/withdrawals') && init?.method === 'POST') {
        balance -= 40
        items.unshift(transaction({ sequence: 2, type: 'Debit', amount: 40, balanceAfter: balance, description: 'Aluguel' }))
        return Promise.resolve(jsonResponse(items[0], 201))
      }
      if (url.endsWith('/balance')) return Promise.resolve(jsonResponse({ accountId, balance }))
      return Promise.resolve(jsonResponse(page([...items])))
    })

    const user = userEvent.setup()
    render(<App />)
    expect(await screen.findByTestId('balance')).toHaveTextContent('R$ 100,00')

    // Act
    await user.click(screen.getByLabelText('Saída'))
    await user.type(screen.getByLabelText('Valor (R$)'), '40')
    await user.type(screen.getByLabelText('Descrição (opcional)'), 'Aluguel')
    await user.click(screen.getByRole('button', { name: 'Registrar saída' }))

    // Assert
    expect(await screen.findByRole('status')).toHaveTextContent('Saída de R$ 40,00 registrada')
    expect(await screen.findByText('R$ 60,00', { selector: '[data-testid="balance"]' })).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(3)
    expect(screen.getByText('Aluguel')).toBeInTheDocument()
  })

  it('filters the history by type and resets to the first page', async () => {
    // Arrange
    fetchMock.mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/balance')) return Promise.resolve(jsonResponse({ accountId, balance: 0 }))
      if (url.includes('type=Debit')) {
        return Promise.resolve(jsonResponse(page([transaction({ type: 'Debit', amount: 5, description: 'Só saída' })])))
      }
      return Promise.resolve(jsonResponse(page([transaction({ description: 'Entrada qualquer' })])))
    })

    const user = userEvent.setup()
    render(<App />)
    expect(await screen.findByText('Entrada qualquer')).toBeInTheDocument()

    // Act
    await user.selectOptions(screen.getByLabelText('Tipo'), 'Debit')

    // Assert
    expect(await screen.findByText('Só saída')).toBeInTheDocument()
    expect(screen.queryByText('Entrada qualquer')).not.toBeInTheDocument()

    const lastHistoryUrl = fetchMock.mock.calls.map(([input]) => String(input)).filter((u) => u.includes('/transactions')).at(-1)
    expect(lastHistoryUrl).toBe(`/api/accounts/${accountId}/transactions?type=Debit&page=1&pageSize=10`)
  })

  it('shows an error with a retry action when the API is unreachable', async () => {
    // Arrange
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch')).mockRejectedValueOnce(new TypeError('Failed to fetch'))
    fetchMock.mockImplementation((input) =>
      Promise.resolve(
        String(input).endsWith('/balance') ? jsonResponse({ accountId, balance: 7 }) : jsonResponse(page([])),
      ),
    )
    const user = userEvent.setup()
    render(<App />)

    // Act
    const alert = await screen.findByRole('alert')
    await user.click(within(alert).getByRole('button', { name: 'Tentar novamente' }))

    // Assert
    expect(alert).toHaveTextContent('Failed to fetch')
    expect(await screen.findByTestId('balance')).toHaveTextContent('R$ 7,00')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
