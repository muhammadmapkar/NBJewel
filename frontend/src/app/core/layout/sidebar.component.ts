// Role: Renders the visual navigation menu and its allowed collapse and
// submenu interactions without performing navigation logic.

import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { menuItems, MenuItem } from '../mock/mock-data';

@Component({
  selector: 'nb-sidebar', standalone: true, imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="sidebar" [class.collapsed]="collapsed">
      <div class="brand"><span class="brand-name">NB <small>JEWEL</small></span><button (click)="collapseChange.emit(!collapsed)">↤</button></div>
      <nav>
        @for (item of items; track item.label) {
          <div class="nav-group">
            <a class="nav-item" [routerLink]="item.route || null" routerLinkActive="active" [class.has-children]="!!item.children" (click)="item.children && toggle(item.label)">
              <span class="nav-icon">{{ item.icon }}</span><span class="nav-label">{{ item.label }}</span><span class="chevron">{{ item.children ? (expanded() === item.label ? '⌄' : '›') : (item.route ? '' : '›') }}</span>
            </a>
            @if (item.children && expanded() === item.label) {
              <div class="submenu">
                @for (child of item.children; track child) {
                  <a [routerLink]="child === 'Scheme Master' ? '/scheme-master' : child === 'Chit Master' ? '/chit-master' : null" [class.active]="(child === 'Scheme Master' && currentPage === 'scheme') || (child === 'Chit Master' && currentPage === 'chit')">{{ child }}<span>›</span></a>
                }
              </div>
            }
          </div>
        }
      </nav>
      <div class="sidebar-bottom"><div class="font-tools"><button>A−</button><span>•••••••</span><button>A+</button></div><button class="logout">↻ <span class="nav-label">Logout</span></button></div>
    </aside>
  `,
  styles: [`
    .sidebar { position: fixed; inset: 0 auto 0 0; z-index: 20; width: var(--sidebar-width); padding: 14px 10px; color: white; background: var(--sidebar); transition: width .2s; overflow-y: auto; }
    .sidebar.collapsed { width: 74px; } .brand { height: 68px; display:flex; justify-content:space-between; align-items:center; padding: 0 12px; border-bottom:1px solid #fff4; margin-bottom: 14px; }
    .brand-name { font-size: 26px; font-weight: 800; line-height: .7; } .brand-name small { display:block; font-size:7px; letter-spacing: 3px; margin-left: 3px; }
    .brand button { border:0; background:transparent; color:white; font-size:20px; } .collapsed .brand { justify-content:center; } .collapsed .brand button { display:none; }
    .nav-item, .logout { width:100%; display:flex; align-items:center; gap:12px; min-height:45px; padding: 0 14px; border-radius:5px; color:white; text-decoration:none; font-weight:600; }
    .nav-item:hover, .nav-item.active { color: var(--gold); background: var(--pale); } .nav-icon { width:18px; text-align:center; font-size:16px; } .nav-label { white-space:nowrap; overflow:hidden; }
    .chevron { margin-left:auto; font-size:19px; } .submenu { margin-left: 27px; border-left: 1px solid #fff6; } .submenu a { display:flex; justify-content:space-between; color:white; padding: 10px 10px; text-decoration:none; font-size:13px; border-radius: 4px; }
    .submenu a:hover, .submenu a.active { background: var(--pale); color:var(--gold); } .collapsed .nav-label, .collapsed .chevron, .collapsed .submenu { display:none; } .collapsed .nav-item { justify-content:center; padding:0; }
    .sidebar-bottom { margin-top: 26px; } .font-tools { display:flex; justify-content:space-around; align-items:center; font-size:16px; margin: 15px 0 28px; } .font-tools span { font-size:13px; letter-spacing:2px; }
    .font-tools button, .logout { border:0; background:transparent; color:white; } .logout { text-align:left; } .collapsed .logout { justify-content:center; padding:0; }
    @media (max-width: 900px) { .sidebar { transform:translateX(-100%); } .sidebar:not(.collapsed) { transform:translateX(0); box-shadow: 8px 0 25px #0004; } .sidebar.collapsed { transform:translateX(-100%); } }
  `]
})
export class SidebarComponent {
  @Input() collapsed = false;
  @Output() collapseChange = new EventEmitter<boolean>();
  readonly items: MenuItem[] = menuItems;
  readonly expanded = signal('Gold Scheme');
  currentPage = '';
  toggle(label: string): void { this.expanded.set(this.expanded() === label ? '' : label); }
}
