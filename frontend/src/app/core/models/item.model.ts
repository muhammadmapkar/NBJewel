// Role: Describes one catalog item exactly as the .NET API returns it.

export interface Item {
  id: number;
  code: string;
  name: string;
  categoryId: number;
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
}

// Body sent when creating or fully updating an item (the API has no id field here).
export interface ItemWriteRequest {
  code: string;
  name: string;
  categoryId: number;
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
