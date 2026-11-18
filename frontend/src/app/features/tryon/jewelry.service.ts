// Role: Calls the .NET try-on jewelry endpoints. The owner PIN is typed by the owner, kept in
// memory only for this tab, and sent in the X-Owner-Pin header. The real PIN lives only in the
// backend OWNER_PIN environment variable (assumed demo gate, replace with real auth later).
// When there is no API, the legacy gallery is empty and the owner page is disabled.

import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, of, throwError } from 'rxjs';
import { ApiError } from '../../core/http/error.interceptor';
import { environment } from '../../../environments/environment';
import { JewelryItem, JewelryType } from './jewelry.models';

@Injectable({ providedIn: 'root' })
export class JewelryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;
  readonly sampleMode = !environment.apiBaseUrl;
  // The PIN the owner entered in this tab. Never saved to storage.
  readonly pin = signal<string | null>(null);

  imageSrc(item: JewelryItem): string {
    if (/^https?:\/\//.test(item.imageUrl)) return item.imageUrl;
    return this.sampleMode ? new URL(item.imageUrl.replace(/^\//, ''), document.baseURI).href : `${this.baseUrl}${item.imageUrl}`;
  }

  listActive(): Observable<JewelryItem[]> {
    if (this.sampleMode) return of([]);
    return this.http.get<JewelryItem[]>(`${this.baseUrl}/api/jewelry`);
  }

  getActive(id: string): Observable<JewelryItem | undefined> {
    return this.listActive().pipe(map(items => items.find(x => x.id === id)));
  }

  listAll(pin: string): Observable<JewelryItem[]> {
    if (this.sampleMode) return throwError(() => new ApiError('The owner page needs the API. Set NBJEWEL_API_URL and deploy again.', 0));
    return this.http.get<JewelryItem[]>(`${this.baseUrl}/api/jewelry/all`, { headers: this.pinHeaders(pin) });
  }

  upload(name: string, type: JewelryType, file: Blob, fileName: string): Observable<JewelryItem> {
    const form = new FormData();
    form.append('name', name);
    form.append('type', type);
    form.append('file', file, fileName);
    return this.http.post<JewelryItem>(`${this.baseUrl}/api/jewelry`, form, { headers: this.pinHeaders() });
  }

  setActive(id: string, active: boolean): Observable<JewelryItem> {
    return this.http.patch<JewelryItem>(`${this.baseUrl}/api/jewelry/${encodeURIComponent(id)}/active`, { active }, { headers: this.pinHeaders() });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/jewelry/${encodeURIComponent(id)}`, { headers: this.pinHeaders() });
  }

  private pinHeaders(pin = this.pin()): HttpHeaders {
    return new HttpHeaders({ 'X-Owner-Pin': pin ?? '' });
  }
}
