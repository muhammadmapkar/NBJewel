# Demo day checklist

Short list of rules for the day. Steps for the demo itself are in `demo-script.md`.

1. Run in Sample mode only. Do not demo Sql mode. It has not been run against a real SQL Server.
2. Run locally only. No login exists, so do not expose the API to the internet.
3. Load the Gemini settings from `.env` into the shell before starting the API:
   `set -a; source .env; set +a`, then `cd backend/NbJewel.Api && dotnet run --launch-profile http`.
4. Start the Angular app: `cd frontend && npm start`. Open `http://localhost:4200/catalog`.
5. Rerun the 5 questions from `run-guide.md` before the demo, and once more just before going on.
6. Gemini can be slow or return 503. The chat then shows "The assistant is busy. Please try again." Press Retry once.
7. Keep a screen recording of a good run as a fallback, and show Swagger and the catalog if chat is down.
8. Gemini approval was verbal. Written confirmation from Mr. Sakthivel is pending (`questions-for-sakthivel.md`, item 18).
9. All data is fake sample data. Say so at the start.
10. Changes made in the demo (status, stock) reset when the API restarts.
