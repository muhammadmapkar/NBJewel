// Role: Sends a chat question to the .NET API (POST /api/chat) and returns the
// answer. It is the only place the chat panel talks to the network. The AI
// service and its keys stay behind the .NET API, never in the browser.

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ChatHistoryMessage, ChatRequest, ChatResponse } from './chat.models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);

  ask(question: string, history: ChatHistoryMessage[]): Observable<ChatResponse> {
    const body: ChatRequest = { question, history };
    return this.http.post<ChatResponse>(`${environment.apiBaseUrl}/api/chat`, body);
  }
}
