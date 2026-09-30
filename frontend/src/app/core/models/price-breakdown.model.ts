// Role: Describes the calculated price of one item (the API calls it ItemPrice).

export interface PriceBreakdown {
  itemId: number;
  currency: string;
  netWeight: number;
  ratePerGram: number;
  metalValue: number;
  makingType: string;
  makingValue: number;
  makingCharge: number;
  totalPrice: number;
}
