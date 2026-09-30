// Role: Defines the data contract the screens depend on. Screens inject this
// class, so the HTTP version can later be swapped for another source.

import { Observable } from 'rxjs';
import {
  Category, Item, ItemSearchParams, ItemWriteRequest, MetalRate,
  PagedResult, PriceBreakdown, StockLevel, Store
} from '../models';

export abstract class ItemDataService {
  abstract searchItems(params?: ItemSearchParams): Observable<PagedResult<Item>>;
  abstract getItem(id: number): Observable<Item>;
  abstract getStock(itemId: number): Observable<StockLevel[]>;
  abstract getPrice(itemId: number): Observable<PriceBreakdown>;
  abstract getCategories(): Observable<Category[]>;
  abstract getStores(): Observable<Store[]>;
  abstract getLatestMetalRates(): Observable<MetalRate[]>;
  abstract createItem(request: ItemWriteRequest): Observable<Item>;
  abstract updateItem(id: number, request: ItemWriteRequest): Observable<Item>;
  abstract updateItemStatus(id: number, status: string): Observable<Item>;
}
