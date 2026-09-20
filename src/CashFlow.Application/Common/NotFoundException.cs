namespace CashFlow.Application.Common;

public sealed class NotFoundException(string resource, Guid id)
    : Exception($"{resource} with id {id} was not found.")
{
    public string Resource { get; } = resource;
    public Guid Id { get; } = id;
}
