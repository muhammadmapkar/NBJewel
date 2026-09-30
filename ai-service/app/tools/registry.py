"""Role: The tool registry. It lists tool declarations for the language model
and runs a tool by name. run_tool never raises: every failure becomes a
result with status "error" so the caller always gets the same shape.
"""

import logging
from typing import Any

from pydantic import ValidationError

from app.config import get_settings
from app.tools.api_client import ApiError, ApiUnavailable, ReadOnlyApiClient, ReadOnlyViolation
from app.tools.catalog_tools import ALL_TOOLS, Tool, ToolResult, error

logger = logging.getLogger(__name__)


def _simplify_schema(node: Any) -> Any:
    """Turns a pydantic JSON schema into the plain form function calling accepts.

    Removes titles and additionalProperties, and turns "string or null" into "string".
    """
    if isinstance(node, list):
        return [_simplify_schema(n) for n in node]
    if not isinstance(node, dict):
        return node
    if "anyOf" in node:
        options = [o for o in node["anyOf"] if o.get("type") != "null"]
        if len(options) == 1:
            merged = {**{k: v for k, v in node.items() if k != "anyOf"}, **options[0]}
            return _simplify_schema(merged)
    cleaned: dict[str, Any] = {}
    for key, value in node.items():
        if key in ("title", "additionalProperties") or (key == "default" and value is None):
            continue
        cleaned[key] = _simplify_schema(value) if key != "properties" else {k: _simplify_schema(v) for k, v in value.items()}
    return cleaned


class ToolRegistry:
    def __init__(self, client: ReadOnlyApiClient | None = None, tools: list[Tool] | None = None) -> None:
        self._client = client or ReadOnlyApiClient(get_settings().nbjewel_api_url)
        self._tools = {t.name: t for t in (tools or ALL_TOOLS)}

    def declarations(self) -> list[dict[str, Any]]:
        """Name, description and JSON schema of every tool."""
        return [{
            "name": t.name,
            "description": t.description,
            "parameters": _simplify_schema(t.args_model.model_json_schema()),
        } for t in self._tools.values()]

    def run_tool(self, name: str, args: dict[str, Any] | None = None) -> ToolResult:
        tool = self._tools.get(name)
        if tool is None:
            return error(f"Unknown tool '{name}'.")
        if args is not None and not isinstance(args, dict):
            return error("Tool arguments must be an object.")
        try:
            parsed = tool.args_model.model_validate(args or {})
        except ValidationError as problem:
            details = "; ".join(f"{'.'.join(str(p) for p in e['loc']) or 'arguments'}: {e['msg']}" for e in problem.errors())
            return error(f"Invalid arguments. {details}")
        try:
            return tool.handler(self._client, parsed)
        except ApiUnavailable as problem:
            return error(str(problem))
        except ApiError as problem:
            return error(str(problem))
        except ReadOnlyViolation as problem:
            return error(str(problem))
        except Exception:  # noqa: BLE001 - callers must never see raw exceptions
            logger.exception("Tool %s failed", name)
            return error("The tool failed unexpectedly.")


_default: ToolRegistry | None = None


def run_tool(name: str, args: dict[str, Any] | None = None) -> ToolResult:
    """Runs a tool with the default registry, which talks to NBJEWEL_API_URL."""
    global _default
    if _default is None:
        _default = ToolRegistry()
    return _default.run_tool(name, args)


def tool_declarations() -> list[dict[str, Any]]:
    global _default
    if _default is None:
        _default = ToolRegistry()
    return _default.declarations()
