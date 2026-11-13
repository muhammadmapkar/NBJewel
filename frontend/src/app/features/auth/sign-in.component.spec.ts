// Role: Ensures pending registration displays verification guidance without navigating or authenticating.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, runInInjectionContext, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { SignInComponent } from './sign-in.component';

describe('registration verification guidance', () => {
  it('stays on the page for pending verification and supports a generic resend response', () => {
    const navigateByUrl = vi.fn();
    const register = vi.fn(() => of({ verificationRequired: true, message: 'Check your email to verify your account.' }));
    const resendVerification = vi.fn(() => of({ message: 'Generic response' }));
    const injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, { provide: AuthService, useValue: { register, resendVerification } }, { provide: Router, useValue: { navigateByUrl } }, { provide: ActivatedRoute, useValue: { snapshot: { data: { register: true }, queryParamMap: { has: () => false } } } }], null as unknown as EnvironmentInjector);
    try {
      const page = runInInjectionContext(injector, () => new SignInComponent());
      page.email = 'customer@example.test'; page.password = 'password'; page.submit();
      expect(page.verificationPending()).toBe(true); expect(page.notice()).toContain('Check your email');
      expect(navigateByUrl).not.toHaveBeenCalled(); expect(page.password).toBe('');
      page.resend(); expect(resendVerification).toHaveBeenCalledWith('customer@example.test');
      expect(page.notice()).toContain('If your account needs verification');
    } finally { injector.destroy(); }
  });
});
