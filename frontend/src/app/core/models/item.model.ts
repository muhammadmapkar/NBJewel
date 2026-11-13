// Role: Describes one catalog item exactly as the .NET API returns it.

export interface Item {
  id: number;
  code: string;
  name: string;
  categoryId: number;
  designId: number;
  // Read-only: the API fills it from the design master.
  designCode: string;
  metalType: string;
  purity: string;
  grossWt: number;
  netWt: number;
  stoneWt: number;
  makingType: string;
  makingValue: number;
  huid: string | null;
  status: string;
  imageUrl: string | null;
  storefront?: StorefrontMetadata | null;
  stoneValue?: number;
  vatPercent?: number;
}

// Body sent when creating or fully updating an item (the API has no id field here).
// There is no status: new items start as Draft and status moves through its own call.
export interface ItemWriteRequest {
  code: string;
  name: string;
  categoryId: number;
  designId: number;
  metalType: string;
  purity: string;
  grossWt: number;
  netWt: number;
  stoneWt: number;
  makingType: string;
  makingValue: number;
  huid: string | null;
  imageUrl: string | null;
  storefront?: StorefrontMetadata | null;
  stoneValue?: number;
  vatPercent?: number;
}

// Filters accepted by GET /api/items. Every field is optional.
export interface ItemSearchParams {
  search?: string;
  category?: number;
  metal?: string;
  purity?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

// One page of results, as returned by GET /api/items.
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// Assumed customer-facing demo copy; publishing is a separate confirmed server action.
export interface StorefrontMetadata {
  description: string; collection?: string; note?: string; occasions?: string[]; stones?: string[];
  size?: string; badge?: 'new' | 'bestseller' | null; popularity?: number; addedOn?: string | null; isSample?: boolean;
}
