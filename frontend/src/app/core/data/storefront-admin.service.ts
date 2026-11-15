// Role: Calls protected ERP image/review/publication endpoints. Existing auth/CSRF interceptors
// apply to these requests; sample publication persistence stays on the backend.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Item, StorefrontMetadata } from '../models';
export interface StorefrontAsset { itemId: number; revisionId: string; state: string; qualityReview: string; licenseStatus: string; width: number; height: number; }
export interface StorefrontSuggestion { revisionId: string; description: string; collection: string; }
export interface PublicationState { itemId: number; published: boolean; assetRevisionId: string | null; }
// SQL assigns category IDs; use the loaded category name. The API independently rechecks eligibility.
export function publicationReady(item: Item, asset: StorefrontAsset | null, categoryName: string): boolean {
  return item.status === 'Complete' && ['Necklaces', 'Earrings'].includes(categoryName) && !!item.storefront?.description.trim() &&
    asset?.itemId === item.id && asset.state === 'ready' && asset.qualityReview === 'approved';
}
@Injectable({ providedIn: 'root' })
export class StorefrontAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl.replace(/\/$/, '');
  asset(id: number): Observable<StorefrontAsset | null> {
    return this.http.get<StorefrontAsset>(`${this.base}/api/items/${id}/image`).pipe(
      catchError((error: { status?: number }) => error.status === 404 ? of(null) : throwError(() => error)));
  }
  upload(id: number, file: File): Observable<StorefrontAsset> {
    const form = new FormData(); form.append('file', file);
    return this.http.post<StorefrontAsset>(`${this.base}/api/items/${id}/image`, form);
  }
  preview(id: number, revisionId: string): Observable<Blob> { return this.http.get(`${this.base}/api/items/${id}/image/preview`, { params: { revisionId }, responseType: 'blob' }); }
  analyze(id: number, revisionId: string): Observable<StorefrontSuggestion> { return this.http.post<StorefrontSuggestion>(`${this.base}/api/items/${id}/image/analyze`, { revisionId }); }
  review(id: number, revisionId: string): Observable<StorefrontAsset> { return this.http.post<StorefrontAsset>(`${this.base}/api/items/${id}/image/review`, { revisionId, approve: true, confirm: true }); }
  state(id: number): Observable<PublicationState> { return this.http.get<PublicationState>(`${this.base}/api/ai/knowledge/products/${id}`); }
  publish(id: number, assetRevisionId: string): Observable<void> { return this.http.post<void>(`${this.base}/api/ai/knowledge/products/${id}/publish`, { assetRevisionId, confirm: true }); }
  revoke(id: number): Observable<void> { return this.http.post<void>(`${this.base}/api/ai/knowledge/products/${id}/revoke`, { confirm: true }); }
  saveCopy(item: Item, storefront: StorefrontMetadata): Observable<Item> { return this.http.put<Item>(`${this.base}/api/items/${item.id}`, { ...item, storefront }); }
}
