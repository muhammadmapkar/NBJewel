"""Role: Reads the AI service settings from environment variables.

The Gemini key is held as a SecretStr, so printing or logging the settings
object never shows its value.
"""

from functools import lru_cache

from pydantic import SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    gemini_api_key: SecretStr | None = None
    gemini_model: str = ""
    nbjewel_api_url: str = "http://localhost:5155"

    def safe_summary(self) -> dict[str, object]:
        """Settings that are safe to log. The key is reported only as present or missing."""
        return {
            "gemini_api_key_set": self.gemini_api_key is not None and bool(self.gemini_api_key.get_secret_value()),
            "gemini_model": self.gemini_model,
            "nbjewel_api_url": self.nbjewel_api_url,
        }


@lru_cache
def get_settings() -> Settings:
    return Settings()
