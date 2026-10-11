// Role: Describes one variant (size and color) of an item.
// The API sends everything except stock. The data service adds stock by
// summing the item's stock records for this variant.

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
