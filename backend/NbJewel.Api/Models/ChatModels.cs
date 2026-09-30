// Role: Defines the chat request and response contracts. They match the AI
// service's POST /chat exactly, so the API can pass them straight through.

namespace NbJewel.Api.Models;

public sealed record ChatHistoryMessage(string Role, string Content);
public sealed record ChatRequest(string Question, List<ChatHistoryMessage>? History);
public sealed record ChatSource(string Tool, string RecordId);
public sealed record ChatResponse(string Answer, List<ChatSource> Sources, bool Refused);
public sealed record ErrorMessage(string Message);
