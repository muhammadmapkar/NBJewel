// Role: Types and pure rules for the customer shop: the catalog file shape
// (public/catalog/catalog.json, made by scripts/build-tryon-catalog.py), price from the gold rate,
// catalog filters and sorting, and the try-on "parts" query. All values in the catalog are SAMPLE
// data for the demo (assumed). Unit tested in catalog.models.spec.ts.

import { Fit } from '../tryon/overlay-math';

export type PieceKind = 'necklace' | 'earrings' | 'tikka';
export type Category = 'set' | PieceKind;
export type Badge = 'new' | 'bestseller' | null;

export interface PieceImage {
  image: string; // path relative to the app root
  aspect: number; // height / width
  fit?: Fit;
  neckline?: number; // necklaces: inner front edge as a share of the image height
}

export interface Product {
  id: string;
  name: string;
  note: string;
  description: string;
  code: string;
  collection: string;
  occasions: string[];
  category: Category;
  kinds: PieceKind[];
  card: string;
  metal: string;
  purity: string; // "22K"
  hallmark: string;
  grossWeight: number; // grams
  netWeight: number; // grams of gold
  makingPercent: number; // of the gold value
  stones: string[];
  stoneValue: number;
  size: string;
  badge: Badge;
  popularity: number; // 1 is the most popular
  addedOn: string; // ISO date
  parts: { necklace?: PieceImage; earringLeft?: PieceImage; earringRight?: PieceImage; tikka?: PieceImage };
}

export interface Catalog {
  currency: string;
  rates: Record<string, number>; // per gram, by purity
  ratesNote: string;
  vatPercent: number;
  branches: string[];
  sample: boolean;
  products: Product[];
}

export const KIND_LABELS: Record<PieceKind, string> = { necklace: 'Necklace', earrings: 'Earrings', tikka: 'Maang tikka' };

// ---------- Price ----------

export interface PriceBreakdown { gold: number; making: number; stones: number; vat: number; total: number; rate: number }

const round2 = (n: number) => Math.round(n * 100) / 100;

// Gold value = net weight x rate; making charge = a percent of the gold value; VAT on the sum.
export function priceOf(product: Product, catalog: Pick<Catalog, 'rates' | 'vatPercent'>): PriceBreakdown {
  const rate = catalog.rates[product.purity] ?? 0;
  const gold = round2(product.netWeight * rate);
  const making = round2(gold * product.makingPercent / 100);
  const stones = round2(product.stoneValue);
  const vat = round2((gold + making + stones) * catalog.vatPercent / 100);
  return { gold, making, stones, vat, total: round2(gold + making + stones + vat), rate };
}

export function formatMoney(amount: number, currency: string): string {
  return `${currency} ${amount.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`;
}

// ---------- Filters and sorting ----------

export interface Range { label: string; min: number; max: number } // min inclusive, max exclusive

export const PRICE_RANGES: readonly Range[] = [
  { label: 'Under 10,000', min: 0, max: 10000 },
  { label: '10,000 to 25,000', min: 10000, max: 25000 },
  { label: '25,000 to 40,000', min: 25000, max: 40000 },
  { label: '40,000 and above', min: 40000, max: Infinity }
];

export const WEIGHT_RANGES: readonly Range[] = [
  { label: 'Under 20 g', min: 0, max: 20 },
  { label: '20 to 60 g', min: 20, max: 60 },
  { label: '60 g and above', min: 60, max: Infinity }
];

export const TYPE_OPTIONS: readonly { value: Category; label: string }[] = [
  { value: 'set', label: 'Sets' },
  { value: 'necklace', label: 'Necklaces' },
  { value: 'earrings', label: 'Earrings' }
];

export type SortKey = 'popular' | 'priceLow' | 'priceHigh' | 'newest';
export const SORT_OPTIONS: readonly { value: SortKey; label: string }[] = [
  { value: 'popular', label: 'Popular' },
  { value: 'priceLow', label: 'Price: low to high' },
  { value: 'priceHigh', label: 'Price: high to low' },
  { value: 'newest', label: 'Newest' }
];

export interface Filters {
  types: Category[];
  purities: string[];
  prices: string[]; // range labels
  weights: string[];
  stones: string[]; // "Plain gold" means no stones
  occasions: string[];
  collections: string[]; // set from the home page collection tiles
}

export const EMPTY_FILTERS: Filters = { types: [], purities: [], prices: [], weights: [], stones: [], occasions: [], collections: [] };
export const PLAIN_GOLD = 'Plain gold';

export function activeFilterCount(f: Filters): number {
  return f.types.length + f.purities.length + f.prices.length + f.weights.length + f.stones.length + f.occasions.length + f.collections.length;
}

// A set counts as each kind it contains, so its earrings show under Earrings too.
export function matchesType(product: Product, type: Category): boolean {
  return type === 'set' ? product.category === 'set' : product.kinds.includes(type);
}

const inRange = (value: number, ranges: readonly Range[], labels: string[]) =>
  ranges.filter(r => labels.includes(r.label)).some(r => value >= r.min && value < r.max);

// Within one group any choice may match (OR); across groups all must match (AND).
export function matchesFilters(product: Product, f: Filters, price: number): boolean {
  if (f.types.length && !f.types.some(t => matchesType(product, t))) return false;
  if (f.purities.length && !f.purities.includes(product.purity)) return false;
  if (f.prices.length && !inRange(price, PRICE_RANGES, f.prices)) return false;
  if (f.weights.length && !inRange(product.grossWeight, WEIGHT_RANGES, f.weights)) return false;
  if (f.stones.length) {
    const stones = product.stones.length ? product.stones : [PLAIN_GOLD];
    if (!f.stones.some(s => stones.includes(s))) return false;
  }
  if (f.occasions.length && !f.occasions.some(o => product.occasions.includes(o))) return false;
  if (f.collections.length && !f.collections.includes(product.collection)) return false;
  return true;
}

export function sortProducts(list: readonly Product[], key: SortKey, priceOfId: (p: Product) => number): Product[] {
  const copy = [...list];
  switch (key) {
    case 'priceLow': return copy.sort((a, b) => priceOfId(a) - priceOfId(b));
    case 'priceHigh': return copy.sort((a, b) => priceOfId(b) - priceOfId(a));
    case 'newest': return copy.sort((a, b) => b.addedOn.localeCompare(a.addedOn));
    default: return copy.sort((a, b) => a.popularity - b.popularity);
  }
}

// Values offered in the filter panel, taken from the catalog itself.
export function filterValues(products: readonly Product[]): { purities: string[]; stones: string[]; occasions: string[] } {
  const uniq = (xs: string[]) => [...new Set(xs)].sort();
  const stones = uniq(products.flatMap(p => p.stones));
  return {
    purities: uniq(products.map(p => p.purity)),
    stones: products.some(p => !p.stones.length) ? [PLAIN_GOLD, ...stones] : stones,
    occasions: uniq(products.flatMap(p => p.occasions))
  };
}

// Same collection or same kind of piece, most popular first, not the product itself.
export function similarProducts(product: Product, all: readonly Product[], count = 4): Product[] {
  return all
    .filter(p => p.id !== product.id && (p.collection === product.collection || p.kinds.some(k => product.kinds.includes(k))))
    .sort((a, b) => a.popularity - b.popularity)
    .slice(0, count);
}

// ---------- Try-on parts ----------

// Reads the "parts" query value ("necklace,earrings") and keeps only kinds the product has.
// Empty or unknown means all of them.
export function parseParts(value: string | null, product: Pick<Product, 'kinds'>): PieceKind[] {
  const wanted = (value ?? '').split(',').map(x => x.trim()).filter((x): x is PieceKind => (product.kinds as string[]).includes(x));
  return wanted.length ? wanted : [...product.kinds];
}
