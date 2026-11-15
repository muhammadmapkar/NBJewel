// Role: Verifies public runtime configuration, safe URL parsing and an unset-address fallback.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { expect, it } from 'vitest';
import { resolveApiBase, loadStorefrontConfig, StorefrontConfig } from './storefront-config';
it('resolves public HTTP addresses and an unset address without allowing credentials or scripts', () => {
  expect(resolveApiBase(' https://api.example.test/root/ ', 'https://shop.example.test/')).toBe('https://api.example.test/root');
  expect(resolveApiBase('/backend', 'https://shop.example.test/demo/')).toBe('https://shop.example.test/backend');
  expect(resolveApiBase('', 'https://shop.example.test/')).toBeNull();
  for (const input of ['javascript:alert(1)', 'https://user:password@api.example.test', 'https://api.example.test?secret=value', 42]) expect(() => resolveApiBase(input, 'https://shop.example.test/')).toThrow();
});
it('loads configuration independently of the build and reports a missing config as an error', async () => {
  const injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector);
  const http = injector.get(HttpTestingController); const config: StorefrontConfig = { apiBaseUrl: null };
  try {
    const first = loadStorefrontConfig(injector.get(HttpClient), config, 'https://shop.example.test/base/');
    http.expectOne('https://shop.example.test/base/storefront-config.json').flush({ apiBaseUrl: 'https://api.example.test/' }); await first; expect(config.apiBaseUrl).toBe('https://api.example.test');
    const second = loadStorefrontConfig(injector.get(HttpClient), config, 'https://shop.example.test/base/');
    http.expectOne('https://shop.example.test/base/storefront-config.json').flush(null, { status: 404, statusText: 'Missing' }); await second; expect(config.apiBaseUrl).toBeNull(); expect(config.error).toBeTruthy();
    http.verify();
  } finally { injector.destroy(); }
});
