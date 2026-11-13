<!-- Role: Records what the repository cleanup on branch chore/repo-cleanup removed, why, the test results, and what was kept. -->
# Cleanup report

Date: 2026-11-13. Branch: `chore/repo-cleanup`, created from `tryon-feature` at `67de770`. Nothing was pushed, merged or deployed. Removed files stay in git history.

## What was removed

| Commit | Category | Removed | Why |
|---|---|---|---|
| `a285a91` | Scratch files | `package-lock.json` (root), `backend/NbJewel.Api/NbJewel.Api.http` | The lockfile was empty and there is no root `package.json`. The `.http` file held manual smoke requests. Zero references to either. |
| `8a5ded9` | Dead code | `frontend/src/app/features/gold-scheme/form-page.styles.ts` | Exported an empty string. Zero importers. |
| `9f7b254` | Unused dependency | `Microsoft.AspNetCore.OpenApi` in `NbJewel.Api.csproj` | No code used it. Swagger comes from Swashbuckle, which does not depend on it. |
| `59fb4e2` | Duplicate docs | `.github/copilot-instructions.md` | Copy of `AGENTS.md` with one line of drift. Zero references. |
| `5f222dd` | Unreferenced docs | `docs/code-walkthrough.pdf`, `docs/demo-day-checklist.md`, `docs/unified-ai-audit.md`, `docs/unified-ai-platform-plan.md`, `docs/customer-chatbot-faq.md`, `docs/next-steps.md` | No file in the repository linked to any of them. Removed on the owner's instruction. |

No build output or cache was tracked (`bin`, `obj`, `dist`, `.angular`, `node_modules` are all ignored already), so that category was empty.

## Mistake made and corrected

Commit `9f7b254` was made with `git commit -a`. Another session had edited `PROGRESS.md` and `docs/run-guide.md` in the working tree at the same time, and those edits were swept into the commit. Commit `c003fa7` restores both files to their committed state, and the edits were put back in the working tree, uncommitted and unchanged. History was not rewritten, so `9f7b254` still shows those two files in its diff. All later commits used explicit paths.

## Test results

Backend tests ran in sample mode (`NBJEWEL_SQL_CONNECTION` unset). The 6 skipped tests are the SQL-only tests.

| When | dotnet build | Backend tests | Frontend tests | Angular production build |
|---|---|---|---|---|
| Before (at `67de770`) | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |
| After `a285a91` | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |
| After `8a5ded9` | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |
| After `9f7b254` | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |
| After `59fb4e2` | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |
| After `5f222dd` | 0 errors, 0 warnings | 304 passed, 0 failed, 6 skipped | 66 passed | Passed, 350.23 kB initial |

No commit had to be reverted for a failing check.

Not verified: the Swagger page was not opened in a running API after the package removal. The SQL-enabled test suite was not run (it needs the Docker container, which needs approval).

## Approved but not removed

- `PROGRESS.md`: approved for deletion, but it now has uncommitted edits from another session. Uncommitted changes are on the never-touch list, so it was left alone. The nbjewel-lead agent also uses it as its entry point.
- `frontend/netlify.toml` or `frontend/vercel.json`: only one of the pair should go, and the owner has not said which host is the real target. Both are still referenced by `README.md` and `docs/tryon-deploy.md`.
- `.gitignore` additions (`TestResults/`, `*.trx`, `.vs/`, `.vercel/`, `.netlify/`, `.playwright-mcp/`): proposed, not yet approved, not applied.

## Kept despite looking unused

- `docs/api-contracts.md`, `docs/assumptions.md`, `docs/data-dictionary.md`: placeholder docs, kept on the owner's instruction. Linked from `README.md`.
- `Jewellery Images/` (6 tracked images, 1 untracked): zero references, on the never-touch list.
- `frontend/public/tryon-samples/` and `sample-data/jewelry/`: byte-identical sets, but both are used (frontend sample mode and the API seed).
- Controllers, `Chat/AiGateway.cs`, `Platform/LegacyAssistant.cs`, `Platform/PlatformKeyProtection.cs`: no cross-file type references, but ASP.NET finds controllers by reflection and the others are registered in `Program.cs`.
- `tslib`, `@types/qrcode`: no direct import, but required by Angular and by the `qrcode` import.
- `coverlet.collector`: unused in code, but it is in the test project (never-touch).
- Unused exports `MakingType` and `Widget` in `frontend/src/app/core/mock/mock-data.ts`: removing them means editing a live file, and this task was removal only.
- `scripts/`, `docker-compose.yml`, `render.yaml`, `backend/Dockerfile`: all referenced by the README or deploy docs.

## Follow-ups

- `docs/agent-tasks.md` was linked only from `docs/next-steps.md`, which is now removed. It has no inbound references any more.
- `AI-MEMORY-HANDOFF.md` still says the SQL fixes are uncommitted. They are in commit `586f9c8`. The file is on the never-touch list, so it was not edited.
- Another session is editing this working tree. Its uncommitted edits to `PROGRESS.md` and `docs/run-guide.md` are intact and were not committed.
