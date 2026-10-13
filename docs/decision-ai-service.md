# Decision: where the AI service lives

Status: proposed, for review by Mr. Sakthivel. Anything about NB Jewel itself is assumed. All figures marked "assumed" are guesses to be replaced by real numbers.

## The decision

Should the chat assistant stay a separate Python (FastAPI) service, or move into the .NET API?

## What exists today

A separate FastAPI service (`ai-service/`) runs the chat loop, 8 read-only tools, 4 answer guards and the Gemini provider. It reads ERP data only through the .NET API, with GET requests that the code enforces. The .NET API forwards `POST /api/chat` to it. The frontend never talks to the service or the model.

## Options

| | A. Keep separate Python service (today) | B. Move into the .NET API |
|---|---|---|
| Fit with the team | Needs Python on the server and someone who knows it | One language and one deploy, if the team is .NET only (assumed) |
| Isolation | The model, its key and its tools sit apart from the ERP code. A bug there cannot touch ERP data | The key and the model code live inside the ERP process |
| Read-only guarantee | Enforced by a GET-only client, tested by a source scan | Possible, but tools would sit next to the repositories that can write, so the rule needs care |
| Change speed | Prompts, tools and guards change without redeploying the ERP API | Every AI change ships with the API |
| Cost to build | Done, with 282 offline tests | Rewrite of tools, guards and tests (assumed 2 to 3 weeks) |
| Running cost | One extra small service (assumed) | None extra |
| Model swap | One provider class | One provider class |

## Recommendation

Keep option A for the demo and the first pilot. The service is built and tested, and the separation protects the ERP: the model only reaches data through a narrow read-only door. Revisit option B only if the team cannot run Python in production.

Whichever option is chosen, keep these rules: the assistant answers only from fixed tools, no free SQL, no answers from model memory, and the API key lives only in backend environment variables.

## Estimates (all assumed)

- Model cost: assumed a few cents per 100 questions with a small model, not measured. The live tests have not been run, so there is no real token count yet.
- Answer time: assumed 3 to 10 seconds per question, not measured. The API allows 90 seconds.
- Hosting the extra service: assumed one small container.

## Known limits

- The guards compare numbers and sources. They cannot tell whether the sentence around a number is true. See `grounding-design.md`.
- The live model pass rate is unknown until the live tests run.
- No login or per-user data rules exist yet.

## Open questions for Mr. Sakthivel

1. Is the production stack .NET only, or can the team run a Python service?
2. Which AI vendors, models and regions are approved? Is sending ERP data to Gemini allowed at all, even for pilot data?
3. Must the service run inside NB Jewel's network or cloud account?
4. Who owns the prompt, tools and guards after the internship?
5. What response time and monthly cost are acceptable?
6. Should the assistant see different data for different users? How do users sign in?
7. Does the real system expose an API the tools can call, or only a SQL Server database? (See question 1 in `questions-for-sakthivel.md`.)
