// Role: Shows the fixed location, financial year, date, metal ticker, and
// administrator identity shown across the application shell.

import { Component } from '@angular/core';
import { rates } from '../mock/mock-data';

@Component({
  selector: 'nb-topbar', standalone: true,
  template: `<header class="topbar"><div class="context"><span>⌖ <b>Location:</b> ABC JEWELRY T.NAGAR</span><span>◷ <b>Financial Year:</b> 2023-24</span><span>▣ <b>Date:</b> 14/03/2024</span></div><div class="ticker">{{ rates.join('  *  ') }}</div><div class="user"><span class="avatar">A</span><div><b>ADMIN</b><small>admin</small></div></div></header>`,
  styles: [`
    .topbar { height:58px; display:flex; align-items:center; gap:20px; padding: 0 18px; background:white; border-bottom:1px solid #eee; color:var(--ink); }
    .context { display:flex; gap:18px; flex:none; color:var(--gold); font-size:12px; white-space:nowrap; } .context b { color:var(--gold); } .ticker { flex:1; overflow:hidden; white-space:nowrap; font-size:12px; color:#333; }
    .user { display:flex; align-items:center; gap:9px; white-space:nowrap; } .user small { display:block; } .avatar { display:grid; place-items:center; width:34px; height:34px; border-radius:50%; background:#ead9aa; color:var(--gold); font-weight:800; }
    @media (max-width: 900px) { .topbar { gap:8px; } .context span:nth-child(2), .context span:nth-child(3), .ticker { display:none; } .context { flex:1; overflow:hidden; } }
  `]
})
export class TopbarComponent { readonly rates = rates; }
