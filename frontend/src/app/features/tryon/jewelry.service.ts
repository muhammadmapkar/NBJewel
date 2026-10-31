// Role: Calls the .NET try-on jewelry endpoints. The owner PIN is typed by the owner, kept in
// memory only for this tab, and sent in the X-Owner-Pin header. The real PIN lives only in the
// backend OWNER_PIN environment variable (assumed demo gate, replace with real auth later).

import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { JewelryItem, JewelryType } from './jewelry.models';

@Injectable({ providedIn: 'root' })
export class JewelryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;
  // The PIN the owner entered in this tab. Never saved to storage.
  readonly pin = signal<string | null>(null);

  imageSrc(item: JewelryItem): string {
    return /^https?:\/\//.test(item.imageUrl) ? item.imageUrl : `${this.baseUrl}${item.imageUrl}`;
  }

  listActive(): Observable<JewelryItem[]> {
    return this.http.get<JewelryItem[]>(`${this.baseUrl}/api/jewelry`);
  }

  getActive(id: string): Observable<JewelryItem | undefined> {
    return this.listActive().pipe(map(items => items.find(x => x.id === id)));
  }

  listAll(pin: string): Observable<JewelryItem[]> {
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
