import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../api/client'
import { transaction } from '../test/fixtures'
import { MovementForm } from './MovementForm'

describe('MovementForm', () => {
  it('submits a deposit with the parsed amount and trimmed description', async () => {
    // Arrange
    const onSubmit = vi.fn().mockResolvedValue(transaction({ amount: 1500.5, balanceAfter: 1500.5 }))
    const user = userEvent.setup()
    render(<MovementForm onSubmit={onSubmit} />)

    // Act
    await user.type(screen.getByLabelText('Valor (R$)'), '1500.50')
    await user.type(screen.getByLabelText('Descrição (opcional)'), '  Recebimento NF 1042  ')
    await user.click(screen.getByRole('button', { name: 'Registrar entrada' }))

    // Assert
    expect(onSubmit).toHaveBeenCalledWith('deposit', { amount: 1500.5, description: 'Recebimento NF 1042' })
    expect(await screen.findByRole('status')).toHaveTextContent('Entrada de R$ 1.500,50 registrada')
    expect(screen.getByLabelText('Valor (R$)')).toHaveValue(null)
  })

  it('switches to withdrawal and omits an empty description', async () => {
    // Arrange
    const onSubmit = vi.fn().mockResolvedValue(transaction({ type: 'Debit', amount: 30, balanceAfter: 70 }))
    const user = userEvent.setup()
    render(<MovementForm onSubmit={onSubmit} />)

    // Act
    await user.click(screen.getByLabelText('Saída'))
    await user.type(screen.getByLabelText('Valor (R$)'), '30')
    await user.click(screen.getByRole('button', { name: 'Registrar saída' }))

    // Assert
    expect(onSubmit).toHaveBeenCalledWith('withdraw', { amount: 30, description: undefined })
    expect(await screen.findByRole('status')).toHaveTextContent('Saída de R$ 30,00 registrada. Saldo: R$ 70,00.')
  })

  it('rejects a non-positive amount before calling the API', async () => {
    // Arrange
    const onSubmit = vi.fn()
    const user = userEvent.setup()
    render(<MovementForm onSubmit={onSubmit} />)

    // Act
    await user.type(screen.getByLabelText('Valor (R$)'), '0')
    await user.click(screen.getByRole('button', { name: 'Registrar entrada' }))

    // Assert
    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('Informe um valor maior que zero.')
  })

  it('shows a friendly message when the API rejects for insufficient funds', async () => {
    // Arrange
    const onSubmit = vi
      .fn()
      .mockRejectedValue(new ApiError(422, { status: 422, errorType: 'InsufficientFundsException' }))
    const user = userEvent.setup()
    render(<MovementForm onSubmit={onSubmit} />)

    // Act
    await user.click(screen.getByLabelText('Saída'))
    await user.type(screen.getByLabelText('Valor (R$)'), '999')
    await user.click(screen.getByRole('button', { name: 'Registrar saída' }))

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Saldo insuficiente para esta saída.')
    expect(screen.getByLabelText('Valor (R$)')).toHaveValue(999)
  })

  it('surfaces validation messages coming from the API', async () => {
    // Arrange
    const onSubmit = vi
      .fn()
      .mockRejectedValue(new ApiError(400, { status: 400, errors: { Description: ['Too long.'] } }))
    const user = userEvent.setup()
    render(<MovementForm onSubmit={onSubmit} />)

    // Act
    await user.type(screen.getByLabelText('Valor (R$)'), '10')
    await user.click(screen.getByRole('button', { name: 'Registrar entrada' }))

    // Assert
    expect(await screen.findByRole('alert')).toHaveTextContent('Too long.')
  })
})
