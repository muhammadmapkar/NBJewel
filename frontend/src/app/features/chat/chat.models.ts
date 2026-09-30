// Role: Describes the chat request and response (as sent by the .NET API) and
// the messages the chat panel keeps in memory.

export interface ChatSource {
  tool: string;
  recordId: string;
}

export interface ChatHistoryMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  question: string;
  history: ChatHistoryMessage[];
}

export interface ChatResponse {
  answer: string;
  sources: ChatSource[];
  refused: boolean;
}

export interface ChatMessage {
  id: number;
  role: 'user' | 'assistant';
  text: string;
  sources: ChatSource[];
  refused: boolean;
}
