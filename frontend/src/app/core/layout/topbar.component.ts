// Role: Shows the fixed location, financial year, date, metal ticker, and
// administrator identity shown across the application shell. On phone widths,
// while the sidebar is closed, it shows the NB Jewel logo as a button that opens it.

import { Component, input, output } from '@angular/core';
import { rates } from '../mock/mock-data';

@Component({
  selector: 'nb-topbar', standalone: true,
  template: `<header class="topbar">@if (collapsed()) { <button type="button" class="menu" (click)="menu.emit()" title="Toggle menu" aria-label="Toggle menu"><span>NB <small>JEWEL</small></span></button> }<div class="context"><span>⌖ <b>Location:</b> ABC JEWELRY T.NAGAR</span><span>◷ <b>Financial Year:</b> 2023-24</span><span>▣ <b>Date:</b> 14/03/2024</span></div><div class="ticker">{{ rates.join('  *  ') }}</div><div class="user"><span class="avatar">A</span><div><b>ADMIN</b><small>admin</small></div></div></header>`,
  styles: [`
    .menu { display:none; flex:none; border:0; background:transparent; color:var(--gold); padding:4px 2px; font:inherit; font-size:20px; font-weight:800; line-height:.8; text-align:left; cursor:pointer; } .menu small { display:block; font-size:6px; letter-spacing:3px; } .menu:hover { opacity:.8; } .menu:focus-visible { outline:2px solid var(--gold); outline-offset:3px; }
    .topbar { height:58px; display:flex; align-items:center; gap:20px; padding: 0 18px; background:white; border-bottom:1px solid #eee; color:var(--ink); }
    .context { display:flex; gap:18px; flex:none; color:var(--gold); font-size:12px; white-space:nowrap; } .context b { color:var(--gold); } .ticker { flex:1; overflow:hidden; white-space:nowrap; font-size:12px; color:#333; }
    .user { display:flex; align-items:center; gap:9px; white-space:nowrap; } .user small { display:block; } .avatar { display:grid; place-items:center; width:34px; height:34px; border-radius:50%; background:#ead9aa; color:var(--gold); font-weight:800; }
    @media (max-width: 900px) { .menu { display:block; } .topbar { gap:8px; } .context span:nth-child(2), .context span:nth-child(3), .ticker { display:none; } .context { flex:1; overflow:hidden; } }
  `]
})
export class TopbarComponent {
  readonly rates = rates;
  // True while the sidebar is closed. The logo button only shows then, on phone widths.
  readonly collapsed = input(false);
  // Phone widths only: asks the shell to open the sidebar.
  readonly menu = output<void>();
}
