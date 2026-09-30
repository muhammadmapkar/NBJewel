// Role: Renders the financial-year selection cards and local visual selection.

import { Component, signal } from '@angular/core';
import { years } from '../../core/mock/mock-data';

@Component({
  selector: 'nb-financial-year', standalone: true,
  template: `<div class="page-head"><h1>Change Financial Year</h1><input placeholder="⌕  Search"></div><div class="years">@for (year of yearList; track year) {<button [class.selected]="selected() === year" (click)="selected.set(year)"><span>▣</span><small>Financial Year:</small><b>{{ year }}</b></button>}</div>`,
  styles: [`.page-head { display:flex; justify-content:space-between; align-items:center; margin-bottom:28px; } h1 { font-size:24px; margin:0; } .page-head input { width:215px; padding:9px; border:1px solid #d4d4d4; border-radius:6px; } .years { display:grid; grid-template-columns:repeat(7,1fr); gap:28px; } .years button { min-height:112px; border:2px solid #ebc33c; border-radius:7px; background:white; display:grid; place-content:center; gap:7px; text-align:center; } .years button.selected { background:var(--button); color:white; border-color:var(--button); } .years span { font-size:20px; } .years small { font-size:11px; } .years b { font-size:21px; } @media (max-width:1050px) { .years { grid-template-columns:repeat(4,1fr); } } @media (max-width:600px) { .page-head { display:block; } .page-head input { margin-top:14px; width:100%; } .years { grid-template-columns:repeat(2,1fr); } }`]
})
export class FinancialYearComponent { readonly yearList = years; readonly selected = signal('2023 - 2024'); }
