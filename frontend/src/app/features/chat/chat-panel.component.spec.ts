// Role: Checks plain public source chips, pending retry IDs and display erasure on identity changes.
import '@angular/compiler';
import { createEnvironmentInjector, EnvironmentInjector, runInInjectionContext, signal, ɵEffectScheduler, ɵINJECTOR_SCOPE, provideZonelessChangeDetection } from '@angular/core';
import { Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { of, Subject } from 'rxjs';
import { ItemDataService } from '../../core/data/item-data.service';
import { ChatPanelComponent } from './chat-panel.component';
import { ChatService } from './chat.service';
import { ChatResponse } from './chat.models';
describe('chat panel ownership', () => {
  let injector: EnvironmentInjector;
  let panel: ChatPanelComponent;
  const identityKey = signal('anonymous');
  let response: Subject<ChatResponse>;
  const ask = vi.fn();
  const getItem = vi.fn();
  const clearChat = vi.fn(() => of(undefined));
  beforeEach(() => {
    identityKey.set('anonymous'); response = new Subject<ChatResponse>();
    ask.mockReset().mockImplementation(() => response); getItem.mockReset(); clearChat.mockClear();
    injector = createEnvironmentInjector([provideZonelessChangeDetection(), { provide: ɵINJECTOR_SCOPE, useValue: 'root' }, { provide: ChatService, useValue: { identityKey, ask, clearChat } }, { provide: ItemDataService, useValue: { getItem } }, { provide: Router, useValue: {} }], null as unknown as EnvironmentInjector);
    panel = runInInjectionContext(injector, () => new ChatPanelComponent());
    Object.defineProperty(panel, 'assistant', { value: () => 'website' });
    injector.get(ɵEffectScheduler).flush();
  });
  afterEach(() => injector.destroy());
  it('does not look up public sources in the internal catalog or produce internal links', () => {
    panel.send('Help');
    response.next({ answer: '<b>Plain text</b>', sources: [{ tool: 'get_item_details', recordId: '1' }], refused: false, traceId: 'trace' });
    expect(getItem).not.toHaveBeenCalled();
    expect(panel.messages()[1].text).toBe('<b>Plain text</b>');
    expect(panel.chips(panel.messages()[1])[0].itemId).toBeNull();
    expect(ask.mock.calls[0][0]).toBe('website');
  });
  it('preserves the request ID when retrying a failed turn', () => {
    panel.send('Help'); response.error({ status: 503 });
    response = new Subject<ChatResponse>(); panel.retry();
    expect(ask.mock.calls[1]).toEqual(ask.mock.calls[0]);
    expect(panel.messages()).toHaveLength(1);
  });
  it('erases display state and ignores pending responses after identity changes', () => {
    panel.send('Private question'); panel.draft.set('Draft');
    identityKey.set('erp:staff'); injector.get(ɵEffectScheduler).flush();
    response.next({ answer: 'Late answer', sources: [], refused: false, traceId: 'trace' });
    expect(panel.messages()).toEqual([]); expect(panel.draft()).toBe(''); expect(panel.loading()).toBe(false);
  });
  it('clears display only after server deletion succeeds', () => {
    panel.send('Help'); response.next({ answer: 'Answer', sources: [], refused: false, traceId: 'trace' });
    const deletion = new Subject<void>(); clearChat.mockReturnValueOnce(deletion);
    panel.clear(); expect(panel.messages()).toHaveLength(2);
    deletion.next(); expect(panel.messages()).toEqual([]); expect(clearChat).toHaveBeenCalledWith('website');
  });
  it('keeps the display on failed deletion and erases it on signout', () => {
    panel.send('Help'); response.next({ answer: 'Answer', sources: [], refused: false, traceId: 'trace' });
    const deletion = new Subject<void>(); clearChat.mockReturnValueOnce(deletion);
    panel.clear(); deletion.error({ status: 503 });
    expect(panel.messages()).toHaveLength(2); expect(panel.clearFailed()).toBe(true);
    identityKey.set('customer:user'); injector.get(ɵEffectScheduler).flush();
    expect(panel.messages()).toEqual([]); expect(panel.failed()).toBe(false);
  });

  it('offers a fresh chat after an unreadable conversation without displaying old messages', () => {
    panel.send('Question'); response.error({ status: 410 });
    expect(panel.messages()).toEqual([]); expect(panel.conversationExpired()).toBe(true);
    response = new Subject<ChatResponse>(); panel.retry();
    expect(panel.messages()).toHaveLength(1); expect(panel.messages()[0].text).toBe('Question');
    expect(ask.mock.calls[1]).toEqual(ask.mock.calls[0]);
  });

});
