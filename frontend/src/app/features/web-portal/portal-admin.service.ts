// Role: Defines assumed fixed portal editor contracts and calls membership-scoped APIs through the existing cookie/CSRF interceptor.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
export interface PortalSettings {
  name: string; colorPreset: string; logoAssetId?: string | null; bannerAssetId?: string | null; headline: string; introduction: string; story: string;
  phone: string; whatsApp: string; email: string; openingHours: string;
  showBanner: boolean; showCollections: boolean; showFeatured: boolean; showStory: boolean; showStores: boolean; showMarketRates: boolean;
  showPrices: boolean; enquiries: boolean; tryOn: boolean; bag: boolean; reservations: boolean;
  visibleCollections: string[]; featuredItemIds: number[]; visibleBranchIds: number[];
}
export interface PortalDraft { revision: number; settings: PortalSettings; online: boolean; publishedRevision?: number | null; }
export interface PortalChoices { branches: { id: number; name: string }[]; products: { id: number; name: string; collection: string }[]; collections: string[]; }
export const PORTAL_FLAGS: { key: keyof Pick<PortalSettings, 'showBanner'|'showCollections'|'showFeatured'|'showStory'|'showStores'|'showMarketRates'|'showPrices'|'enquiries'|'tryOn'|'bag'|'reservations'>; label: string }[] = [
  { key:'showBanner', label:'Homepage banner' }, { key:'showCollections', label:'Collections' }, { key:'showFeatured', label:'Featured designs' }, { key:'showStory', label:'Shop story' }, { key:'showStores', label:'Stores section' }, { key:'showMarketRates', label:'Market reference section' }, { key:'showPrices', label:'Show prices' }, { key:'enquiries', label:'WhatsApp enquiries' }, { key:'tryOn', label:'Try-on' }, { key:'bag', label:'Customer bag' }, { key:'reservations', label:'Reservation requests' }
];
@Injectable({ providedIn: 'root' })
export class PortalAdminService {
  private readonly http = inject(HttpClient); private readonly base = environment.apiBaseUrl.replace(/\/$/, '');
  private storefront = '';
  private root(slug: string): string { if (!/^[a-z0-9-]{3,40}$/.test(slug)) throw new Error('Invalid business.'); return `${this.base}/api/businesses/${slug}/portal`; }
  configuration(): Observable<string> { return this.http.get<{ storefrontBaseUrl: string }>('/portal-config.json').pipe(map(value => {
    const url = new URL(value.storefrontBaseUrl);
    if (url.username || url.password || url.search || url.hash || (url.protocol !== 'https:' && !(url.protocol === 'http:' && (url.hostname === 'localhost' || /^127\.[0-9.]+$/.test(url.hostname))))) throw new Error('Invalid storefront preview address.');
    return url.href.replace(/\/$/, '');
  }), tap(value => this.storefront = value)); }
  draft(slug: string): Observable<PortalDraft> { return this.http.get<PortalDraft>(this.root(slug) + '/draft'); }
  choices(slug: string): Observable<PortalChoices> { return this.http.get<PortalChoices>(this.root(slug) + '/choices'); }
  save(slug: string, revision: number, settings: PortalSettings): Observable<PortalDraft> { return this.http.put<PortalDraft>(this.root(slug) + '/draft', { revision, settings }); }
  publish(slug: string, revision: number): Observable<PortalDraft> { return this.http.post<PortalDraft>(this.root(slug) + '/publish', { revision }); }
  offline(slug: string, revision: number): Observable<PortalDraft> { return this.http.post<PortalDraft>(this.root(slug) + '/offline', { revision }); }
  upload(slug: string, file: File): Observable<{ id: string }> { const body = new FormData(); body.set('file', file); return this.http.post<{ id: string }>(this.root(slug) + '/assets', body); }
  previewUrl(slug: string): string { this.root(slug); if (!this.storefront) throw new Error('Preview address is unavailable.'); return `${this.storefront}/shops/${slug}/preview`; }
}
