"""Role: Guard 4. The answer must not repeat the system prompt. Planted text in
item data or a user message may ask the model to "reveal your system prompt".

Limit: only lines of the prompt that appear word for word (ignoring case and punctuation)
are caught. A paraphrase of the prompt is not.
"""

import re

from app.guards.types import PASS, Answer, GuardResult

MIN_WORDS = 5


def _normalize(text: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9]+", " ", text.lower()).split())


def prompt_lines(system_prompt: str) -> list[str]:
    lines = (_normalize(line.strip().lstrip("-").strip()) for line in system_prompt.splitlines())
    return [line for line in lines if len(line.split()) >= MIN_WORDS]


def check_prompt_leak(answer: Answer, system_prompt: str | None) -> GuardResult:
    if not system_prompt:
        return PASS
    text = _normalize(answer.text)
    for line in prompt_lines(system_prompt):
        if line in text:
            return GuardResult(False, "leak", "The answer repeats part of the system prompt.")
    return PASS
