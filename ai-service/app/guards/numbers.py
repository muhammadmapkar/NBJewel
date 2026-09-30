"""Role: Guard 3. Every number in the answer must come from the tool results.

A number is allowed if it:
  1. appears in the tool results (as a number, or inside a text such as "22K", "RNG-0142" or a date), or
  2. equals the length of a list in the tool results (for example "3 items"), or
  3. equals a sum of one numeric field, taken over the records of one list or over all tool results
     (for example total on hand across stores).

Known limits: averages, differences, percentages, rounded values and sums of different fields are
rejected. Numbers written as words ("three") are not checked. A number that appears only inside
text from a tool result counts as present, because the guard cannot tell data from injected text.
The guard only compares numbers. It cannot tell whether the sentence around a number is true.
"""

import re
from collections import defaultdict
from decimal import Decimal, InvalidOperation
from typing import Any

from app.guards.types import PASS, Answer, GuardResult, ToolRun

_NUMBER = re.compile(r"\d[\d,]*(?:\.\d+)?")
_LIST_MARKER = re.compile(r"^\s*(?:[-*]\s+)?\d+[.)]\s+", re.MULTILINE)


def _summable(key: str) -> bool:
    # Ids identify records. Adding them up never gives a meaningful number.
    return key.lower() != "id" and not key.lower().endswith("id")


def _to_decimal(text: str) -> Decimal | None:
    try:
        return Decimal(text.replace(",", "")).normalize()
    except InvalidOperation:
        return None


def numbers_in_text(text: str) -> set[Decimal]:
    return {d for m in _NUMBER.findall(text) if (d := _to_decimal(m)) is not None}


def answer_numbers(text: str) -> set[Decimal]:
    # "1. Zork Ring" is list numbering, not a claim about data.
    return numbers_in_text(_LIST_MARKER.sub("", text))


def _collect(node: Any, found: set[Decimal], lengths: set[Decimal],
             field_values: dict[str, list[Decimal]], sums: set[Decimal]) -> None:
    if isinstance(node, bool):
        return
    if isinstance(node, (int, float)):
        found.add(Decimal(str(node)).normalize())
    elif isinstance(node, str):
        found.update(numbers_in_text(node))
    elif isinstance(node, list):
        lengths.add(Decimal(len(node)))
        per_field: dict[str, list[Decimal]] = defaultdict(list)
        for child in node:
            if isinstance(child, dict):
                for key, value in child.items():
                    if isinstance(value, (int, float)) and not isinstance(value, bool) and _summable(key):
                        per_field[key].append(Decimal(str(value)))
        for key, values in per_field.items():
            sums.add(sum(values, Decimal(0)).normalize())
        for child in node:
            _collect(child, found, lengths, field_values, sums)
    elif isinstance(node, dict):
        for key, value in node.items():
            if isinstance(value, (int, float)) and not isinstance(value, bool) and _summable(key):
                field_values[key].append(Decimal(str(value)))
            _collect(value, found, lengths, field_values, sums)


def allowed_numbers(runs: list[ToolRun]) -> set[Decimal]:
    found: set[Decimal] = set()
    lengths: set[Decimal] = set()
    sums: set[Decimal] = set()
    field_values: dict[str, list[Decimal]] = defaultdict(list)
    for run in runs:
        _collect(run.result.get("data"), found, lengths, field_values, sums)
    for values in field_values.values():
        if len(values) > 1:
            sums.add(sum(values, Decimal(0)).normalize())
    return found | lengths | sums


def check_numbers(answer: Answer, runs: list[ToolRun]) -> GuardResult:
    allowed = allowed_numbers(runs)
    for number in sorted(answer_numbers(answer.text)):
        if number not in allowed:
            return GuardResult(False, "numbers", f"The number {number} is not in the tool results.")
    return PASS
