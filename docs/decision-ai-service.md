# Decision: where the chatbot lives

Status: decided. Mr. Sakthivel chose option B (move into the .NET API). The approval was verbal and is not written down anywhere else (assumed to stand until confirmed in writing). Anything about NB Jewel itself is assumed. All figures marked "assumed" are guesses to be replaced by real numbers.

## The decision

Should the chat assistant stay a separate Python (FastAPI) service, or move into the .NET API?

## What was built first

A separate FastAPI service (`ai-service/`) ran the chat loop, 8 read-only tools, 4 answer guards and the Gemini provider. It read ERP data only through the .NET API, with GET requests that the code enforced. The .NET API forwarded `POST /api/chat` to it.

## What exists now

The same chatbot runs inside the .NET API, in `backend/NbJewel.Api/Chat/`:

- `ChatTools.cs`: the same 8 read-only tools, now reading through `IChatDataReader` (in `IChatDataReader.cs`), a read-only door over the repository, instead of HTTP calls.
- `Guards.cs`: the same 4 answer guards and the fixed refusal text.
- `GeminiLlmClient.cs`: Gemini over its REST API with a plain `HttpClient`. The key comes only from the `GEMINI_API_KEY` environment variable.
- `ChatOrchestrator.cs`: the loop (at most 5 tool calls and 8 model calls per question, last 6 history messages, temperature 0.1).
- `ChatController` keeps the same `POST /api/chat` request and response, so the Angular app did not change.

Added in the move: a 30 second limit per Gemini request, a 45 second limit per question, the message "The assistant is busy. Please try again." with status 503, and a log line per Gemini call with status, error type and milliseconds only.

Nothing calls the old `ai-service/` folder. The live Gemini run passed cleanly twice (the second time on 2026-10-14), so the folder is deleted in its own commit after this one. It stays in git history.

## Options considered

| | A. Keep separate Python service | B. Move into the .NET API (chosen) |
|---|---|---|
| Fit with the team | Needs Python on the server and someone who knows it | One language and one deploy, if the team is .NET only (assumed) |
| Isolation | The model, its key and its tools sit apart from the ERP code | The key and the model code live inside the ERP process |
| Read-only guarantee | Enforced by a GET-only client, tested by a source scan | The tools use only `IChatDataReader`, whose methods all start with Get or Search. This is a code and test rule, not a network rule, so it needs care in review |
| Change speed | Prompts, tools and guards change without redeploying the ERP API | Every AI change ships with the API |
| Cost to build | Done | Done in the move (commit `56fd3b7`) |
| Running cost | One extra small service (assumed) | None extra |
| Model swap | One provider class | One provider class (`ILlmClient`) |

## Why B

The decision came from Mr. Sakthivel. The reasons that fit the project: the team is assumed to be .NET only, one deploy is simpler, and no Python is needed on the server. The cost is less isolation: the model code now lives next to the repositories that can write. The chat tools are written to call only read methods, and tests check the tools and the guards.

Whichever option is used, keep these rules: the assistant answers only from fixed tools, no free SQL, no answers from model memory, and the API key lives only in backend environment variables.

## Estimates (all assumed)

- Model cost: assumed a few cents per 100 questions with a small model, not measured.
- Answer time: the last live run answered in 1.4 to 4.7 seconds. Earlier runs saw 16 to 25 seconds and one call of 90 seconds, caused by Gemini load. The API now stops a question at 45 seconds.
- Hosting: no extra service.

## Known limits

- The guards compare numbers and sources. They cannot tell whether the sentence around a number is true. See `grounding-design.md`.
- The live result is one clean run of 5 questions. It has not been repeated.
- No login or per-user data rules exist yet.

## Open questions for Mr. Sakthivel

1. Please confirm the decision in writing (it was verbal).
2. Which AI vendors, models and regions are approved? Is sending ERP data to Gemini allowed at all, even for pilot data?
3. Must the chatbot run inside NB Jewel's network or cloud account?
4. Who owns the prompt, tools and guards after the internship?
5. What response time and monthly cost are acceptable?
6. Should the assistant see different data for different users? How do users sign in?
7. Does the real system expose an API the tools can call, or only a SQL Server database? (See question 1 in `questions-for-sakthivel.md`.)
