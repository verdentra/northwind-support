
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Customer"/> aggregates.
/// </summary>
public interface ICustomerRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);

    /// <summary>Loads a customer so triage can check their service tier.</summary>
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
}
