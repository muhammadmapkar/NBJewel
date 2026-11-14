// Role: Accessible native sign-in/up dialog shared by every gated action; cancellation never completes an intent.
import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerSession } from './customer-session';
@Component({ selector: 'ct-auth-dialog', standalone: true, imports: [FormsModule], template: `
<dialog #dialog (cancel)="cancel($event)" aria-labelledby="auth-title">
  <button class="close" type="button" (click)="session.cancel()" aria-label="Close sign in">×</button>
  <p class="brand">NB JEWEL</p><h2 class="serif" id="auth-title">{{ session.reason() }}</h2>
  <p class="muted">Save your favourites, try on a design and plan your visit.</p>
  <div class="tabs"><button class="chip" [class.on]="mode() === 'login'" (click)="mode.set('login')">Log in</button><button class="chip" [class.on]="mode() === 'register'" (click)="mode.set('register')">Sign up</button></div>
  <form (ngSubmit)="submit()">
    <label>Email<input autofocus name="email" type="email" [(ngModel)]="email" required autocomplete="email" maxlength="254" /></label>
    <label>Password<input name="password" type="password" [(ngModel)]="password" required [attr.autocomplete]="mode() === 'login' ? 'current-password' : 'new-password'" maxlength="128" /></label>
    @if (mode() === 'register') { <p class="muted small">Use 12+ characters with upper and lower case, a number and a symbol.</p> }
    @if (session.error()) { <p role="alert">{{ session.error() }}</p> }
    <button class="btn" type="submit" [disabled]="session.busy()">{{ session.busy() ? 'Please wait...' : mode() === 'login' ? 'Log in and continue' : 'Create account and continue' }}</button>
  </form>
</dialog>`, styles: [`
dialog { border:1px solid var(--line); border-radius:24px; width:min(440px,calc(100% - 24px)); max-height:90dvh; padding:28px; background:var(--ivory); color:var(--ink); box-shadow:var(--shadow); } dialog::backdrop { background:rgba(42,13,18,.55); backdrop-filter:blur(3px); } h2 { font-size:30px; margin:12px 0; } .brand { color:var(--maroon); } .close { float:right; border:0; background:none; font-size:30px; min-width:44px; min-height:44px; } .tabs { display:flex; gap:8px; margin:20px 0; } form,label { display:grid; gap:10px; } label { margin-bottom:10px; font-size:14px; } input { min-height:48px; border:1px solid var(--line); border-radius:10px; padding:10px; font:inherit; width:100%; } .small { font-size:12px; } [role=alert] { color:var(--maroon); } @media(max-width:767px) { dialog { margin:auto auto 0; width:100%; border-radius:24px 24px 0 0; padding-bottom:calc(24px + env(safe-area-inset-bottom)); } }
`] })
export class AuthDialogComponent {
  readonly session = inject(CustomerSession); readonly mode = signal<'login' | 'register'>('login');
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog'); email = ''; password = '';
  constructor() { effect(() => { const el = this.dialog()?.nativeElement; if (!el) return; if (this.session.prompt() && !el.open) el.showModal(); else if (!this.session.prompt() && el.open) { el.close(); this.password = ''; } }); }
  cancel(event: Event): void { event.preventDefault(); this.session.cancel(); }
  async submit(): Promise<void> { await this.session.authenticate(this.mode(), this.email.trim(), this.password); this.password = ''; }
}
