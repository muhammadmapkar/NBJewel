// Role: Printable item tag for one item. It shows the code, name, metal, purity,
// net weight, price and a QR code. The QR holds the item code only (no price),
// so a printed tag never carries an out-of-date price in the code. Print uses the
// browser print dialog, and a print-only rule in styles.css hides the shell.

import { DecimalPipe } from '@angular/common';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import * as QRCode from 'qrcode';
import { ItemDataService } from '../../core/data/item-data.service';
import { Item, PriceBreakdown } from '../../core/models';

const PRINT_CLASS = 'print-tag';

@Component({
  selector: 'nb-item-tag', standalone: true, imports: [DecimalPipe, RouterLink],
  template: `
    <div class="bar no-print">
      <a [routerLink]="['/catalog', id]">← Back to item</a>
      <button type="button" (click)="print()" [disabled]="!item()">⎙ Print</button>
    </div>
    @if (error()) { <p class="note">{{ error() }}</p> }
    @else if (item(); as it) {
      <article class="tag" aria-label="Item tag">
        <div class="brand">NB JEWEL</div>
        <div class="body">
          <div class="info">
            <b class="code">{{ it.code }}</b>
            <span class="name">{{ it.name }}</span>
            <span>{{ it.metalType }} {{ it.purity }}, design {{ it.designCode }}</span>
            <span>Net weight {{ it.netWt | number:'1.3-3' }} g</span>
            @if (price(); as p) { <b class="price">{{ p.totalPrice | number:'1.2-2' }} {{ p.currency }}</b> }
            @else { <span class="note">Price unavailable</span> }
          </div>
          @if (qr()) { <img class="qr" [src]="qr()" [alt]="'QR code for ' + it.code" width="120" height="120" /> }
        </div>
      </article>
    } @else { <p class="note">Loading...</p> }
  `,
  styles: [`
    .bar { display:flex; justify-content:space-between; align-items:center; max-width:420px; margin-bottom:12px; } .bar a { color:var(--gold); text-decoration:none; } .bar button { border:0; border-radius:6px; background:var(--button); color:#3e3100; padding:9px 16px; font-weight:700; cursor:pointer; } .bar button:disabled { opacity:.5; cursor:default; }
    .tag { max-width:420px; background:white; border:1px solid var(--gold); border-radius:8px; overflow:hidden; } .brand { padding:8px 14px; background:var(--widget); color:var(--gold); font-weight:800; letter-spacing:3px; font-size:13px; }
    .body { display:flex; justify-content:space-between; align-items:center; gap:12px; padding:14px; } .info { display:grid; gap:4px; font-size:13px; } .code { font-size:20px; } .name { font-weight:600; } .price { margin-top:6px; font-size:18px; color:var(--gold); }
    .note { color:var(--muted); font-size:13px; }
  `]
})
export class ItemTagComponent implements OnDestroy {
  private readonly data = inject(ItemDataService);
  readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));
  readonly item = signal<Item | null>(null);
  readonly price = signal<PriceBreakdown | null>(null);
  readonly qr = signal('');
  readonly error = signal('');

  constructor() {
    document.body.classList.add(PRINT_CLASS);
    this.data.getItem(this.id).subscribe({
      next: item => {
        this.item.set(item);
        QRCode.toDataURL(item.code, { margin: 1, width: 240 }).then(url => this.qr.set(url)).catch(() => this.qr.set(''));
      },
      error: () => this.error.set('Item not found.')
    });
    // Price can legitimately be missing (no metal rate), so a failure is not an error for the tag.
    this.data.getPrice(this.id).subscribe({ next: p => this.price.set(p), error: () => this.price.set(null) });
  }

  print(): void { window.print(); }
  ngOnDestroy(): void { document.body.classList.remove(PRINT_CLASS); }
}
