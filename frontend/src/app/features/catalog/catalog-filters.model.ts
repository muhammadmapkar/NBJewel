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

export const ITEM_STATUSES = ['Active', 'Draft', 'Discontinued'];
