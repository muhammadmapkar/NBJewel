// Role: Keeps owned conversation IDs in memory and sends only new turns to the backend AI gateway.
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject } from '@angular/core';
import { Observable, catchError, defer, finalize, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';
import { AssistantId, ChatResponse, Conversation } from './chat.models';

interface ConversationState { id?: string; creating?: Observable<Conversation>; }
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  readonly identityKey = computed(() => {
    const identity = this.auth.identity();
    const owner = identity ? `${identity.accountType}:${identity.id}` : 'anonymous';
    return `${owner}:${this.auth.sessionRevision()}`;
  });
  private owner = this.identityKey();
  private readonly conversations = new Map<AssistantId, ConversationState>();

  constructor() { effect(() => this.syncIdentity()); }

  ask(assistant: AssistantId, text: string, clientRequestId: string): Observable<ChatResponse> {
    return defer(() => {
      this.syncIdentity();
      const owner = this.owner;
      const state = this.state(assistant);
      return this.conversation(assistant, state).pipe(
        switchMap(conversation => {
          this.assertOwner(owner, assistant, state);
          return this.http.post<ChatResponse>(`${this.url(assistant)}/${encodeURIComponent(conversation.id)}/messages`, { text, clientRequestId });
        }),
        tap(() => this.assertOwner(owner, assistant, state)),
        catchError((error: { status?: number }) => {
          if ([401, 403, 404, 410].includes(error.status ?? 0) && this.conversations.get(assistant) === state) this.conversations.delete(assistant);
          return throwError(() => error);
        })
      );
    });
  }

  clearChat(assistant: AssistantId): Observable<void> {
    return defer(() => {
      this.syncIdentity();
      const state = this.conversations.get(assistant);
      if (!state) return of(undefined);
      const owner = this.owner;
      const conversation: Observable<{ id: string } | null> = state.id ? of({ id: state.id }) : state.creating ?? of(null);
      return conversation.pipe(
        switchMap(value => {
          this.assertOwner(owner, assistant, state);
          return value ? this.http.delete<void>(`${this.url(assistant)}/${encodeURIComponent(value.id)}`) : of(undefined);
        }),
        tap(() => { if (this.conversations.get(assistant) === state) this.conversations.delete(assistant); }),
        catchError((error: { status?: number }) => {
          if ([401, 403, 404, 410].includes(error.status ?? 0) && this.conversations.get(assistant) === state) this.conversations.delete(assistant);
          return throwError(() => error);
        })
      );
    });
  }

  private state(assistant: AssistantId): ConversationState {
    let state = this.conversations.get(assistant);
    if (!state) { state = {}; this.conversations.set(assistant, state); }
    return state;
  }

  private conversation(assistant: AssistantId, state: ConversationState): Observable<Conversation> {
    if (state.id) return of({ id: state.id } as Conversation);
    if (!state.creating) {
      state.creating = this.http.post<Conversation>(this.url(assistant), {}).pipe(
        tap(conversation => state.id = conversation.id),
        finalize(() => state.creating = undefined),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return state.creating;
  }

  private syncIdentity(): void {
    if (this.owner === this.identityKey()) return;
    this.owner = this.identityKey();
    this.conversations.clear();
  }

  private assertOwner(owner: string, assistant: AssistantId, state: ConversationState): void {
    this.syncIdentity();
    if (owner !== this.owner || this.conversations.get(assistant) !== state) throw new Error('Conversation identity changed.');
  }

  private url(assistant: AssistantId): string { return `${environment.apiBaseUrl.replace(/\/$/, '')}/api/ai/${assistant}/conversations`; }
}
