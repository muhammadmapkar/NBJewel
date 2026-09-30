// Role: Describes the stock of one item in one store (the API calls it StockRecord).

export interface StockLevel {
  itemId: number;
  storeId: number;
  onHand: number;
  reserved: number;
  // Assumed, no backend field yet: the API does not send a last updated time.
  lastUpdated?: string;
}
