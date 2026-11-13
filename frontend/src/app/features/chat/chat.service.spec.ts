// Role: Verifies server conversation ownership, namespace separation and retry requests.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, signal, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService, SessionIdentity } from '../../core/auth/auth.service';
import { ChatService } from './chat.service';
describe('owned conversations', () => {
  let service: ChatService;
  let http: HttpTestingController;
  let injector: EnvironmentInjector;
  const identity = signal<SessionIdentity | null>(null);
  const sessionRevision = signal(0);
  const base = `${environment.apiBaseUrl}/api/ai`;
  const reply = { answer: 'Answer', sources: [], refused: false, traceId: 'trace' };
  beforeEach(() => {
    identity.set(null); sessionRevision.set(0);
    injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, ChatService, { provide: AuthService, useValue: { identity, sessionRevision } }, provideHttpClient(), provideHttpClientTesting()], null as unknown as EnvironmentInjector);
    service = injector.get(ChatService); http = injector.get(HttpTestingController);
  });
  afterEach(() => { http.verify(); injector.destroy(); });
  const create = (assistant: string, id: string) => {
    const request = http.expectOne(`${base}/${assistant}/conversations`);
    expect(request.request.body).toEqual({});
    request.flush({ id, assistantId: assistant, createdAt: '', expiresAt: '' });
  };
  it('separates assistant namespaces and sends no client history', async () => {
    for (const assistant of ['erp', 'website'] as const) {
      const result = firstValueFrom(service.ask(assistant, 'Question', 'turn'));
      create(assistant, assistant);
      const request = http.expectOne(`${base}/${assistant}/conversations/${assistant}/messages`);
      expect(request.request.body).toEqual({ text: 'Question', clientRequestId: 'turn' });
      request.flush(reply); await result;
    }
  });
  it('shares concurrent conversation creation', async () => {
    const first = firstValueFrom(service.ask('website', 'One', 'one'));
    const second = firstValueFrom(service.ask('website', 'Two', 'two'));
    create('website', 'shared');
    const requests = http.match(`${base}/website/conversations/shared/messages`);
    expect(requests).toHaveLength(2);
    requests.forEach(request => request.flush(reply));
    await Promise.all([first, second]);
  });
  it('keeps retry IDs and replaces a forbidden conversation on explicit retry', async () => {
    for (const status of [503, 403]) {
      const result = firstValueFrom(service.ask('website', 'Question', 'retry'));
      if (status === 503) create('website', 'old');
      const rejected = expect(result).rejects.toMatchObject({ status });
      const request = http.expectOne(`${base}/website/conversations/old/messages`);
      expect(request.request.body.clientRequestId).toBe('retry');
      request.flush({}, { status, statusText: 'Failed' }); await rejected;
    }
    const retry = firstValueFrom(service.ask('website', 'Question', 'retry'));
    create('website', 'fresh');
    http.expectOne(`${base}/website/conversations/fresh/messages`).flush(reply); await retry;
  });
  it('deletes on clear and resets IDs on identity changes', async () => {
    const first = firstValueFrom(service.ask('website', 'Question', 'first'));
    create('website', 'old'); http.expectOne(`${base}/website/conversations/old/messages`).flush(reply); await first;
    const clearing = firstValueFrom(service.clearChat('website'));
    const deletion = http.expectOne(`${base}/website/conversations/old`);
    expect(deletion.request.method).toBe('DELETE'); deletion.flush(null); await clearing;
    const next = firstValueFrom(service.ask('website', 'New', 'next'));
    create('website', 'new'); http.expectOne(`${base}/website/conversations/new/messages`).flush(reply); await next;
    identity.set({ id: 'customer', email: '', accountType: 'customer', permissions: [], storeIds: [], allStores: false });
    const customer = firstValueFrom(service.ask('website', 'Customer', 'customer'));
    create('website', 'customer'); http.expectOne(`${base}/website/conversations/customer/messages`).flush(reply); await customer;
    identity.set(null);
    const anonymous = firstValueFrom(service.ask('website', 'Anonymous', 'anon'));
    create('website', 'anon'); http.expectOne(`${base}/website/conversations/anon/messages`).flush(reply); await anonymous;
  });
  it('rejects late creation from a previous identity without reusing its ID', async () => {
    const old = firstValueFrom(service.ask('website', 'Old owner', 'old'));
    const oldRejected = expect(old).rejects.toThrow('Conversation identity changed.');
    const oldCreate = http.expectOne(`${base}/website/conversations`);
    identity.set({ id: 'new', email: '', accountType: 'customer', permissions: [], storeIds: [], allStores: false });
    const current = firstValueFrom(service.ask('website', 'New owner', 'new'));
    create('website', 'current');
    oldCreate.flush({ id: 'previous', assistantId: 'website', createdAt: '', expiresAt: '' });
    http.expectNone(`${base}/website/conversations/previous/messages`);
    http.expectOne(`${base}/website/conversations/current/messages`).flush(reply);
    await oldRejected; await current;
  });

  it('resets same-user conversations when logout and login occur before effects flush', async () => {
    const user = { id: 'same', email: '', accountType: 'customer' as const, permissions: [], storeIds: [], allStores: false };
    identity.set(user);
    const first = firstValueFrom(service.ask('website', 'Old session', 'old'));
    const rejected = expect(first).rejects.toThrow('Conversation identity changed.');
    create('website', 'old-session');
    const oldMessage = http.expectOne(`${base}/website/conversations/old-session/messages`);
    identity.set(null); sessionRevision.update(value => value + 1);
    identity.set(user); sessionRevision.update(value => value + 1);
    const next = firstValueFrom(service.ask('website', 'New session', 'new'));
    create('website', 'new-session');
    oldMessage.flush(reply); await rejected;
    http.expectOne(`${base}/website/conversations/new-session/messages`).flush(reply); await next;
  });

  it('discards an unreadable conversation on 410 and creates a fresh one on retry', async () => {
    const first = firstValueFrom(service.ask('website', 'Question', 'turn'));
    const rejected = expect(first).rejects.toMatchObject({ status: 410 });
    create('website', 'unreadable');
    http.expectOne(`${base}/website/conversations/unreadable/messages`).flush({ code: 'conversation_unreadable' }, { status: 410, statusText: 'Gone' }); await rejected;
    const retry = firstValueFrom(service.ask('website', 'Question', 'turn'));
    create('website', 'fresh'); http.expectOne(`${base}/website/conversations/fresh/messages`).flush(reply); await retry;
  });

});
