// Role: Confirms an email token from the URL fragment without treating verification as an authenticated session.
import { Component, inject, signal } from '@angular/core';
import { CustomerSession, customerError } from '../customer/customer-session';
import { SiteHeaderComponent } from '../ui/site-header.component';
@Component({ standalone: true, imports: [SiteHeaderComponent], template: `<ct-site-header /><main class="portal-page"><h1 class="serif">Verify your email</h1><p role="status">{{ message() }}</p><button class="btn" (click)="confirm()">Confirm email</button><button class="btn ghost" (click)="session.require()">Log in</button></main>` })
export class VerifyEmailPage {
  readonly session = inject(CustomerSession); readonly message = signal('Confirm your email, then log in to continue.');
  private readonly details = new URLSearchParams(location.hash.slice(1));
  constructor() { history.replaceState(null, '', location.pathname); }
  async confirm(): Promise<void> { try { await this.session.mutate('POST', '/api/auth/confirm-email', { userId: this.details.get('userId'), token: this.details.get('token') }); this.message.set('Email verified. You can now log in.'); } catch (error) { this.message.set(customerError(error)); } }
}
