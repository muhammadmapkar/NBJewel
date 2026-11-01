// Role: Loads the customer catalog once from public/catalog/catalog.json (bundled with the app,
// so it works on a static host with no API) and shares it between pages.

import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Product } from './catalog.models';

export type CatalogState = { status: 'loading' } | { status: 'ready'; products: Product[] } | { status: 'error' };

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  readonly state = signal<CatalogState>({ status: 'loading' });
  private requested = false;

  load(force = false): void {
    if (this.requested && !force) return;
    this.requested = true;
    this.state.set({ status: 'loading' });
    this.http.get<Product[]>(this.url('catalog/catalog.json')).subscribe({
      next: products => this.state.set({ status: 'ready', products }),
      error: () => this.state.set({ status: 'error' })
    });
  }

  products(): Product[] {
    const s = this.state();
    return s.status === 'ready' ? s.products : [];
  }

  find(id: string): Product | undefined {
    return this.products().find(p => p.id === id);
  }

  url(path: string): string {
    return new URL(path, document.baseURI).href;
  }
}
