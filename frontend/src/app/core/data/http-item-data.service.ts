// Role: Implements ItemDataService by calling the .NET API with HttpClient.

import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Category, Item, ItemSearchParams, ItemWriteRequest, MetalRate,
  PagedResult, PriceBreakdown, StockLevel, Store
} from '../models';
import { ItemDataService } from './item-data.service';

@Injectable()
export class HttpItemDataService extends ItemDataService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

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

  override getPrice(itemId: number): Observable<PriceBreakdown> {
    return this.http.get<PriceBreakdown>(`${this.baseUrl}/api/items/${itemId}/price`);
  }

  override getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.baseUrl}/api/categories`);
  }

  override getStores(): Observable<Store[]> {
    return this.http.get<Store[]>(`${this.baseUrl}/api/stores`);
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
}
