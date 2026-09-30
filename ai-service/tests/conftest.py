"""Role: pytest setup for the test suite. It skips "live" tests unless RUN_LIVE=1 and
GEMINI_API_KEY are set, and writes tests/report.md after a live run.
"""

import datetime
from pathlib import Path

import pytest

from tests import live_support

REPORT_PATH = Path(__file__).parent / "report.md"


def pytest_configure(config: pytest.Config) -> None:
    config.addinivalue_line("markers", "live: uses the real Gemini model. Runs only with RUN_LIVE=1 and GEMINI_API_KEY.")


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    if live_support.live_enabled():
        return
    skip = pytest.mark.skip(reason="Live test. Set RUN_LIVE=1 and GEMINI_API_KEY to run it.")
    for item in items:
        if "live" in item.keywords:
            item.add_marker(skip)


def pytest_sessionfinish(session: pytest.Session, exitstatus: int) -> None:
    reports = [r for r in live_support.REPORTS.values() if r.runs]
    if not reports:
        return
    lines = [
        "# Live test report", "",
        f"- Model: {live_support.model_name() or 'unknown'}",
        f"- Date: {datetime.date.today().isoformat()}",
        f"- Runs per question: {live_support.repeats()}", "",
        "| Set | Runs | Passed | Pass rate | Target | Result |", "|---|---|---|---|---|---|",
    ]
    for r in reports:
        lines.append(f"| {r.name} | {r.runs} | {r.passed} | {r.rate:.0%} | {r.target:.0%} | {'PASS' if r.rate >= r.target else 'FAIL'} |")
    lines += ["", "## Failed questions", ""]
    failed = [(r.name, q, a) for r in reports for q, a in r.failures]
    if not failed:
        lines.append("None.")
    for name, question, answer in failed:
        lines += [f"- **{name}**: {question}", f"  - Got: {answer}"]
    REPORT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")
