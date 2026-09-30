// Role: Defines the item, reference-data, stock, pricing, and write request
// contracts used by the API and its swappable repository.

namespace NbJewel.Api.Models;

public sealed class Item
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string DesignCode { get; set; } = string.Empty;
    public string MetalType { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal GrossWt { get; set; }
    public decimal NetWt { get; set; }
    public decimal StoneWt { get; set; }
    public string MakingType { get; set; } = string.Empty;
    public decimal MakingValue { get; set; }
    public string? Huid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public sealed class Category { public int Id { get; set; } public string Name { get; set; } = ""; public int? ParentId { get; set; } }
public sealed class Store { public int Id { get; set; } public string Name { get; set; } = ""; public string City { get; set; } = ""; }
public sealed class StockRecord { public int ItemId { get; set; } public int StoreId { get; set; } public int OnHand { get; set; } public int Reserved { get; set; } }
public sealed class MetalRate { public int Id { get; set; } public string MetalType { get; set; } = ""; public string Purity { get; set; } = ""; public decimal RatePerGram { get; set; } public string Currency { get; set; } = "AED"; public DateOnly EffectiveDate { get; set; } }
public sealed record ItemPrice(int ItemId, string Currency, decimal NetWeight, decimal RatePerGram, decimal MetalValue, string MakingType, decimal MakingValue, decimal MakingCharge, decimal TotalPrice);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record ItemCreateRequest(string Code, string Name, int CategoryId, string DesignCode, string MetalType, string Purity, decimal GrossWt, decimal NetWt, decimal StoneWt, string MakingType, decimal MakingValue, string? Huid, string Status, string? ImageUrl);
public sealed record ItemUpdateRequest(string Code, string Name, int CategoryId, string DesignCode, string MetalType, string Purity, decimal GrossWt, decimal NetWt, decimal StoneWt, string MakingType, decimal MakingValue, string? Huid, string Status, string? ImageUrl);
public sealed record StatusUpdateRequest(string Status);
