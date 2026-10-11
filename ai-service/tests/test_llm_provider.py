"""Role: Tests for the Gemini provider using a fake SDK client, so no API key
and no network are needed. Also checks that the key never appears in output.
"""

from types import SimpleNamespace

import pytest
from google.genai import types

from app.config import Settings
from app.llm.gemini import GeminiProvider
from app.llm.provider import LLMError, Message, ToolCall

TOOLS = [{"name": "list_categories", "description": "List categories.", "parameters": {"type": "object", "properties": {}}}]


class FakeClient:
    def __init__(self, parts):
        self.captured = {}
        content = types.Content(role="model", parts=parts)
        response = types.GenerateContentResponse(candidates=[types.Candidate(content=content)])
        self.models = SimpleNamespace(generate_content=lambda **kw: self._call(response, kw))

    def _call(self, response, kwargs):
        self.captured = kwargs
        return response


def test_text_response_and_request_shape():
    client = FakeClient([types.Part(text="Hello")])
    provider = GeminiProvider(api_key="", model="some-model", client=client)
    reply = provider.generate([Message("system", "Be brief."), Message("user", "Hi")], TOOLS, temperature=0.2)
    assert reply.text == "Hello" and not reply.has_tool_calls
    assert client.captured["model"] == "some-model"
    config = client.captured["config"]
    assert config.system_instruction == "Be brief." and config.temperature == 0.2
    assert config.automatic_function_calling.disable is True
    declaration = config.tools[0].function_declarations[0]
    assert declaration.name == "list_categories"
    assert [c.role for c in client.captured["contents"]] == ["user"]


def test_tool_call_response_and_history_round_trip():
    client = FakeClient([types.Part.from_function_call(name="list_categories", args={})])
    provider = GeminiProvider(api_key="", model="some-model", client=client)
    reply = provider.generate([Message("user", "What categories exist?")], TOOLS)
    assert reply.has_tool_calls and reply.text is None
    call = reply.tool_calls[0]
    assert call.name == "list_categories" and call.args == {}

    history = [
        Message("user", "What categories exist?"),
        Message("assistant", tool_calls=(call,)),
        Message("tool", tool_name="list_categories", tool_result={"status": "ok"}),
    ]
    provider.generate(history, TOOLS)
    roles = [c.role for c in client.captured["contents"]]
    assert roles == ["user", "model", "user"]
    assert client.captured["contents"][2].parts[0].function_response.name == "list_categories"


def test_assistant_call_without_saved_part_is_rebuilt():
    client = FakeClient([types.Part(text="ok")])
    provider = GeminiProvider(api_key="", model="m", client=client)
    history = [Message("user", "x"), Message("assistant", tool_calls=(ToolCall("list_categories", {}),))]
    provider.generate(history, TOOLS)
    assert client.captured["contents"][1].parts[0].function_call.name == "list_categories"


def test_missing_model_or_key_raises_clear_error():
    with pytest.raises(LLMError, match="GEMINI_MODEL"):
        GeminiProvider(api_key="anything", model="")
    with pytest.raises(LLMError, match="GEMINI_API_KEY"):
        GeminiProvider(api_key="", model="some-model")


def test_sdk_failure_becomes_llm_error_without_details():
    def boom(**kwargs):
        raise RuntimeError("secret-looking detail")
    client = SimpleNamespace(models=SimpleNamespace(generate_content=boom))
    provider = GeminiProvider(api_key="", model="m", client=client)
    with pytest.raises(LLMError) as caught:
        provider.generate([Message("user", "hi")], TOOLS)
    assert "secret-looking detail" not in str(caught.value)


def test_settings_never_show_the_key():
    settings = Settings(gemini_api_key="not-a-real-key-123", gemini_model="m", _env_file=None)
    assert "not-a-real-key-123" not in repr(settings)
    assert "not-a-real-key-123" not in str(settings.safe_summary())
    assert settings.safe_summary()["gemini_api_key_set"] is True


# ---------- thinking level ----------

def sent_config(client):
    return client.captured["config"]


def test_thinking_level_defaults_to_low_and_is_sent():
    client = FakeClient([types.Part(text="ok")])
    GeminiProvider(api_key="", model="m", client=client).generate([Message("user", "hi")], TOOLS)
    assert sent_config(client).thinking_config.thinking_level == types.ThinkingLevel.LOW


@pytest.mark.parametrize("level,expected", [("minimal", "MINIMAL"), ("MEDIUM", "MEDIUM"), ("high", "HIGH")])
def test_thinking_level_can_be_changed(level, expected):
    client = FakeClient([types.Part(text="ok")])
    GeminiProvider(api_key="", model="m", client=client, thinking_level=level).generate([Message("user", "hi")], TOOLS)
    assert sent_config(client).thinking_config.thinking_level.value == expected


def test_empty_thinking_level_leaves_the_model_default():
    client = FakeClient([types.Part(text="ok")])
    GeminiProvider(api_key="", model="m", client=client, thinking_level="").generate([Message("user", "hi")], TOOLS)
    assert sent_config(client).thinking_config is None


def test_unknown_thinking_level_is_a_clear_error():
    with pytest.raises(LLMError, match="GEMINI_THINKING_LEVEL"):
        GeminiProvider(api_key="", model="m", client=FakeClient([]), thinking_level="extreme")


class RejectingThenOk:
    """First call fails like a model that does not accept the thinking setting."""

    def __init__(self, code=400, message="Thinking level is not supported for this model."):
        self.configs, self.code, self.message = [], code, message
        response = types.GenerateContentResponse(candidates=[types.Candidate(content=types.Content(role="model", parts=[types.Part(text="fine")]))])

        def generate_content(**kw):
            self.configs.append(kw["config"])
            if len(self.configs) == 1:
                error = RuntimeError(self.message)
                error.code = self.code
                raise error
            return response
        self.models = SimpleNamespace(generate_content=generate_content)


def test_model_that_rejects_thinking_is_retried_once_without_it_and_remembered():
    client = RejectingThenOk()
    provider = GeminiProvider(api_key="", model="m", client=client)
    assert provider.generate([Message("user", "hi")], TOOLS).text == "fine"
    assert client.configs[0].thinking_config is not None and client.configs[1].thinking_config is None
    provider.generate([Message("user", "again")], TOOLS)
    assert client.configs[2].thinking_config is None  # not sent again


def test_other_client_errors_are_not_retried():
    client = RejectingThenOk(code=429, message="Quota exceeded for thinking requests")
    with pytest.raises(LLMError):
        GeminiProvider(api_key="", model="m", client=client).generate([Message("user", "hi")], TOOLS)
    assert len(client.configs) == 1


def test_settings_default_thinking_level_is_low_and_loads_from_env(monkeypatch):
    assert Settings(_env_file=None).gemini_thinking_level == "low"
    monkeypatch.setenv("GEMINI_THINKING_LEVEL", "minimal")
    assert Settings(_env_file=None).gemini_thinking_level == "minimal"
