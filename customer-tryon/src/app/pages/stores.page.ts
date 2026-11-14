// Role: Public store discovery with a keyless interactive map, consent-based nearest sorting and store-specific catalog links.
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { StoreDirectoryService, PublicStore, distanceKm } from '../customer/store-directory.service';
import { SiteHeaderComponent } from '../ui/site-header.component';
@Component({ selector: 'ct-stores', standalone: true, imports: [RouterLink, SiteHeaderComponent], template: `
<ct-site-header [back]="true" /><main class="portal-page">
<p class="eyebrow">A personal visit</p><h1 class="serif">Find Nearest NB JEWEL Store</h1>
<p class="muted">Explore stores, compare availability and find a design to try in person. Sample locations are illustrative, not verified NB JEWEL branches.</p>
<button class="btn" (click)="locate()">Use my location</button> <button class="btn ghost" (click)="directory.load(true)">Refresh stores</button>
@if (notice()) { <p role="status">{{ notice() }}</p> } @if (directory.error()) { <p role="alert">{{ directory.error() }}</p> }
<div class="store-layout"><div class="store-list">
@for (store of sorted(); track store.location.id) { <article [class.selected]="selected()?.location?.id === store.location.id">
<button class="select-store" (click)="selected.set(store)"><b>{{ store.location.name }}</b><span>{{ store.location.city }} @if (position()) { · {{ distance(store).toFixed(1) }} km }</span></button>
<p class="badge">{{ store.location.isSample ? 'Sample location' : store.liveSynced ? 'ERP connected · recently synced' : 'Sync not verified or stale' }}</p>
<p>{{ store.productCount }} {{ store.location.source === 'demo' ? 'sample designs' : 'published designs in stock' }}</p>
@if (store.location.inventorySyncedAt) { <p class="muted small">Inventory sync: {{ store.location.inventorySyncedAt }}</p> }
<a class="btn ghost" routerLink="/shop" [queryParams]="{ store: store.location.id, source: store.location.source }">Browse this store</a></article> }
</div><div class="map-wrap">@if (mapUrl(); as url) { <iframe title="Interactive map of selected store" [src]="url" loading="lazy" referrerpolicy="no-referrer"></iframe><p class="muted small">Map © OpenStreetMap contributors. Select a store card to move the marker. Pan and zoom within the map.</p> } @else { <p>Select a store to view its map.</p> }</div></div></main>`, styles: [`
.store-layout { display:grid; grid-template-columns:minmax(260px,1fr) 2fr; gap:24px; margin-top:24px; } .store-list { display:grid; gap:14px; } article { padding:20px; background:var(--card); border:1px solid var(--line); border-radius:var(--radius); } article.selected { border-color:var(--gold); } .select-store { display:grid; gap:5px; text-align:left; border:0; background:none; color:var(--ink); padding:0; width:100%; min-height:48px; } .select-store b { font:600 24px var(--serif); } .badge { font-size:12px; color:var(--maroon); } iframe { width:100%; height:480px; border:1px solid var(--line); border-radius:var(--radius); } .map-wrap { position:sticky; top:80px; align-self:start; } @media(max-width:767px) { .store-layout { grid-template-columns:1fr; } .map-wrap { grid-row:1; position:static; } iframe { height:300px; } }
`] })
export class StoresPage {
  readonly directory = inject(StoreDirectoryService); private readonly sanitizer = inject(DomSanitizer);
  readonly selected = signal<PublicStore | null>(null); readonly position = signal<{ lat: number; lon: number } | null>(null); readonly notice = signal('');
  readonly sorted = computed(() => [...this.directory.stores()].sort((a, b) => this.position() ? this.distance(a) - this.distance(b) : a.location.name.localeCompare(b.location.name)));
  readonly mapUrl = computed(() => { const store = this.selected() ?? this.sorted()[0]; if (!store) return null; const { latitude: lat, longitude: lon } = store.location;
    return this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.openstreetmap.org/export/embed.html?bbox=${lon-.035},${lat-.025},${lon+.035},${lat+.025}&layer=mapnik&marker=${lat},${lon}`); });
  constructor() { void this.directory.load(); }
  distance(store: PublicStore): number { const position = this.position(); return position ? distanceKm(position.lat, position.lon, store.location.latitude, store.location.longitude) : 0; }
  locate(): void { if (!navigator.geolocation) { this.notice.set('Location is unavailable. Choose a store from the list.'); return; }
    navigator.geolocation.getCurrentPosition(p => { this.position.set({ lat: p.coords.latitude, lon: p.coords.longitude }); this.selected.set(this.sorted()[0] ?? null); this.notice.set('Sorted by distance. Your location stays in this browser.'); }, () => this.notice.set('Location was not shared. You can still browse every store.'), { timeout: 10000, maximumAge: 60000 }); }
}
