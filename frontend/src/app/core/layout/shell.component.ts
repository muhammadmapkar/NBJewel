// Role: Wraps every page with the responsive sidebar, top bar, chat button and
// chat drawer, and the routed page outlet. It owns the sidebar open or collapsed
// state and remembers it for the browser session (sessionStorage). With nothing
// remembered, the sidebar starts open on desktop and closed on phone widths.

import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { ChatPanelComponent } from '../../features/chat/chat-panel.component';
import { SidebarComponent } from './sidebar.component';
import { TopbarComponent } from './topbar.component';

const STORAGE_KEY = 'nb.sidebarCollapsed';
const PHONE_QUERY = '(max-width: 900px)';

// Storage can be missing or blocked (private window), so every access is guarded.
function isPhone(): boolean {
  try { return window.matchMedia(PHONE_QUERY).matches; } catch { return false; }
}

function readCollapsed(): boolean {
  try {
    const saved = sessionStorage.getItem(STORAGE_KEY);
    if (saved === 'true' || saved === 'false') return saved === 'true';
  } catch { /* storage unavailable, use the default */ }
  return isPhone();
}

@Component({
  selector: 'nb-shell', standalone: true, imports: [RouterOutlet, SidebarComponent, TopbarComponent, ChatPanelComponent],
  template: `
    <div class="app-shell" [class.sidebar-collapsed]="collapsed()">
      <nb-sidebar [collapsed]="collapsed()" (collapseChange)="setCollapsed($event)" />
      @if (!collapsed()) { <div class="scrim" (click)="setCollapsed(true)" aria-hidden="true"></div> }
      <main class="main-area">
        <nb-topbar (menu)="setCollapsed(false)" />
        <section class="page-content"><router-outlet /></section>
      </main>
      <button class="chat-button" (click)="chatOpen.set(!chatOpen())" aria-label="Open chat">◔</button>
      <nb-chat-panel [open]="chatOpen()" (closed)="chatOpen.set(false)" />
    </div>
  `,
  styles: [`
    .app-shell { min-height: 100vh; display: flex; }
    .main-area { flex: 1; min-width: 0; margin-left: var(--sidebar-width); transition: margin .2s; }
    .sidebar-collapsed .main-area { margin-left: 74px; }
    .page-content { min-height: calc(100vh - 58px); padding: 20px; background: var(--page); }
    .scrim { display: none; }
    .chat-button { position: fixed; right: 26px; bottom: 24px; z-index: 30; width: 56px; height: 56px; border: 0; border-radius: 50%; color: white; background: var(--gold); font-size: 28px; box-shadow: 0 5px 18px #8b690055; }
    @media (max-width: 900px) { .main-area, .sidebar-collapsed .main-area { margin-left: 0; } .page-content { padding: 14px; } .scrim { display: block; position: fixed; inset: 0; z-index: 15; background: #0004; } }
  `]
})
export class ShellComponent {
  private readonly router = inject(Router);

  readonly collapsed = signal(readCollapsed());
  readonly chatOpen = signal(false);

  constructor() {
    // On a phone the open sidebar covers the page, so close it after choosing a page.
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => { if (isPhone() && !this.collapsed()) this.setCollapsed(true); });
  }

  setCollapsed(value: boolean): void {
    this.collapsed.set(value);
    try { sessionStorage.setItem(STORAGE_KEY, String(value)); } catch { /* remembering is optional */ }
  }
}
