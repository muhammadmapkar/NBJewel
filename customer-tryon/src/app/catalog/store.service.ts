// Role: The customer's bag and wishlist, and the last store reservation. Kept in this browser
// only (localStorage), as a convenience: nothing is sent anywhere. Storage can be blocked (private
// mode), so every access is guarded and the app still works for the visit.

import { Injectable, computed, signal } from '@angular/core';

const KEY = 'nbjewel.customer.v1';

export interface Reservation {
  reference: string;
  name: string;
  phone: string;
  branch: string;
  date: string;
  items: string[]; // product ids
  createdAt: string;
}

interface Saved { bag: string[]; wishlist: string[]; reservation: Reservation | null }

function read(): Saved {
  try {
    const raw = localStorage.getItem(KEY);
    if (raw) {
      const s = JSON.parse(raw) as Partial<Saved>;
      return { bag: Array.isArray(s.bag) ? s.bag : [], wishlist: Array.isArray(s.wishlist) ? s.wishlist : [], reservation: s.reservation ?? null };
    }
  } catch { /* storage blocked or bad data: start empty */ }
  return { bag: [], wishlist: [], reservation: null };
}

// A short reference like "NBJ-7K3Q9P" (no look-alike letters).
export function makeReference(random: () => number = Math.random): string {
  const chars = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
  let out = '';
  for (let i = 0; i < 6; i++) out += chars[Math.floor(random() * chars.length)];
  return `NBJ-${out}`;
}

@Injectable({ providedIn: 'root' })
export class StoreService {
  private readonly saved = signal<Saved>(read());
  readonly bag = computed(() => this.saved().bag);
  readonly wishlist = computed(() => this.saved().wishlist);
  readonly reservation = computed(() => this.saved().reservation);
  readonly bagCount = computed(() => this.bag().length);

  inBag(id: string): boolean { return this.bag().includes(id); }
  inWishlist(id: string): boolean { return this.wishlist().includes(id); }

  addToBag(id: string): void { if (!this.inBag(id)) this.update(s => ({ ...s, bag: [...s.bag, id] })); }
  removeFromBag(id: string): void { this.update(s => ({ ...s, bag: s.bag.filter(x => x !== id) })); }
  toggleWishlist(id: string): void {
    this.update(s => ({ ...s, wishlist: s.wishlist.includes(id) ? s.wishlist.filter(x => x !== id) : [...s.wishlist, id] }));
  }

  // Demo only: the reservation stays on this phone; it is not sent to the shop.
  reserve(details: Omit<Reservation, 'reference' | 'createdAt' | 'items'>): Reservation {
    const reservation: Reservation = { ...details, items: [...this.bag()], reference: makeReference(), createdAt: new Date().toISOString() };
    this.update(s => ({ ...s, bag: [], reservation }));
    return reservation;
  }

  private update(change: (s: Saved) => Saved): void {
    const next = change(this.saved());
    this.saved.set(next);
    try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* remembering is optional */ }
  }
}
