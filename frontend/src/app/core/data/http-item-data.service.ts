// Role: Implements ItemDataService by calling the .NET API with HttpClient.
// Images have no API endpoints yet, so those methods hand off to
// InMemorySampleDataService. When the backend adds endpoints, replace the
// bodies at the bottom of this class with HttpClient calls.

import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { forkJoin, map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Category, Design, Item, ItemImage, ItemSearchParams, ItemVariant, ItemWriteRequest, MetalRate,
  PagedResult, PriceBreakdown, StockAddRequest, StockLevel, Store
} from '../models';
import { InMemorySampleDataService } from './in-memory-sample-data.service';
import { ItemDataService } from './item-data.service';

// A variant as the API sends it: no stock number.
type ApiVariant = Omit<ItemVariant, 'stock'>;

@Injectable()
export class HttpItemDataService extends ItemDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;
  private readonly sample = inject(InMemorySampleDataService);

  override searchItems(params: ItemSearchParams = {}): Observable<PagedResult<Item>> {
    let query = new HttpParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== '') {
        query = query.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<Item>>(`${this.baseUrl}/api/items`, { params: query });
  }

  override getItem(id: number): Observable<Item> {
    return this.http.get<Item>(`${this.baseUrl}/api/items/${id}`);
  }

  override getStock(itemId: number): Observable<StockLevel[]> {
    return this.http.get<StockLevel[]>(`${this.baseUrl}/api/items/${itemId}/stock`);
  }

  override addStock(itemId: number, request: StockAddRequest): Observable<StockLevel> {
    return this.http.post<StockLevel>(`${this.baseUrl}/api/items/${itemId}/stock`, request);
  }

  override getLowStock(threshold: number): Observable<StockLevel[]> {
    return this.http.get<StockLevel[]>(`${this.baseUrl}/api/stock/low`, { params: new HttpParams().set('threshold', threshold) });
  }

  override getPrice(itemId: number): Observable<PriceBreakdown> {
    return this.http.get<PriceBreakdown>(`${this.baseUrl}/api/items/${itemId}/price`);
  }

  override getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.baseUrl}/api/categories`);
  }

  override getStores(): Observable<Store[]> {
    return this.http.get<Store[]>(`${this.baseUrl}/api/stores`);
  }

  override getDesigns(): Observable<Design[]> {
    return this.http.get<Design[]>(`${this.baseUrl}/api/designs`);
  }

  override getLatestMetalRates(): Observable<MetalRate[]> {
    return this.http.get<MetalRate[]>(`${this.baseUrl}/api/metal-rates/latest`);
  }

  override createItem(request: ItemWriteRequest): Observable<Item> {
    return this.http.post<Item>(`${this.baseUrl}/api/items`, request);
  }

  override updateItem(id: number, request: ItemWriteRequest): Observable<Item> {
    return this.http.put<Item>(`${this.baseUrl}/api/items/${id}`, request);
  }

  override updateItemStatus(id: number, status: string): Observable<Item> {
    return this.http.patch<Item>(`${this.baseUrl}/api/items/${id}/status`, { status });
  }

  // The API sends variants and stock separately. Stock per variant is the sum over stores.
  override getVariants(itemId: number): Observable<ItemVariant[]> {
    return forkJoin({
      variants: this.http.get<ApiVariant[]>(`${this.baseUrl}/api/items/${itemId}/variants`),
      stock: this.getStock(itemId)
    }).pipe(
      map(({ variants, stock }) => variants.map(v => ({
        ...v,
        stock: stock.filter(s => s.variantId === v.id).reduce((sum, s) => sum + s.onHand, 0)
      })))
    );
  }

  // Assumed, no backend endpoint yet: served from in-memory sample data.
  override getImages(itemId: number): Observable<ItemImage[]> { return this.sample.getImages(itemId); }
  override addImage(itemId: number, fileName: string): Observable<ItemImage> { return this.sample.addImage(itemId, fileName); }
  override setPrimaryImage(itemId: number, imageId: number): Observable<void> { return this.sample.setPrimaryImage(itemId, imageId); }
  override removeImage(itemId: number, imageId: number): Observable<void> { return this.sample.removeImage(itemId, imageId); }
  override reorderImages(itemId: number, imageIds: number[]): Observable<void> { return this.sample.reorderImages(itemId, imageIds); }
}
