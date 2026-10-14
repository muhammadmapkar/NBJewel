# Data privacy checklist

Do not send any real NB Jewel or client data to the chatbot or to Gemini until every check below has an answer and a date, and the last check (written approval) is done. Today the demo uses fake data only.

Fill in "Answer" and "Date" for each check. Leave no blank.

## The 8 checks

### 1. Training use
Will the AI vendor use our prompts or data to train its models? Is there a way to opt out, and is the opt-out in writing?
- Answer:
- Date:

### 2. Retention and logging
How long does the vendor keep prompts and answers? Who at the vendor can read them? Also decide how long our own .NET API keeps its logs. Today it logs tool names, tool status, record counts, guard results and, for each Gemini call, the status, error type and time to the console. It does not log the question. Nothing is stored in a database.
- Answer:
- Date:

### 3. Data region
In which country or region is the data processed and stored? Does the client allow that region?
- Answer:
- Date:

### 4. Data processing agreement
Is there a signed data processing agreement with the vendor that covers this use?
- Answer:
- Date:

### 5. Sub-processors
Which other companies does the vendor use to process the data? Are they acceptable to the client?
- Answer:
- Date:

### 6. Client contract allows AI
Does the client contract allow sending its data to an AI model (Gemini) at all?
- Answer:
- Date:

### 7. Personal data in prompts
Can prompts or tool results contain personal data (customer names, phone numbers, addresses, ID numbers)? Today the tools return item, stock and rate data only, but a user can type anything into the chat box. Decide what is allowed and whether to block or hide it.
- Answer:
- Date:

### 8. Written approval from Sakthivel
Has Sakthivel approved in writing the use of real data with this assistant? Keep the written approval with the project documents.
- Answer:
- Date:

## API key safety rules

- The Gemini key lives only in the environment of the .NET API (an environment variable, loaded from a local `.env` file in the repository root). It is never in the frontend, the source code, the tests, the docs or git.
- `.env` and `.env.*` are listed in `.gitignore`. Only `.env.example` files, with empty values, are tracked. Never put a real value in `.env.example`.
- Never paste the key into chat, issues, screenshots, commit messages or terminal output that is shared.
- The offline tests need no key. For live tests, set the key in your shell for that session only.
- Use a separate key for demos and testing. Set a usage limit on it.
- If a key is ever exposed, revoke it at once and create a new one. Deleting it from git history is not enough.
- The code reads the key as a secret value, and never logs it.
- Do not add the key to any file under `frontend/`. Anything in the frontend is visible to every user of the browser.
