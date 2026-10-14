# AGENTS.md

Rules for any AI coding agent working in this repository.

## Project

- Name: nbjewel-assistant.
- Internship demo for Cetas (jewelry retail ERP, NB Jewel).
- Sample data only. No real client data.
- Never invent NB Jewel internals. Label guesses as "assumed" in docs.

## Stack

- Frontend: Angular (standalone components, TypeScript strict, signals).
- Backend: .NET Web API (C#). SQL Server later.
- Chatbot: runs inside the .NET Web API, calling Gemini over REST. No separate AI service.

## Chatbot rules

- The chatbot answers only from ERP data, using fixed read-only tools.
- No free SQL.
- No answers from model memory.

## Security and data

- API keys live only in backend environment variables. Never in the frontend or the repo.
- The data layer is swappable: sample repository now, SQL or a real API later.

## Working rules

- The owner has no .NET or Angular production experience. Add a short comment at the top of each file explaining its role.
- The frontend must reuse the existing theme, shell, and shared ui components. Do not restyle.
- No em-dashes in UI text or docs.
- Always output complete files.
