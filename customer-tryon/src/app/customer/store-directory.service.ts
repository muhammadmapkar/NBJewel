// Role: Loads public store locations, labels sample/fresh sync status and calculates distance locally after consent.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CustomerSession } from './customer-session';
export interface StoreLocation { id: string; name: string; city: string; latitude: number; longitude: number; source: 'demo' | 'erp'; isSample: boolean; erpEnabled: boolean; inventorySyncedAt: string | null; imagesSyncedAt: string | null; productIds: string[]; }
export interface PublicStore { location: StoreLocation; liveSynced: boolean; productCount: number; }
export function distanceKm(lat: number, lon: number, otherLat: number, otherLon: number): number {
  const rad = Math.PI / 180, dLat = (otherLat - lat) * rad, dLon = (otherLon - lon) * rad;
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(lat * rad) * Math.cos(otherLat * rad) * Math.sin(dLon / 2) ** 2;
  return 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(Math.max(0, 1 - a)));
}
@Injectable({ providedIn: 'root' })
export class StoreDirectoryService {
  private readonly http = inject(HttpClient); private readonly session = inject(CustomerSession);
  readonly stores = signal<PublicStore[]>([]); readonly error = signal(''); private loaded = false;
  async load(force = false): Promise<void> {
    if (this.loaded && !force) return; this.loaded = true; this.error.set('');
    try {
      const base = this.session.base;
      if (!base) throw new Error('API not configured.');
      const result = await firstValueFrom(this.http.get<PublicStore[]>(`${base}/api/storefront/stores`));
      if (!Array.isArray(result) || result.some(x => !x.location || !Number.isFinite(x.location.latitude) || !Number.isFinite(x.location.longitude) || Math.abs(x.location.latitude) > 85 || Math.abs(x.location.longitude) > 180)) throw new Error();
      this.stores.set(result);
    } catch { this.stores.set([]); this.error.set('Store information is unavailable. Please retry.'); }
  }
}
