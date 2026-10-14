@AGENTS.md

WORKFLOW
1. After finishing each phase, call the nbjewel-lead subagent with your own report.
2. If its verdict is PASS, follow its next prompt.
3. If PARTIAL or FAIL, stop and show me its verdict.
4. Chain at most 3 phases. Then stop and summarize in 8 lines.

ALWAYS STOP AND ASK ME BEFORE
1. Deleting any file or folder.
2. git push.
3. Anything touching .env or secrets.
4. Docker or SQL data changes.
5. Any step that needs Mr. Sakthivel's decision.

STYLE
Replies max 8 lines. Grep first. Quiet flags. File edit tool only. No em-dashes in documents.
