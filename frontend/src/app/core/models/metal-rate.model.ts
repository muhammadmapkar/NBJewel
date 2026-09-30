// Role: Describes the latest rate per gram for one metal and purity.

export interface MetalRate {
  id: number;
  metalType: string;
  purity: string;
  ratePerGram: number;
  currency: string;
  // The API sends a date only, formatted as yyyy-MM-dd.
  effectiveDate: string;
}
