// Role: Validation rules for adding stock, shared by the sample and SQL
// repositories. Stock entry ADDS to on-hand, so the quantity must be above 0.

namespace NbJewel.Api.Models;

public static class StockRules
{
    public static void EnsureQuantity(int quantity)
    {
        if (quantity <= 0) throw new DomainValidationException("Quantity must be greater than 0.");
    }

    // Adds two stock numbers and refuses to wrap around past the int limit.
    public static int AddOnHand(int onHand, int quantity)
    {
        var total = (long)onHand + quantity;
        if (total > int.MaxValue) throw new DomainValidationException("Quantity is too large; the on-hand total would overflow.");
        return (int)total;
    }
}
