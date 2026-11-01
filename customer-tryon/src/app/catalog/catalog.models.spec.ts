// Role: Unit tests for the shop rules: price from the gold rate, filters (OR within a group,
// AND across groups), sorting, similar designs and the try-on "parts" query.

import { describe, expect, it } from 'vitest';
import { EMPTY_FILTERS, Filters, Product, activeFilterCount, filterValues, matchesFilters, parseParts, priceOf, similarProducts, sortProducts } from './catalog.models';

function product(over: Partial<Product>): Product {
  return {
    id: 'x', name: 'X', note: '', description: '', code: 'C', collection: 'Bridal', occasions: ['Bridal'],
    category: 'set', kinds: ['necklace', 'earrings'], card: '', metal: 'Gold', purity: '22K', hallmark: '',
    grossWeight: 50, netWeight: 40, makingPercent: 10, stones: [], stoneValue: 0, size: '', badge: null,
    popularity: 1, addedOn: '2026-01-01', parts: {}, ...over
  };
}

const shop = { rates: { '22K': 400, '18K': 300 }, vatPercent: 5 };
const f = (over: Partial<Filters>): Filters => ({ ...EMPTY_FILTERS, ...over });

describe('priceOf', () => {
  it('gold value plus making charge plus stones, then VAT', () => {
    const p = priceOf(product({ netWeight: 10, makingPercent: 10, stoneValue: 100 }), shop);
    expect(p.gold).toBe(4000);
    expect(p.making).toBe(400);
    expect(p.stones).toBe(100);
    expect(p.vat).toBe(225);
    expect(p.total).toBe(4725);
  });

  it('uses the rate for the product purity', () => {
    expect(priceOf(product({ netWeight: 10, makingPercent: 0, purity: '18K' }), { ...shop, vatPercent: 0 }).total).toBe(3000);
  });
});

describe('matchesFilters', () => {
  const set = product({ id: 'set', stones: ['Kundan', 'Emerald'], occasions: ['Bridal'], grossWeight: 90 });
  const earrings = product({ id: 'ear', category: 'earrings', kinds: ['earrings'], stones: [], occasions: ['Daily'], grossWeight: 14 });

  it('no filters match everything', () => {
    expect(matchesFilters(set, EMPTY_FILTERS, 1)).toBe(true);
    expect(activeFilterCount(EMPTY_FILTERS)).toBe(0);
  });

  it('a set shows under the kinds it contains', () => {
    expect(matchesFilters(set, f({ types: ['earrings'] }), 1)).toBe(true);
    expect(matchesFilters(earrings, f({ types: ['set'] }), 1)).toBe(false);
  });

  it('price and weight ranges', () => {
    expect(matchesFilters(set, f({ prices: ['40,000 and above'] }), 45000)).toBe(true);
    expect(matchesFilters(set, f({ prices: ['Under 10,000'] }), 45000)).toBe(false);
    expect(matchesFilters(earrings, f({ weights: ['Under 20 g'] }), 1)).toBe(true);
    expect(matchesFilters(set, f({ weights: ['Under 20 g', '60 g and above'] }), 1)).toBe(true);
  });

  it('plain gold matches pieces without stones', () => {
    expect(matchesFilters(earrings, f({ stones: ['Plain gold'] }), 1)).toBe(true);
    expect(matchesFilters(set, f({ stones: ['Plain gold'] }), 1)).toBe(false);
    expect(matchesFilters(set, f({ stones: ['Emerald'] }), 1)).toBe(true);
  });

  it('collection from the home page tiles', () => {
    expect(matchesFilters(set, f({ collections: ['Bridal'] }), 1)).toBe(true);
    expect(matchesFilters(set, f({ collections: ['Temple'] }), 1)).toBe(false);
  });

  it('groups combine with AND', () => {
    expect(matchesFilters(set, f({ stones: ['Emerald'], occasions: ['Daily'] }), 1)).toBe(false);
  });

  it('filterValues lists what the catalog has, with Plain gold first', () => {
    expect(filterValues([set, earrings])).toEqual({ purities: ['22K'], stones: ['Plain gold', 'Emerald', 'Kundan'], occasions: ['Bridal', 'Daily'] });
  });
});

describe('sortProducts and similarProducts', () => {
  const a = product({ id: 'a', popularity: 2, addedOn: '2026-05-01', collection: 'Temple', kinds: ['necklace'] });
  const b = product({ id: 'b', popularity: 1, addedOn: '2026-09-01', collection: 'Bridal', kinds: ['earrings'] });
  const c = product({ id: 'c', popularity: 3, addedOn: '2026-07-01', collection: 'Daily', kinds: ['tikka'] });
  const price = (p: Product) => ({ a: 300, b: 100, c: 200 })[p.id as 'a' | 'b' | 'c'];

  it('sorts by popularity, price and date', () => {
    expect(sortProducts([a, b, c], 'popular', price).map(p => p.id)).toEqual(['b', 'a', 'c']);
    expect(sortProducts([a, b, c], 'priceLow', price).map(p => p.id)).toEqual(['b', 'c', 'a']);
    expect(sortProducts([a, b, c], 'priceHigh', price).map(p => p.id)).toEqual(['a', 'c', 'b']);
    expect(sortProducts([a, b, c], 'newest', price).map(p => p.id)).toEqual(['b', 'c', 'a']);
  });

  it('similar designs share a collection or a kind, never the product itself', () => {
    const d = product({ id: 'd', popularity: 4, collection: 'Temple', kinds: ['earrings'] });
    expect(similarProducts(a, [a, b, c, d]).map(p => p.id)).toEqual(['d']);
  });
});

describe('parseParts', () => {
  it('keeps only kinds the product has; empty means all', () => {
    expect(parseParts('necklace,ring', { kinds: ['necklace', 'earrings'] })).toEqual(['necklace']);
    expect(parseParts(null, { kinds: ['necklace', 'earrings'] })).toEqual(['necklace', 'earrings']);
  });
});
