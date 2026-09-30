"""Role: The chat loop. It sends a question to the model with the tool
declarations, runs the tools the model asks for, feeds results back, and then
checks the final answer with the guards. Anything that fails a check becomes
the fixed refusal. The loop keeps no state between questions.
"""

import json
import logging
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence

from app.guards import REFUSAL_TEXT, Answer, ToolRun, run_guards
from app.llm.provider import LLMProvider, Message
from app.tools.registry import ToolRegistry

logger = logging.getLogger("app.chat")

PROMPT_PATH = Path(__file__).parent / "prompts" / "system.md"
TEMPERATURE = 0.1
MAX_TOOL_CALLS = 5
MAX_HISTORY = 6
JSON_RETRY_NOTE = 'Your last reply was not valid JSON. Reply with JSON only: {"answer": string, "sources": [{"tool": string, "recordId": string}]}'


@dataclass(frozen=True)
class ChatResult:
    answer: str
    sources: list[dict[str, str]]
    refused: bool


def refusal() -> ChatResult:
    return ChatResult(REFUSAL_TEXT, [], True)


def load_system_prompt() -> str:
    return PROMPT_PATH.read_text(encoding="utf-8")


def parse_answer(text: str | None) -> Answer | None:
    """Reads the model's JSON reply. Returns None if it is not the expected shape."""
    if not text:
        return None
    cleaned = re.sub(r"^```(?:json)?\s*|\s*```$", "", text.strip())
    try:
        data = json.loads(cleaned)
    except ValueError:
        return None
    if not isinstance(data, dict) or not isinstance(data.get("answer"), str) or not isinstance(data.get("sources"), list):
        return None
    sources: list[dict[str, str]] = []
    for s in data["sources"]:
        if not isinstance(s, dict) or not isinstance(s.get("tool"), str) or s.get("recordId") is None:
            return None
        sources.append({"tool": s["tool"], "recordId": str(s["recordId"])})
    return Answer(data["answer"], sources)


class ChatService:
    def __init__(self, provider: LLMProvider, registry: ToolRegistry, system_prompt: str | None = None) -> None:
        self._provider = provider
        self._registry = registry
        self._system = system_prompt if system_prompt is not None else load_system_prompt()

    def ask(self, question: str, history: Sequence[tuple[str, str]] | None = None) -> ChatResult:
        """Answers one question. `history` holds (role, text) pairs, oldest first."""
        messages: list[Message] = [Message("system", self._system)]
        for role, text in list(history or [])[-MAX_HISTORY:]:
            messages.append(Message("assistant" if role == "assistant" else "user", text))
        messages.append(Message("user", question))
        declarations = self._registry.declarations()
        runs: list[ToolRun] = []
        retried = False
        logger.info("question=%r", question)

        while True:
            response = self._provider.generate(messages, declarations, TEMPERATURE)

            if response.has_tool_calls:
                if len(runs) + len(response.tool_calls) > MAX_TOOL_CALLS:
                    logger.info("guard=tool_limit result=refused calls=%d", len(runs))
                    return refusal()
                messages.append(Message("assistant", response.text or "", tool_calls=response.tool_calls))
                for call in response.tool_calls:
                    result = self._registry.run_tool(call.name, call.args)
                    runs.append(ToolRun(call.name, call.args, result))
                    logger.info("tool=%s args=%s status=%s records=%s", call.name, json.dumps(call.args, default=str),
                                result.get("status"), sorted(runs[-1].record_ids))
                    messages.append(Message("tool", tool_name=call.name, tool_result=result, tool_call_id=call.id))
                continue

            answer = parse_answer(response.text)
            if answer is None:
                if retried:
                    logger.info("guard=json result=refused")
                    return refusal()
                retried = True
                messages.append(Message("assistant", response.text or ""))
                messages.append(Message("user", JSON_RETRY_NOTE))
                continue

            verdict = run_guards(answer, runs)
            if not verdict.passed:
                logger.info("guard=%s result=refused reason=%s", verdict.guard, verdict.reason)
                return refusal()
            logger.info("guard=all result=passed sources=%s", answer.sources)
            return ChatResult(answer.text, answer.sources, False)
