// Role: Wraps every page with the responsive sidebar, top bar, chat button and
// chat drawer, and the routed page outlet.

import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ChatPanelComponent } from '../../features/chat/chat-panel.component';
import { SidebarComponent } from './sidebar.component';
import { TopbarComponent } from './topbar.component';

@Component({
  selector: 'nb-shell', standalone: true, imports: [RouterOutlet, SidebarComponent, TopbarComponent, ChatPanelComponent],
  template: `
    <div class="app-shell" [class.sidebar-collapsed]="collapsed()">
      <nb-sidebar [collapsed]="collapsed()" (collapseChange)="collapsed.set($event)" />
      <main class="main-area">
        <nb-topbar />
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
    .chat-button { position: fixed; right: 26px; bottom: 24px; z-index: 30; width: 56px; height: 56px; border: 0; border-radius: 50%; color: white; background: var(--gold); font-size: 28px; box-shadow: 0 5px 18px #8b690055; }
    @media (max-width: 900px) { .main-area, .sidebar-collapsed .main-area { margin-left: 0; } .page-content { padding: 14px; } }
  `]
})
export class ShellComponent {
  readonly collapsed = signal(false);
  readonly chatOpen = signal(false);
}
