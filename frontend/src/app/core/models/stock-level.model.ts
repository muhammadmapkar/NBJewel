// Role: Describes the stock of one item variant in one store (the API calls it
// StockRecord), plus the body sent when adding stock.

export interface StockLevel {
  itemId: number;
  // Null for an item that has no variants (assumed).
  variantId: number | null;
  storeId: number;
  onHand: number;
  reserved: number;
  // Assumed, no backend field yet: the API does not send a last updated time.
  lastUpdated?: string;
}

// Body for adding stock. The quantity is ADDED to on-hand and must be above 0.
export interface StockAddRequest {
  variantId: number | null;
  storeId: number;
  quantity: number;
}
