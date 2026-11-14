// Role: Uses server cookie sessions and CSRF; a single cancellable sign-in prompt resumes pending customer intents.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { STOREFRONT_CONFIG } from '../config/storefront-config';
export interface CustomerIdentity { id: string; email: string; accountType: string; }
@Injectable({ providedIn: 'root' })
export class CustomerSession {
  private readonly http = inject(HttpClient);
  private readonly config = inject(STOREFRONT_CONFIG);
  readonly identity = signal<CustomerIdentity | null>(null);
  readonly prompt = signal(false); readonly reason = signal('Continue with your account');
  readonly error = signal(''); readonly busy = signal(false);
  private pending?: Promise<boolean>; private finish?: (value: boolean) => void;
  private revision = 0;
  get base(): string | null { return this.config.portalApiBaseUrl ?? this.config.apiBaseUrl; }
  async refresh(): Promise<boolean> {
    if (!this.base || this.config.error) { this.identity.set(null); return false; }
    const revision = this.revision;
    try { const user = await firstValueFrom(this.http.get<CustomerIdentity>(`${this.base}/api/auth/me`, { withCredentials: true }));
      if (revision !== this.revision) return false;
      if (user.accountType !== 'customer') this.identity.set(null);
      else if (this.identity()?.id !== user.id) this.identity.set(user);
      return user.accountType === 'customer';
    } catch { if (revision === this.revision) this.identity.set(null); return false; }
  }
  async require(reason = 'Sign in to continue'): Promise<boolean> {
    if (this.pending) return this.pending;
    if (await this.refresh()) return true;
    if (this.pending) return this.pending;
    this.reason.set(reason); this.error.set(''); this.prompt.set(true);
    this.pending = new Promise(resolve => this.finish = resolve); return this.pending;
  }
  cancel(): void { this.revision++; this.resolve(false); }
  private resolve(ok: boolean): void { this.prompt.set(false); this.finish?.(ok); this.finish = undefined; this.pending = undefined; }
  async mutate<T>(method: 'POST' | 'PUT' | 'DELETE', path: string, body: unknown = {}): Promise<T> {
    if (!this.base || this.config.error) throw new Error('Customer services are not configured. Browsing remains available.');
    const revision = this.revision;
    const { token } = await firstValueFrom(this.http.get<{ token: string }>(`${this.base}/api/auth/csrf`, { withCredentials: true }));
    try { return await firstValueFrom(this.http.request<T>(method, `${this.base}${path}`, { body, withCredentials: true, headers: { 'X-CSRF-TOKEN': token } })); }
    catch (error) { if ((error as { status?: number }).status === 401 && revision === this.revision) this.identity.set(null); throw error; }
  }
  async authenticate(mode: 'login' | 'register', email: string, password: string): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); const revision = ++this.revision;
    try {
      const result = await this.mutate<CustomerIdentity | { verificationRequired: true }>('POST', `/api/auth/${mode}`, { email, password });
      if (revision !== this.revision) return;
      if ('verificationRequired' in result) { this.error.set('Check your email to verify your account, then log in.'); return; }
      if (result.accountType !== 'customer') { this.error.set('Use a customer account here. Staff sign in through the retailer portal.'); return; }
      this.identity.set(result); this.resolve(true);
    } catch (error) { if (revision === this.revision) this.error.set(customerError(error)); }
    finally { this.busy.set(false); }
  }
  async logout(): Promise<void> { await this.mutate('POST', '/api/auth/logout'); this.revision++; this.identity.set(null); this.resolve(false); }
  async read<T>(path: string): Promise<T> {
    if (!this.base) throw new Error('Customer services are not configured.');
    return firstValueFrom(this.http.get<T>(`${this.base}${path}`, { withCredentials: true }));
  }
}
export function customerError(error: unknown): string {
  if (error instanceof Error && !('status' in error)) return error.message;
  const status = (error as { status?: number }).status;
  if (status === 401) return 'Please check your login details or sign in again.';
  if (status === 429) return 'Too many attempts. Please wait and try again.';
  if (status === 409 || status === 404) return 'Availability changed. Refresh and try again.';
  if (status === 400) return 'Check your details. New passwords need 12 characters, upper and lower case, a number and a symbol.';
  return 'This service is unavailable. You can keep browsing and try again later.';
}
export const customerGuard: CanActivateFn = () => inject(CustomerSession).require('Sign in to try this design on');
