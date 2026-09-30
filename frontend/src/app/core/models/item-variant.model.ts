// Role: Describes one variant (size and color) of an item.
// Assumed, no backend endpoint yet: the .NET API does not serve variants.

export interface ItemVariant {
  id: number;
  itemId: number;
  size: string;
  color: string;
  variantCode: string;
  // Amount added to the item price for this variant (assumed to be in AED).
  priceDelta: number;
  stock: number;
}

export const VARIANT_CURRENCY = 'AED';
