// Role: Wraps every page with the responsive sidebar, top bar, chat button,
// and the routed page outlet.

import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './sidebar.component';
import { TopbarComponent } from './topbar.component';

@Component({
  selector: 'nb-shell', standalone: true, imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  template: `
    <div class="app-shell" [class.sidebar-collapsed]="collapsed()">
      <nb-sidebar [collapsed]="collapsed()" (collapseChange)="collapsed.set($event)" />
      <main class="main-area">
        <nb-topbar />
        <section class="page-content"><router-outlet /></section>
      </main>
      <button class="chat-button" (click)="chatOpen.set(true)" aria-label="Open chat">◔</button>
      @if (chatOpen()) {
        <aside class="chat-drawer">
          <div class="drawer-title"><b>Assistant</b><button (click)="chatOpen.set(false)">×</button></div>
          <div class="empty-chat">Your AI assistant will appear here later.</div>
        </aside>
      }
    </div>
  `,
  styles: [`
    .app-shell { min-height: 100vh; display: flex; }
    .main-area { flex: 1; min-width: 0; margin-left: var(--sidebar-width); transition: margin .2s; }
    .sidebar-collapsed .main-area { margin-left: 74px; }
    .page-content { min-height: calc(100vh - 58px); padding: 20px; background: var(--page); }
    .chat-button { position: fixed; right: 26px; bottom: 24px; z-index: 30; width: 56px; height: 56px; border: 0; border-radius: 50%; color: white; background: var(--gold); font-size: 28px; box-shadow: 0 5px 18px #8b690055; }
    .chat-drawer { position: fixed; z-index: 40; right: 20px; bottom: 90px; width: 310px; height: 300px; background: white; border: 1px solid var(--line); border-radius: 8px; box-shadow: 0 12px 36px #0002; }
    .drawer-title { display:flex; justify-content:space-between; padding: 16px; border-bottom: 1px solid var(--line); color: var(--gold); }
    .drawer-title button { border: 0; background: transparent; font-size: 22px; color: var(--gold); }
    .empty-chat { display:grid; place-items:center; height: calc(100% - 58px); color: var(--muted); padding: 25px; text-align:center; }
    @media (max-width: 900px) { .main-area, .sidebar-collapsed .main-area { margin-left: 0; } .page-content { padding: 14px; } }
  `]
})
export class ShellComponent {
  readonly collapsed = signal(false);
  readonly chatOpen = signal(false);
}
