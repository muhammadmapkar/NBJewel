// Role: Types for the customer catalog (public/catalog/catalog.json, made by
// scripts/build-tryon-catalog.py). A product is a set or a single kind of piece; each piece has
// its own transparent image and optional fit tuning for the try-on.

import { Fit } from '../tryon/overlay-math';

export type PieceKind = 'necklace' | 'earrings' | 'tikka';
export type Category = 'set' | PieceKind;

export interface PieceImage {
  image: string; // path relative to the app root
  aspect: number; // height / width
  fit?: Fit;
}

export interface Product {
  id: string;
  name: string;
  note: string;
  category: Category;
  kinds: PieceKind[];
  card: string;
  parts: {
    necklace?: PieceImage;
    earringLeft?: PieceImage;
    earringRight?: PieceImage;
    tikka?: PieceImage;
  };
}

export const KIND_LABELS: Record<PieceKind, string> = { necklace: 'Necklace', earrings: 'Earrings', tikka: 'Maang tikka' };

export const FILTERS: readonly { value: 'all' | Category; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'set', label: 'Sets' },
  { value: 'necklace', label: 'Necklaces' },
  { value: 'earrings', label: 'Earrings' }
];

// A product shows under a filter when it is that category, or it contains that kind of piece
// (a set with earrings also shows under Earrings, since its earrings can be tried alone).
export function matchesFilter(product: Product, filter: 'all' | Category): boolean {
  if (filter === 'all') return true;
  if (filter === 'set') return product.category === 'set';
  return product.kinds.includes(filter);
}

// Reads the "parts" query value ("necklace,earrings") and keeps only kinds the product has.
// Empty or unknown means all of them.
export function parseParts(value: string | null, product: Product): PieceKind[] {
  const wanted = (value ?? '').split(',').map(x => x.trim()).filter((x): x is PieceKind => (product.kinds as string[]).includes(x));
  return wanted.length ? wanted : [...product.kinds];
}
