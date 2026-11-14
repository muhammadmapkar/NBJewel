// Role: Loads a nonsecret runtime API address before pages start. Missing/unset config uses
// the bundled sample catalog; invalid or unavailable configured data produces a visible error.
import { InjectionToken } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, timeout } from 'rxjs';
export interface StorefrontConfig { apiBaseUrl: string | null; portalApiBaseUrl?: string | null; error?: string; }
export const STOREFRONT_CONFIG = new InjectionToken<StorefrontConfig>('storefront runtime config', { providedIn: 'root', factory: () => ({ apiBaseUrl: null }) });
export function resolveApiBase(value: unknown, pageBase: string): string | null {
  if (value === null || value === undefined || (typeof value === 'string' && !value.trim())) return null;
  if (typeof value !== 'string') throw new Error('Invalid API address.');
  const url = new URL(value.trim(), pageBase);
  if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error('Invalid API address.');
  return url.href.replace(/\/+$/, '');
}
export async function loadStorefrontConfig(http: HttpClient, config: StorefrontConfig, pageBase: string): Promise<void> {
  delete config.error; delete config.portalApiBaseUrl; config.apiBaseUrl = null;
  try {
    const value = await firstValueFrom(http.get<{ apiBaseUrl?: unknown; portalApiBaseUrl?: unknown }>(new URL('storefront-config.json', pageBase).href, { headers: { 'Cache-Control': 'no-cache' } }).pipe(timeout(5000)));
    config.apiBaseUrl = resolveApiBase(value?.apiBaseUrl, pageBase);
    if (value?.portalApiBaseUrl !== undefined) config.portalApiBaseUrl = resolveApiBase(value.portalApiBaseUrl, pageBase);
  } catch (error) {
    if ((error as { status?: number }).status !== 404) config.error = 'Could not load storefront configuration.';
  }
}
