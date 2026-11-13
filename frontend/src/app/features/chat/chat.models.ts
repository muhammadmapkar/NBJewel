// Role: Describes server-owned AI conversations and the panel's memory-only display messages.
export type AssistantId = 'erp' | 'website';
export interface ChatSource { tool: string; recordId: string; }
export interface Conversation { id: string; assistantId: AssistantId; createdAt: string; expiresAt: string; }
export interface ChatResponse { answer: string; sources: ChatSource[]; refused: boolean; traceId: string; }
export interface ChatMessage { id: number; role: 'user' | 'assistant'; text: string; sources: ChatSource[]; refused: boolean; }
