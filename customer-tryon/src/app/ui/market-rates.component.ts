// Role: Presents timestamped market reference quotes separately from ERP selling-rate product calculations.
import { HttpClient } from '@angular/common/http';
import { Component, Injectable, inject, signal } from '@angular/core';
import { CustomerSession } from '../customer/customer-session';
interface Market { status: string; provider: string; quotes: { metal: string; perGram: number; currency: string; asOf: string }[]; }
@Injectable({ providedIn: 'root' })
export class MarketRates {
  private readonly http = inject(HttpClient); private readonly session = inject(CustomerSession);
  readonly value = signal<Market>({ status: 'unconfigured', provider: '', quotes: [] }); private requested = false;
  load(force = false): void { if (this.requested && !force) return; this.requested = true; const base = this.session.base; if (!base) return;
    this.http.get<Market>(`${base}/api/storefront/spot-rates`).subscribe({ next: result => this.value.set(result), error: () => this.value.set({ status: 'unavailable', provider: '', quotes: [] }) }); }
}
@Component({ selector: 'ct-market-rates', standalone: true, template: `<section aria-label="Metal market reference" class="market"><div><b>Gold · Silver · Platinum</b><button type="button" (click)="rates.load(true)">Refresh</button></div>
@if (rates.value().quotes.length) { <div class="quotes">@for (quote of rates.value().quotes; track quote.metal) { <span><b>{{ quote.metal }}</b> {{ quote.currency }} {{ quote.perGram.toFixed(2) }}/g <small>{{ quote.asOf }}</small></span> }</div><p>{{ rates.value().status === 'live' ? 'Recent market quotes' : 'Quotes are stale' }} · {{ rates.value().provider }} · pure-metal reference, not retail prices.</p> }
@else { <p>Live market feed unavailable. Recorded selling rates and product breakdowns remain available below.</p> }
<p>Product prices use their recorded metal/purity rate, net weight, making charge, stones and configured VAT. USD market quotes are not converted into AED selling rates.</p></section>`, styles: [` .market { padding:10px 18px; background:var(--cream); border-bottom:1px solid var(--line); font-size:12px; color:var(--muted); } .market>div { display:flex; justify-content:space-between; gap:16px; } button { color:var(--maroon); background:none; border:0; min-height:32px; text-decoration:underline; } p { margin:4px 0; } .quotes { flex-wrap:wrap; padding:6px 0; } small { display:block; font-size:10px; } `] })
export class MarketRatesComponent { readonly rates = inject(MarketRates); constructor() { this.rates.load(); } }
