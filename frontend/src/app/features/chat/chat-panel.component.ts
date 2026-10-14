// Role: The chat drawer opened by the floating gold button. It shows the
// conversation, sends questions through ChatService, and turns answer sources
// into chips that open item pages. Chat text lives in memory only: it is never
// saved to browser storage and never written to the console. All text is shown
// as plain text, never as HTML.

import { Component, effect, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { ItemDataService } from '../../core/data/item-data.service';
import { Item } from '../../core/models';
import { ChatHistoryMessage, ChatMessage, ChatSource } from './chat.models';
import { ChatService } from './chat.service';

const SUGGESTIONS = [
  'How many 22K gold rings are in stock?',
  'Show stock of RNG-0142 by store.',
  'Which items are Draft?',
  "What is today's 22K rate?",
  'Which items have no images?'
];
// Tools whose recordId is an item id. Other tools use category or rate ids, which are not items.
const ITEM_TOOLS = new Set(['search_items', 'get_item_details', 'get_item_price_breakdown', 'get_stock_by_store', 'list_low_stock', 'list_items_by_status']);
const FALLBACK_ERROR = 'The assistant is busy. Please try again.';
const MAX_LENGTH = 500;
const MAX_HISTORY = 6;
const HISTORY_TEXT_LIMIT = 2000;
const MAX_CHIPS = 8;
const PHONE_WIDTH = 600;

interface SourceChip { key: string; label: string; itemId: number | null; }
interface LastRequest { question: string; history: ChatHistoryMessage[]; }

@Component({
  selector: 'nb-chat-panel', standalone: true, imports: [RouterLink],
  template: `
    <aside class="panel" [class.open]="open()" role="dialog" aria-label="Assistant" [attr.aria-hidden]="!open()">
      <header>
        <b>Assistant</b>
        <div class="header-actions">
          <button type="button" class="clear" [disabled]="messages().length === 0 && !loading()" (click)="clear()">Clear chat</button>
          <button type="button" class="close" aria-label="Close assistant" (click)="closed.emit()">×</button>
        </div>
      </header>

      <div class="log" #log role="log" aria-live="polite">
        @if (messages().length === 0 && !loading() && !failed()) {
          <div class="empty">
            <p>Ask about items, prices, and stock. I answer only from NB Jewel data.</p>
            <div class="suggestions">
              @for (s of suggestions; track s) { <button type="button" class="suggestion" (click)="send(s)">{{ s }}</button> }
            </div>
          </div>
        }

        @for (m of messages(); track m.id) {
          @if (m.role === 'user') {
            <div class="row user"><div class="bubble user-bubble">{{ m.text }}</div></div>
          } @else {
            <div class="row assistant">
              <div class="bubble assistant-bubble" [class.refused]="m.refused">
                @if (m.refused) { <span class="lock" aria-hidden="true">🔒</span> }
                <span class="text">{{ m.text }}</span>
              </div>
              @if (m.sources.length > 0) {
                <div class="chips">
                  @for (c of chips(m); track c.key) {
                    @if (c.itemId !== null) {
                      <a class="chip link" [routerLink]="['/catalog', c.itemId]" (click)="onChip()">{{ c.label }}</a>
                    } @else {
                      <span class="chip">{{ c.label }}</span>
                    }
                  }
                  @if (hiddenChips(m) > 0) { <span class="chip">+{{ hiddenChips(m) }} more</span> }
                </div>
              }
            </div>
          }
        }

        @if (loading()) {
          <div class="row assistant"><div class="bubble assistant-bubble thinking" aria-label="Thinking">Thinking<span class="dots" aria-hidden="true"><i>.</i><i>.</i><i>.</i></span></div></div>
        }
        @if (failed()) {
          <div class="row assistant">
            <div class="bubble assistant-bubble error" role="alert">
              <span>{{ errorText() }}</span>
              <button type="button" class="retry" (click)="retry()">Retry</button>
            </div>
          </div>
        }
      </div>

      <form class="composer" (submit)="$event.preventDefault(); send(draft())">
        <textarea #box rows="2" [maxLength]="maxLength" placeholder="Ask a question" aria-label="Your question"
          [value]="draft()" (input)="onInput($event)" (keydown)="onKey($event)"></textarea>
        <div class="composer-foot">
          <span class="counter" [class.near]="draft().length >= maxLength - 50">{{ draft().length }}/{{ maxLength }}</span>
          <button type="submit" class="send" [disabled]="loading() || draft().trim().length === 0">Send</button>
        </div>
      </form>
    </aside>
  `,
  styles: [`
    .panel { display:none; position:fixed; z-index:40; right:20px; bottom:90px; width:380px; height:min(620px, calc(100vh - 110px)); flex-direction:column; background:white; border:1px solid var(--line); border-radius:8px; box-shadow:0 12px 36px #0002; overflow:hidden; }
    .panel.open { display:flex; }
    header { display:flex; justify-content:space-between; align-items:center; padding:12px 16px; border-bottom:1px solid var(--line); color:var(--gold); background:var(--widget); }
    .header-actions { display:flex; align-items:center; gap:10px; }
    .clear { border:0; background:transparent; padding:0; color:var(--gold); font-size:12px; font-weight:700; text-decoration:underline; }
    .clear:disabled { opacity:.4; cursor:default; }
    .close { border:0; background:transparent; font-size:22px; line-height:1; color:var(--gold); }
    .log { flex:1; overflow-y:auto; padding:14px; display:flex; flex-direction:column; gap:12px; background:var(--page); }
    .empty { margin:auto 0; text-align:center; color:var(--muted); }
    .empty p { margin:0 0 14px; }
    .suggestions { display:flex; flex-direction:column; gap:8px; }
    .suggestion { padding:8px 12px; border:1px solid var(--gold); border-radius:16px; background:white; color:var(--gold); font-weight:600; text-align:left; }
    .suggestion:hover { background:var(--pale); }
    .row { display:flex; flex-direction:column; gap:6px; }
    .row.user { align-items:flex-end; }
    .row.assistant { align-items:flex-start; }
    .bubble { max-width:85%; padding:9px 12px; border-radius:12px; white-space:pre-wrap; overflow-wrap:anywhere; }
    .user-bubble { color:white; background:var(--gold); border-bottom-right-radius:3px; }
    .assistant-bubble { background:white; border:1px solid var(--line); border-bottom-left-radius:3px; }
    .assistant-bubble.refused { color:var(--muted); background:#f4efe2; border:1px dashed var(--gold); }
    .lock { margin-right:6px; }
    .assistant-bubble.error { display:flex; align-items:center; gap:10px; color:var(--danger); }
    .retry { border:1px solid var(--gold); border-radius:6px; background:white; color:var(--gold); padding:3px 10px; font-weight:700; }
    .chips { display:flex; flex-wrap:wrap; gap:6px; max-width:95%; }
    .chip { padding:3px 10px; border-radius:12px; font-size:12px; color:var(--muted); background:white; border:1px solid var(--line); }
    .chip.link { color:var(--gold); border-color:var(--gold); text-decoration:none; font-weight:600; cursor:pointer; }
    .chip.link:hover { background:var(--pale); }
    .dots i { font-style:normal; animation:blink 1.2s infinite; }
    .dots i:nth-child(2) { animation-delay:.2s; } .dots i:nth-child(3) { animation-delay:.4s; }
    @keyframes blink { 0%, 60%, 100% { opacity:.2; } 30% { opacity:1; } }
    .composer { padding:10px 12px; border-top:1px solid var(--line); background:white; }
    textarea { width:100%; resize:none; padding:8px 10px; border:1px solid #cfd1d5; border-radius:6px; outline:none; font:inherit; }
    textarea:focus { border-color:var(--button); }
    .composer-foot { display:flex; justify-content:space-between; align-items:center; margin-top:6px; }
    .counter { font-size:12px; color:var(--muted); } .counter.near { color:var(--danger); }
    .send { min-height:34px; padding:0 20px; border:0; border-radius:6px; background:var(--button); color:#333; font-weight:700; }
    .send:disabled { opacity:.5; cursor:default; }
    @media (max-width:${PHONE_WIDTH}px) { .panel { inset:0; width:auto; height:auto; border:0; border-radius:0; } }
  `]
})
export class ChatPanelComponent {
  private readonly chat = inject(ChatService);
  private readonly items = inject(ItemDataService);
  private readonly router = inject(Router);
  private readonly log = viewChild<ElementRef<HTMLElement>>('log');
  private readonly box = viewChild<ElementRef<HTMLTextAreaElement>>('box');
  private token = 0;
  private nextId = 1;
  private lastRequest: LastRequest | null = null;
  private readonly requestedItems = new Set<number>();

  readonly open = input(false);
  readonly closed = output<void>();

  readonly suggestions = SUGGESTIONS;
  readonly maxLength = MAX_LENGTH;
  readonly messages = signal<ChatMessage[]>([]);
  readonly draft = signal('');
  readonly loading = signal(false);
  readonly failed = signal(false);
  // The server's message when it sent one (the 503 text), else a fixed fallback.
  readonly errorText = signal(FALLBACK_ERROR);
  // Items found for source chips. null means the id is not an item.
  private readonly itemCache = signal<Record<number, Item | null>>({});

  constructor() {
    // Keep the newest message in view.
    effect(() => {
      this.messages(); this.loading(); this.failed(); this.open();
      setTimeout(() => { const el = this.log()?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
    });
    // Put the cursor in the input when the drawer opens.
    effect(() => {
      if (this.open()) setTimeout(() => this.box()?.nativeElement.focus());
    });
  }

  onInput(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value.slice(0, MAX_LENGTH));
  }

  // Enter sends. Shift+Enter adds a new line. Enter during text composition is left alone.
  onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      this.send(this.draft());
    }
  }

  send(text: string): void {
    const question = text.trim().slice(0, MAX_LENGTH);
    if (!question || this.loading()) return;
    const history = this.messages().slice(-MAX_HISTORY).map((m): ChatHistoryMessage => ({ role: m.role, content: m.text.slice(0, HISTORY_TEXT_LIMIT) }));
    this.messages.update(list => [...list, this.message('user', question)]);
    this.draft.set('');
    this.dispatch({ question, history });
  }

  retry(): void {
    if (this.lastRequest && !this.loading()) this.dispatch(this.lastRequest);
  }

  clear(): void {
    this.token++;
    this.messages.set([]);
    this.loading.set(false);
    this.failed.set(false);
    this.lastRequest = null;
  }

  onChip(): void {
    if (window.innerWidth <= PHONE_WIDTH) this.closed.emit();
  }

  chips(m: ChatMessage): SourceChip[] {
    const cache = this.itemCache();
    return m.sources.slice(0, MAX_CHIPS).map(s => {
      const id = this.itemId(s);
      const item = id === null ? undefined : cache[id];
      return { key: `${s.tool}:${s.recordId}`, label: `Source: ${s.tool}, ${item?.code ?? s.recordId}`, itemId: item ? item.id : null };
    });
  }

  hiddenChips(m: ChatMessage): number { return Math.max(0, m.sources.length - MAX_CHIPS); }

  private dispatch(request: LastRequest): void {
    this.lastRequest = request;
    this.loading.set(true);
    this.failed.set(false);
    const token = ++this.token;
    this.chat.ask(request.question, request.history).subscribe({
      next: response => {
        if (token !== this.token) return;
        this.messages.update(list => [...list, { ...this.message('assistant', response.answer), sources: response.sources, refused: response.refused }]);
        this.loading.set(false);
        this.lookUpItems(response.sources);
      },
      error: (err: HttpErrorResponse) => {
        if (token !== this.token) return;
        const text = err?.error?.message;
        this.errorText.set(typeof text === 'string' && text.length > 0 ? text : FALLBACK_ERROR);
        this.loading.set(false);
        this.failed.set(true);
      }
    });
  }

  private message(role: 'user' | 'assistant', text: string): ChatMessage {
    return { id: this.nextId++, role, text, sources: [], refused: false };
  }

  private itemId(source: ChatSource): number | null {
    return ITEM_TOOLS.has(source.tool) && /^\d+$/.test(source.recordId) ? Number(source.recordId) : null;
  }

  // Checks which source ids are real items, so only those chips become links.
  private lookUpItems(sources: ChatSource[]): void {
    for (const source of sources.slice(0, MAX_CHIPS)) {
      const id = this.itemId(source);
      if (id === null || this.requestedItems.has(id)) continue;
      this.requestedItems.add(id);
      this.items.getItem(id).subscribe({
        next: item => this.itemCache.update(c => ({ ...c, [id]: item })),
        error: () => this.itemCache.update(c => ({ ...c, [id]: null }))
      });
    }
  }
}
