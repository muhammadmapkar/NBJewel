// Role: Keeps server-issued session identity in memory and calls cookie-based authentication endpoints.
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SessionIdentity {
  id: string;
  email: string;
  accountType: 'erp' | 'customer';
  permissions: string[];
  storeIds: number[];
  allStores: boolean;
}

export interface VerificationPending { verificationRequired: true; message: string; }
export type RegistrationResult = SessionIdentity | VerificationPending;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<SessionIdentity | null>(null);
  readonly identity = this.current.asReadonly();
  private readonly revision = signal(0);
  readonly sessionRevision = this.revision.asReadonly();
  private readonly endpoint = `${environment.apiBaseUrl.replace(/\/$/, '')}/api/auth`;
  private csrfRequest: Observable<string> | undefined;
  private csrfToken: string | undefined;

  loadSession(): Observable<SessionIdentity | null> {
    return this.http.get<SessionIdentity>(`${this.endpoint}/me`).pipe(
      tap(identity => this.current.set(identity)),
      catchError((error: { status?: number }) => {
        this.current.set(null);
        return error.status === 401 ? of(null) : throwError(() => error);
      })
    );
  }

  csrf(): Observable<string> {
    if (this.csrfToken) return of(this.csrfToken);
    if (!this.csrfRequest) {
      this.csrfRequest = this.http.get<{ token: string }>(`${this.endpoint}/csrf`).pipe(
        map(response => response.token),
        tap(token => this.csrfToken = token),
        finalize(() => this.csrfRequest = undefined),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.csrfRequest;
  }

  login(email: string, password: string): Observable<SessionIdentity> {
    return this.authenticate('login', email, password);
  }

  register(email: string, password: string): Observable<RegistrationResult> {
    return this.http.post<RegistrationResult>(`${this.endpoint}/register`, { email, password }).pipe(
      tap(result => { if (!('verificationRequired' in result)) this.acceptIdentity(result); })
    );
  }

  confirmEmail(userId: string, token: string): Observable<unknown> {
    return this.http.post(`${this.endpoint}/confirm-email`, { userId, token });
  }

  resendVerification(email: string): Observable<unknown> {
    return this.http.post(`${this.endpoint}/resend-verification`, { email });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.endpoint}/logout`, {}).pipe(tap(() => this.clearSession()));
  }

  clearSession(): void {
    this.revision.update(value => value + 1);
    this.current.set(null);
    this.csrfToken = undefined;
  }

  private acceptIdentity(identity: SessionIdentity): void {
    this.revision.update(value => value + 1);
    this.current.set(identity);
    this.csrfToken = undefined;
  }

  private authenticate(action: 'login', email: string, password: string): Observable<SessionIdentity> {
    return this.http.post<SessionIdentity>(`${this.endpoint}/${action}`, { email, password }).pipe(
      tap(identity => this.acceptIdentity(identity))
    );
  }
}
