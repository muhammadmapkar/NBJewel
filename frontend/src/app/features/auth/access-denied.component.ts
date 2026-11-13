// Role: Explains customer account ERP restrictions and permits switching accounts.
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { WidgetCardComponent } from '../../shared/ui/widget-card.component';

@Component({
  selector: 'nb-access-denied', standalone: true, imports: [RouterLink, WidgetCardComponent],
  template: `<main class="account-page"><nb-widget-card title="ERP access unavailable">
    <p>This account does not have ERP staff access. Customer registration does not grant ERP access.</p>
    <p><a routerLink="/tryon">Continue to public try-on</a></p>
    <button type="button" [disabled]="busy()" (click)="signOut()">Sign out and use another account</button>
    @if (error()) { <p role="alert">{{ error() }}</p> }
  </nb-widget-card></main>`,
  styles: [`.account-page { max-width:480px; margin:60px auto; padding:16px; } a { color:var(--gold); } button { padding:10px; border:1px solid var(--gold); border-radius:4px; background:var(--button); } [role=alert] { color:var(--danger); }`]
})
export class AccessDeniedComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly busy = signal(false);
  readonly error = signal('');
  signOut(): void {
    this.busy.set(true);
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/sign-in'),
      error: (error: Error) => { this.error.set(error.message); this.busy.set(false); }
    });
  }
}
