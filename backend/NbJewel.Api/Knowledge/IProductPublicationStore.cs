// Role: Swappable persistence for explicit sample product publication decisions, without SQL.
namespace NbJewel.Api.Knowledge;
public interface IProductPublicationStore
{
    Task<IReadOnlyList<ProductPublication>> ListAsync(CancellationToken ct);
    Task SaveAsync(ProductPublication decision, string? actor, CancellationToken ct);
}
