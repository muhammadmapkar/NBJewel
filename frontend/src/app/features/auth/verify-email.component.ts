// Role: Consumes an email verification fragment in memory and confirms it only after an explicit user action.
import { DOCUMENT } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';
import { ACCOUNT_PAGE_STYLES } from './account-page.styles';

export interface VerificationLink { userId: string; token: string; }
export function parseVerificationFragment(fragment: string): VerificationLink | null {
  if (!fragment.startsWith('#') || fragment.length > 26000) return null;
  try {
    decodeURIComponent(fragment.slice(1));
    const values = new URLSearchParams(fragment.slice(1));
    if (values.getAll('userId').length !== 1 || values.getAll('token').length !== 1) return null;
    const userId = values.get('userId') ?? '';
    const token = values.get('token') ?? '';
    if (!userId || userId.length > 128 || !token.trim() || token.length > 8192 || /[\s\u0000-\u001f\u007f]/.test(userId) || /[\u0000-\u001f\u007f]/.test(token)) return null;
    return { userId, token };
  } catch { return null; }
}

@Component({
  selector: 'nb-verify-email', standalone: true, imports: [RouterLink, WidgetCardComponent],
  template: `<main class="account-page"><nb-widget-card title="Verify your email">
    @if (verified()) {
      <p role="status">Your email is verified. You can now sign in.</p>
    } @else if (validLink) {
      <p>Confirm your email address to finish setting up your account.</p>
      <button type="button" [disabled]="busy()" (click)="confirm()">{{ busy() ? 'Please wait...' : 'Verify email' }}</button>
    } @else {
      <p class="error" role="alert">This verification link is invalid or incomplete. Request a new email from the registration page.</p>
    }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    <p><a routerLink="/sign-in">Back to sign in</a></p>
    <a routerLink="/register">Customer registration</a>
  </nb-widget-card></main>`,
  styles: [ACCOUNT_PAGE_STYLES]
})
export class VerifyEmailComponent {
  private readonly auth = inject(AuthService);
  private readonly document = inject(DOCUMENT);
  private link: VerificationLink | null;
  readonly validLink: boolean;
  readonly busy = signal(false);
  readonly verified = signal(false);
  readonly error = signal('');

  constructor() {
    const browser = this.document.defaultView;
    this.link = parseVerificationFragment(browser?.location.hash ?? '');
    this.validLink = this.link !== null;
    if (browser?.location.hash) browser.history.replaceState(browser.history.state, '', `${browser.location.pathname}${browser.location.search}`);
  }

  confirm(): void {
    if (!this.link || this.busy() || this.verified()) return;
    this.busy.set(true); this.error.set('');
    this.auth.confirmEmail(this.link.userId, this.link.token).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => { this.link = null; this.verified.set(true); },
      error: () => this.error.set('Could not verify this link. It may have expired. Request a new verification email and try again.')
    });
  }
}
