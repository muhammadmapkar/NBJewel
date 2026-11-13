<!-- Role: Defines repository rules and the turn-based storefront handoff protocol. -->
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

## Handoff protocol

- Agents work in turns, one at a time. Use `AI-MEMORY-HANDOFF.md` as the only handoff file. Read it fully on every start and when the owner says "continue".
- On every start: run `git fetch --all --prune`, confirm `feature/customer-storefront`, then run `git pull --ff-only`. If pull fails or the branch diverges, stop and log the failure without guessing. Run `git status` and `git branch -vv` during setup. Set upstream to `origin/feature/customer-storefront` only if missing.
- Run `date` and check the handoff Lock. If another agent updated it under 30 minutes ago, do not edit anything; report only. Otherwise take the Lock with agent name and timestamp, verify the last commit against the handoff, and resume the first unchecked step.
- Work only in the main checkout on `feature/customer-storefront`. Never touch `~/.codex/worktrees/2628` or create another worktree for this turn-based work.
- Authorization permits commits and pushes only to `origin feature/customer-storefront`. No force push, merge, deploy, deletion, Docker actions, or SQL data steps. Prepare SQL changes as files only; never apply them.
- Use sample mode and keep SQL connection variables unset in test/application child processes. Never touch `.env`, secrets, or production data. No new background-removal tooling. Cutout licensing stays open and cutout quality requires manual review.
- Never touch, stage, or commit these owner files: `PROGRESS.md`, `docs/run-guide.md`, `customer-tryon/package-lock.json`, `docs/browser-security-test-plan.md`, `old_proof.png`, `Jewellery Images/Pasted 2026-11-01 at 11.10.49 AM.png`.
- Stage by explicit file path only. Never run `git add -A`, `git add .`, or `git commit -a`. Check `git status` before every commit. Use commit prefix `wip(agent-name):` and push after every commit. Work must never live only locally.
- Update the handoff after every finished task or step, not just at the end. Run the task's tests before every push and log the command and result. Mark a task done only after its required tests pass.
- The handoff must contain: Lock (agent, since); Branch and last commit; Current goal; Done; Next steps (ordered checklist); Blockers and decisions; Files touched; Test commands and last results; Manual checks for Muhammad; Agent log (newest first).
- Before finishing or reaching a usage limit, update the handoff, commit and push, and release the Lock. Record the release in that final pushed checkpoint so the next agent can acquire it.
- Do not ask the owner questions. Pick the safest option and log the decision. Accounts, chatbot, and locator are later phases. After phase 2 task 9, run the lead review, stop, and wait for Muhammad.
