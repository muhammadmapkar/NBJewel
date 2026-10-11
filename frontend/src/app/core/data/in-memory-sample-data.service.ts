// Role: Fake data for images while the .NET API has no endpoints for them. Everything here is assumed, no backend endpoint yet. Data lives in
// memory only and resets when the page is reloaded.

import { Injectable } from '@angular/core';
import { delay, Observable, of, throwError } from 'rxjs';
import { ItemImage } from '../models';

const LATENCY_MS = 250;
const SWATCHES = ['#f3e3a4', '#e9d08a', '#f8f0dc', '#d9b96a'];

// Builds a small picture as a data address, so no image files or network are needed.
export function placeholderImage(label: string, seed: number): string {
  const color = SWATCHES[Math.abs(seed) % SWATCHES.length];
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="240" height="240"><rect width="240" height="240" fill="${color}"/>`
    + `<text x="120" y="128" font-family="Arial" font-size="18" text-anchor="middle" fill="#8b6900">${label}</text></svg>`;
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

@Injectable()
export class InMemorySampleDataService {
  private readonly images = new Map<number, ItemImage[]>();
  private nextImageId = 1000;

  getImages(itemId: number): Observable<ItemImage[]> {
    return of(this.list(itemId).map(i => ({ ...i }))).pipe(delay(LATENCY_MS));
  }

  addImage(itemId: number, fileName: string): Observable<ItemImage> {
    const list = this.list(itemId);
    const image: ItemImage = {
      id: this.nextImageId++,
      itemId,
      url: placeholderImage('Uploaded', this.nextImageId),
      caption: fileName,
      isPrimary: list.length === 0,
      sortOrder: list.length + 1
    };
    list.push(image);
    return of({ ...image }).pipe(delay(LATENCY_MS));
  }

  setPrimaryImage(itemId: number, imageId: number): Observable<void> {
    const list = this.list(itemId);
    if (!list.some(i => i.id === imageId)) return throwError(() => new Error('That image was not found.'));
    list.forEach(i => { i.isPrimary = i.id === imageId; });
    return of(undefined).pipe(delay(LATENCY_MS));
  }

  removeImage(itemId: number, imageId: number): Observable<void> {
    const list = this.list(itemId);
    const index = list.findIndex(i => i.id === imageId);
    if (index < 0) return throwError(() => new Error('That image was not found.'));
    const [removed] = list.splice(index, 1);
    // If the primary image was removed, the first remaining image takes its place.
    if (removed.isPrimary && list.length > 0) list[0].isPrimary = true;
    list.forEach((image, i) => { image.sortOrder = i + 1; });
    return of(undefined).pipe(delay(LATENCY_MS));
  }

  reorderImages(itemId: number, imageIds: number[]): Observable<void> {
    const list = this.list(itemId);
    const ordered = imageIds.map(id => list.find(i => i.id === id));
    if (ordered.length !== list.length || ordered.some(i => !i)) {
      return throwError(() => new Error('The image order was not valid.'));
    }
    list.splice(0, list.length, ...(ordered as ItemImage[]));
    list.forEach((image, i) => { image.sortOrder = i + 1; });
    return of(undefined).pipe(delay(LATENCY_MS));
  }

  // Creates the starting images for an item the first time it is asked for.
  private list(itemId: number): ItemImage[] {
    let list = this.images.get(itemId);
    if (!list) {
      const count = (itemId + 2) % 4;
      list = Array.from({ length: count }, (_, index): ItemImage => ({
        id: itemId * 10 + index + 1,
        itemId,
        url: placeholderImage(`Item ${itemId} view ${index + 1}`, itemId + index),
        caption: `View ${index + 1}`,
        isPrimary: index === 0,
        sortOrder: index + 1
      }));
      this.images.set(itemId, list);
    }
    return list;
  }
}
