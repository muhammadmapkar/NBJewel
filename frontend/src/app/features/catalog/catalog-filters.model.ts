// Role: Describes the filter values shared by the catalog list and its filter panel.

export interface CatalogFilters {
  categoryId: number | null;
  metal: string;
  purity: string;
  minWeight: number | null;
  maxWeight: number | null;
  status: string;
  inStockOnly: boolean;
}

export const DEFAULT_CATALOG_FILTERS: CatalogFilters = {
  categoryId: null,
  metal: '',
  purity: '',
  minWeight: null,
  maxWeight: null,
  status: '',
  inStockOnly: false
};

// Status workflow (assumed): an item moves forward one step at a time. The API enforces it too.
export const ITEM_STATUSES = ['Draft', 'With Smith', 'Received', 'Complete'];

// The only status an item may move to from this one, or null at the end of the workflow.
export function nextStatus(current: string): string | null {
  const index = ITEM_STATUSES.indexOf(current);
  return index >= 0 && index < ITEM_STATUSES.length - 1 ? ITEM_STATUSES[index + 1] : null;
}
