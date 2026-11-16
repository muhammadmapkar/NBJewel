// Role: Verifies authentication URL boundaries and safe post-login navigation.
import { describe, expect, it } from 'vitest';
import { isApiUrl, safeErpReturnUrl } from './auth-policy';

describe('API credential boundary', () => {
  it('accepts only the configured API origin and path', () => {
    expect(isApiUrl('https://erp.example/api/auth/me', 'https://erp.example')).toBe(true);
    expect(isApiUrl('/api/auth/me', 'https://erp.example')).toBe(false);
    expect(isApiUrl('//erp.example/api/auth/me', 'https://erp.example')).toBe(false);
    expect(isApiUrl('https://erp.example.evil/api/auth/me', 'https://erp.example')).toBe(false);
    expect(isApiUrl('https://erp.example@evil.example/api/auth/me', 'https://erp.example')).toBe(false);
    expect(isApiUrl('https://erp.example/api/auth/me', 'http://erp.example')).toBe(false);
    expect(isApiUrl('https://erp.example/apiary', 'https://erp.example')).toBe(false);
    expect(isApiUrl('https://erp.example/tenant/api/auth/me', 'https://erp.example/tenant')).toBe(true);
    expect(isApiUrl('https://erp.example/api/auth/me', 'https://erp.example/tenant')).toBe(false);
  });
});

describe('ERP return navigation', () => {
  it('keeps supported ERP paths', () => {
    expect(safeErpReturnUrl('/catalog/42?tab=stock')).toBe('/catalog/42?tab=stock');
    expect(safeErpReturnUrl('/owner/jewelry')).toBe('/owner/jewelry');
    expect(safeErpReturnUrl('/web-portal')).toBe('/web-portal');
  });
  it('rejects external URLs, auth loops and malformed paths', () => {
    for (const path of [null, '//evil.example', 'https://evil.example', '/\\evil', '/sign-in', '/access-denied', '/tryon', '/catalog;evil=1', '/catalog/%2f%2fevil', '/unknown']) {
      expect(safeErpReturnUrl(path)).toBe('/home');
    }
  });
});
