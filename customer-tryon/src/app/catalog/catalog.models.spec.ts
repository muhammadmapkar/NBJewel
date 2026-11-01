// Role: Unit tests for the catalog filter rules and the "parts" query parsing.

import { describe, expect, it } from 'vitest';
import { Product, matchesFilter, parseParts } from './catalog.models';

const set: Product = { id: 's', name: 'S', note: '', category: 'set', kinds: ['necklace', 'earrings', 'tikka'], card: '', parts: {} };
const earrings: Product = { id: 'e', name: 'E', note: '', category: 'earrings', kinds: ['earrings'], card: '', parts: {} };

describe('matchesFilter', () => {
  it('sets show under Sets and under each kind they contain', () => {
    expect(matchesFilter(set, 'set')).toBe(true);
    expect(matchesFilter(set, 'earrings')).toBe(true);
    expect(matchesFilter(set, 'necklace')).toBe(true);
  });

  it('single pieces show only under their kind and All', () => {
    expect(matchesFilter(earrings, 'set')).toBe(false);
    expect(matchesFilter(earrings, 'necklace')).toBe(false);
    expect(matchesFilter(earrings, 'earrings')).toBe(true);
    expect(matchesFilter(earrings, 'all')).toBe(true);
  });
});

describe('parseParts', () => {
  it('keeps only kinds the product has', () => {
    expect(parseParts('necklace,ring', set)).toEqual(['necklace']);
    expect(parseParts('tikka', earrings)).toEqual(['earrings']);
  });

  it('empty means everything', () => {
    expect(parseParts(null, set)).toEqual(['necklace', 'earrings', 'tikka']);
  });
});
