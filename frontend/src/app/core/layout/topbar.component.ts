// Role: Shows the fixed location, financial year, date, metal ticker, and
// administrator identity shown across the application shell. On phone widths
// it also shows a menu button that reopens the sidebar.

import { Component, output } from '@angular/core';
import { rates } from '../mock/mock-data';

@Component({
  selector: 'nb-topbar', standalone: true,
  template: `<header class="topbar"><button type="button" class="menu" (click)="menu.emit()" aria-label="Open menu">☰</button><div class="context"><span>⌖ <b>Location:</b> ABC JEWELRY T.NAGAR</span><span>◷ <b>Financial Year:</b> 2023-24</span><span>▣ <b>Date:</b> 14/03/2024</span></div><div class="ticker">{{ rates.join('  *  ') }}</div><div class="user"><span class="avatar">A</span><div><b>ADMIN</b><small>admin</small></div></div></header>`,
  styles: [`
    .menu { display:none; flex:none; width:40px; height:40px; border:1px solid #eee; border-radius:6px; background:white; color:var(--gold); font-size:20px; cursor:pointer; }
    .topbar { height:58px; display:flex; align-items:center; gap:20px; padding: 0 18px; background:white; border-bottom:1px solid #eee; color:var(--ink); }
    .context { display:flex; gap:18px; flex:none; color:var(--gold); font-size:12px; white-space:nowrap; } .context b { color:var(--gold); } .ticker { flex:1; overflow:hidden; white-space:nowrap; font-size:12px; color:#333; }
    .user { display:flex; align-items:center; gap:9px; white-space:nowrap; } .user small { display:block; } .avatar { display:grid; place-items:center; width:34px; height:34px; border-radius:50%; background:#ead9aa; color:var(--gold); font-weight:800; }
    @media (max-width: 900px) { .menu { display:block; } .topbar { gap:8px; } .context span:nth-child(2), .context span:nth-child(3), .ticker { display:none; } .context { flex:1; overflow:hidden; } }
  `]
})
export class TopbarComponent {
  readonly rates = rates;
  // Phone widths only: asks the shell to open the sidebar.
  readonly menu = output<void>();
}
