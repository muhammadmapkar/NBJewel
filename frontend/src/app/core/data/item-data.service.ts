// Role: Defines the data contract the screens depend on. Screens inject this
// class, so the HTTP version can later be swapped for another source.

import { Observable } from 'rxjs';
import {
  Category, Design, Item, ItemImage, ItemSearchParams, ItemVariant, ItemWriteRequest, MetalRate,
  PagedResult, PriceBreakdown, StockAddRequest, StockLevel, Store
} from '../models';

export abstract class ItemDataService {
  abstract searchItems(params?: ItemSearchParams): Observable<PagedResult<Item>>;
  abstract getItem(id: number): Observable<Item>;
  abstract getStock(itemId: number): Observable<StockLevel[]>;
  // Adds the quantity to on-hand stock. The API rejects a quantity of 0 or less.
  abstract addStock(itemId: number, request: StockAddRequest): Observable<StockLevel>;
  // Read-only. Stock rows with fewer than this many units available.
  abstract getLowStock(threshold: number): Observable<StockLevel[]>;
  abstract getPrice(itemId: number): Observable<PriceBreakdown>;
  abstract getCategories(): Observable<Category[]>;
  abstract getStores(): Observable<Store[]>;
  abstract getDesigns(): Observable<Design[]>;
  abstract getLatestMetalRates(): Observable<MetalRate[]>;
  abstract createItem(request: ItemWriteRequest): Observable<Item>;
  abstract updateItem(id: number, request: ItemWriteRequest): Observable<Item>;
  // Any of the four statuses can be set at any time. An unknown status returns 400.
  abstract updateItemStatus(id: number, status: string): Observable<Item>;
  abstract getVariants(itemId: number): Observable<ItemVariant[]>;

  // Images. The .NET API has no endpoints for these yet, so they
  // are served by InMemorySampleDataService (see HttpItemDataService).
  abstract getImages(itemId: number): Observable<ItemImage[]>;
  abstract addImage(itemId: number, fileName: string): Observable<ItemImage>;
  abstract setPrimaryImage(itemId: number, imageId: number): Observable<void>;
  abstract removeImage(itemId: number, imageId: number): Observable<void>;
  // Saves a new gallery order. The list holds every image id in the wanted order.
  abstract reorderImages(itemId: number, imageIds: number[]): Observable<void>;
}
