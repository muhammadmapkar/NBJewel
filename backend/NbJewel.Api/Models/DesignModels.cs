// Role: Defines the design master record that the item form dropdown reads.
// The design master is assumed to be the "master chit" from the product manager.

namespace NbJewel.Api.Models;

public sealed class Design
{
    public int DesignId { get; set; }
    public string DesignCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
