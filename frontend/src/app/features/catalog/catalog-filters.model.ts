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

// The four statuses, in display order. Assumed: any status can be chosen from any
// other (forward, backward or skipping), and Complete is not final.
export const ITEM_STATUSES = ['Draft', 'With Smith', 'Received', 'Complete'];

// True when the new status comes earlier in the list than the current one.
export function isBackward(current: string, next: string): boolean {
  const from = ITEM_STATUSES.indexOf(current);
  const to = ITEM_STATUSES.indexOf(next);
  return from >= 0 && to >= 0 && to < from;
}
