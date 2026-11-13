// Role: Provides sign-in and customer registration outside the protected ERP shell.
import { ACCOUNT_PAGE_STYLES } from './account-page.styles';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { safeErpReturnUrl } from '../../core/auth/auth-policy';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';

@Component({
  selector: 'nb-sign-in', standalone: true, imports: [FormsModule, RouterLink, WidgetCardComponent],
  template: `<main class="account-page"><nb-widget-card [title]="registering ? 'Customer registration' : 'NB Jewel sign in'">
    <p>{{ registering ? 'Create a customer account for customer services. ERP access requires an assigned staff account.' : 'Sign in with your assigned staff account to open the ERP.' }}</p>
    @if (!verificationPending()) { <form #form="ngForm" (ngSubmit)="submit()">
      <label>Email<input type="email" name="email" [(ngModel)]="email" required email autocomplete="username" /></label>
      <label>Password<input type="password" name="password" [(ngModel)]="password" required [attr.autocomplete]="registering ? 'new-password' : 'current-password'" /></label>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <button type="submit" [disabled]="form.invalid || busy()">{{ busy() ? 'Please wait...' : registering ? 'Create customer account' : 'Sign in' }}</button>
    </form> }
    @if (verificationPending()) {
      <p role="status">{{ notice() }}</p>
      <button type="button" [disabled]="busy()" (click)="resend()">Resend verification email</button>
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    }
    <p><a [routerLink]="registering ? '/sign-in' : '/register'">{{ registering ? 'Back to sign in' : 'Create a customer account' }}</a></p>
    <a routerLink="/tryon">Open public try-on</a>
  </nb-widget-card></main>`,
  styles: [ACCOUNT_PAGE_STYLES]
})
export class SignInComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  readonly registering = this.route.snapshot.data['register'] === true;
  readonly busy = signal(false);
  readonly verificationPending = signal(false);
  readonly notice = signal('');
  private pendingEmail = '';
  readonly error = signal(this.route.snapshot.queryParamMap.has('sessionError') ? 'Could not verify your session. Please try again.' : '');
  email = '';
  password = '';
  resend(): void {
    if (this.busy() || !this.pendingEmail || this.pendingEmail.length > 254) return;
    this.busy.set(true); this.error.set('');
    this.auth.resendVerification(this.pendingEmail).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => this.notice.set('If your account needs verification, an email will be sent. Check your inbox.'),
      error: () => this.error.set('Could not request a verification email. Please try again.')
    });
  }

  submit(): void {
    if (this.busy() || this.verificationPending() || !this.email.trim() || !this.password) return;
    const requestEmail = this.email.trim();
    this.busy.set(true);
    this.error.set('');
    const request = this.registering ? this.auth.register(requestEmail, this.password) : this.auth.login(requestEmail, this.password);
    request.pipe(finalize(() => { this.busy.set(false); this.password = ''; })).subscribe({
      next: result => {
        if ('verificationRequired' in result) {
          this.pendingEmail = requestEmail;
          this.verificationPending.set(true);
          this.notice.set('Check your email to verify your account.');
          return;
        }
        void this.router.navigateByUrl(result.accountType === 'erp' ? safeErpReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')) : '/access-denied');
      },
      error: (error: Error) => this.error.set(error.message)
    });
  }
}
