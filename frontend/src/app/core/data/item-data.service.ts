// Role: Defines the data contract the screens depend on. Screens inject this
// class, so the HTTP version can later be swapped for another source.

import { Observable } from 'rxjs';
import {
  Category, Item, ItemImage, ItemSearchParams, ItemVariant, ItemWriteRequest, MetalRate,
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

  // Variants and images. The .NET API has no endpoints for these yet, so they
  // are served by InMemorySampleDataService (see HttpItemDataService).
  abstract getVariants(itemId: number): Observable<ItemVariant[]>;
  abstract getImages(itemId: number): Observable<ItemImage[]>;
  abstract addImage(itemId: number, fileName: string): Observable<ItemImage>;
  abstract setPrimaryImage(itemId: number, imageId: number): Observable<void>;
  abstract removeImage(itemId: number, imageId: number): Observable<void>;
  // Saves a new gallery order. The list holds every image id in the wanted order.
  abstract reorderImages(itemId: number, imageIds: number[]): Observable<void>;
}
