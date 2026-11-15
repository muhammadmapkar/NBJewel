// Role: Keeps assumed shop-specific customer state on the backend and device wishlists scoped by business and identity.
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { CustomerSession, customerError } from '../customer/customer-session';
import { CatalogService } from './catalog.service';
export interface Reservation { reference: string; name: string; phone: string; branch: string; date: string; items: string[]; createdAt: string; source: string; status: string; }
export interface CustomerState { name: string; phone: string; bag: { id: string; source: string }[]; reservations: Reservation[]; }
const empty = (): CustomerState => ({ name: '', phone: '', bag: [], reservations: [] });
// Retained for existing reference-format consumers; server generates actual reservation IDs.
export function makeReference(random: () => number = Math.random): string { const chars = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789'; return 'NBJ-' + Array.from({ length: 6 }, () => chars[Math.floor(random() * chars.length)]).join(''); }
@Injectable({ providedIn: 'root' })
export class StoreService {
  readonly session = inject(CustomerSession); private readonly catalog = inject(CatalogService); private ownerKey = '';
  readonly shopSlug = signal('nb-jewel-demo');
  readonly profile = signal<CustomerState>(empty()); readonly error = signal(''); readonly working = signal(false);
  readonly wishlist = signal<string[]>([]); private sequence = 0; private wishlistKey = '';
  get source(): string { return this.catalog.source(); }
  readonly bag = computed(() => this.profile().bag.filter(x => x.source === this.source).map(x => x.id));
  readonly bagCount = computed(() => this.bag().length);
  readonly reservations = computed(() => this.profile().reservations);
  readonly reservation = computed(() => this.reservations().find(x => x.source === this.source) ?? null);
  constructor() { effect(() => { this.session.identity(); this.catalog.source(); this.shopSlug(); untracked(() => this.synchronize()); }); }
  private synchronize(): void {
    const user = this.session.identity(); const slug = this.shopSlug(); const owner = user?.id ?? 'guest'; const key = `${slug}.${owner}`;
    if (key === this.ownerKey) return; this.ownerKey = key;
    this.sequence++; this.profile.set(empty()); this.error.set(''); this.wishlistKey = `nbjewel.wishlist.v3.${key}`;
    try {
      const stored = localStorage.getItem(this.wishlistKey);
      // Assumed one-time copy: demo-source IDs are not ERP IDs and are never migrated.
      const legacy = stored === null && slug === 'nb-jewel-demo' ? localStorage.getItem(`nbjewel.wishlist.v2.erp.${owner}`) : null;
      const value: unknown = JSON.parse(stored ?? legacy ?? '[]');
      const ids = Array.isArray(value) ? value.filter(x => typeof x === 'string').slice(0, 100) : [];
      this.wishlist.set(ids);
      if (stored === null && slug === 'nb-jewel-demo') localStorage.setItem(this.wishlistKey, JSON.stringify(ids));
    } catch { this.wishlist.set([]); }
    if (user) void this.refresh();
  }
  selectShop(slug: string): void {
    if (!/^[a-z0-9-]{3,40}$/.test(slug)) throw new Error('Invalid shop.');
    this.shopSlug.set(slug); this.synchronize();
  }
  private endpoint(path: string): string { return `/api/customer/shops/${encodeURIComponent(this.shopSlug())}/${path}`; }
  inBag(id: string): boolean { return this.bag().includes(id); }
  inWishlist(id: string): boolean { return this.wishlist().includes(id); }
  toggleWishlist(id: string): void { const next = this.inWishlist(id) ? this.wishlist().filter(x => x !== id) : [...this.wishlist(), id].slice(0, 100); this.wishlist.set(next); try { localStorage.setItem(this.wishlistKey, JSON.stringify(next)); } catch { /* Optional local preference. */ } }
  async refresh(): Promise<void> {
    const user = this.session.identity()?.id; const sequence = ++this.sequence;
    try { const state = await this.session.read<CustomerState>(this.endpoint('state')); if (user === this.session.identity()?.id && sequence === this.sequence) this.profile.set(state); }
    catch (error) { if (sequence === this.sequence) this.error.set(customerError(error)); }
  }
  private async update(method: 'POST' | 'PUT' | 'DELETE', path: string, body: unknown): Promise<boolean> {
    if (this.working()) { this.error.set('Please wait for your current request to finish.'); return false; }
    this.synchronize();
    const user = this.session.identity()?.id; const sequence = ++this.sequence; this.working.set(true); this.error.set('');
    try { const state = await this.session.mutate<CustomerState>(method, path, body); if (user !== this.session.identity()?.id || sequence !== this.sequence) return false; this.profile.set(path === '/api/customer/profile' ? { ...this.profile(), name: state.name, phone: state.phone } : state); return true; }
    catch (error) { if (sequence === this.sequence) this.error.set(customerError(error)); return false; }
    finally { this.working.set(false); }
  }
  async addToBag(id: string): Promise<boolean> { const source = this.source; const slug = this.shopSlug(); if (!await this.session.require('Sign in to add this design to your bag') || slug !== this.shopSlug()) return false; return this.update('POST', this.endpoint('bag'), { id, source }); }
  async removeFromBag(id: string): Promise<void> { const slug = this.shopSlug(); if (await this.session.require() && slug === this.shopSlug()) await this.update('DELETE', this.endpoint(`bag/${encodeURIComponent(id)}`), {}); }
  async saveProfile(name: string, phone: string): Promise<boolean> { if (!await this.session.require()) return false; return this.update('PUT', '/api/customer/profile', { name, phone }); }
  async reserve(details: { name: string; phone: string; storeId: string; date: string; requestId: string; items: string[] }): Promise<boolean> {
    const slug = this.shopSlug(); if (!await this.session.require('Sign in to reserve your selected designs') || slug !== this.shopSlug()) return false;
    return this.update('POST', this.endpoint('reservations'), { ...details, source: this.source });
  }
}
