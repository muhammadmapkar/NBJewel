// Role: Verifies token bounds, fragment removal, explicit confirmation and CSRF-protected requests.
import '@angular/compiler';
import { DOCUMENT } from '@angular/common';
import { createEnvironmentInjector, EnvironmentInjector, runInInjectionContext, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { authInterceptor } from '../../core/auth/auth.interceptor';
import { environment } from '../../../environments/environment';
import { parseVerificationFragment, VerifyEmailComponent } from './verify-email.component';

describe('email verification', () => {
  let injector: EnvironmentInjector;
  let http: HttpTestingController;
  const browser = { location: { hash: '', pathname: '/verify-email', search: '' }, history: { state: null, replaceState: vi.fn() } };
  const api = `${environment.apiBaseUrl}/api/auth`;
  beforeEach(() => {
    browser.location.hash = ''; browser.history.replaceState.mockClear();
    injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, AuthService, { provide: DOCUMENT, useValue: { defaultView: browser } }, { provide: Router, useValue: { url: '/verify-email', navigate: vi.fn() } }, provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()], null as unknown as EnvironmentInjector);
    http = injector.get(HttpTestingController);
  });
  afterEach(() => { http.verify(); injector.destroy(); });
  const component = () => runInInjectionContext(injector, () => new VerifyEmailComponent());
  it.each(['', '#userId=user', '#token=token', '#userId=user&token=', '#userId=user&token=%ZZ', '#userId=one&userId=two&token=token', `#userId=${'x'.repeat(129)}&token=token`, `#userId=user&token=${'x'.repeat(8193)}`])('sends no request for a malformed verification link', fragment => {
    browser.location.hash = fragment;
    const page = component(); page.confirm();
    expect(page.validLink).toBe(false); http.expectNone(() => true);
    if (fragment) expect(browser.history.replaceState).toHaveBeenCalledWith(null, '', '/verify-email');
  });
  it('removes the fragment immediately and waits for explicit confirmation before posting', () => {
    browser.location.hash = '#userId=user&token=abc%2B123%2F%3D';
    const page = component(); expect(page.validLink).toBe(true);
    expect(browser.history.replaceState).toHaveBeenCalledWith(null, '', '/verify-email');
    http.expectNone(() => true);
    page.confirm();
    http.expectOne(`${api}/csrf`).flush({ token: 'csrf' });
    const request = http.expectOne(`${api}/confirm-email`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ userId: 'user', token: 'abc+123/=' });
    expect(request.request.headers.get('X-CSRF-TOKEN')).toBe('csrf');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ message: 'Verified' });
    expect(page.verified()).toBe(true); expect(injector.get(AuthService).identity()).toBeNull();
    page.confirm(); http.expectNone(() => true);
  });
  it('accepts the maximum decoded token and user ID lengths', () => {
    expect(parseVerificationFragment(`#userId=${'u'.repeat(128)}&token=${'t'.repeat(8192)}`)).not.toBeNull();
  });
});
