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
  abstract getPrice(itemId: number): Observable<PriceBreakdown>;
  abstract getCategories(): Observable<Category[]>;
  abstract getStores(): Observable<Store[]>;
  abstract getDesigns(): Observable<Design[]>;
  abstract getLatestMetalRates(): Observable<MetalRate[]>;
  abstract createItem(request: ItemWriteRequest): Observable<Item>;
  abstract updateItem(id: number, request: ItemWriteRequest): Observable<Item>;
  // The API only allows the next status in the workflow and replies 409 otherwise.
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
