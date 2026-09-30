"""Role: The only way tools reach the .NET API. It is read-only by design:
any HTTP method other than GET raises an error before a request is sent.
"""

from typing import Any

import httpx

TIMEOUT_SECONDS = 10.0


class ReadOnlyViolation(Exception):
    """Raised when code tries to use an HTTP method other than GET."""


class ApiUnavailable(Exception):
    """Raised when the API cannot be reached or times out."""


class ApiError(Exception):
    """Raised when the API answers with an unexpected error status."""


def _guard(request: httpx.Request) -> None:
    # Runs for every request on the underlying client, so nothing can bypass it.
    if request.method.upper() != "GET":
        raise ReadOnlyViolation(f"Only GET is allowed, not {request.method}.")


class ReadOnlyApiClient:
    def __init__(self, base_url: str, transport: httpx.BaseTransport | None = None) -> None:
        self._http = httpx.Client(
            base_url=base_url.rstrip("/"),
            timeout=TIMEOUT_SECONDS,
            transport=transport,
            event_hooks={"request": [_guard]},
        )

    def request(self, method: str, path: str, params: dict[str, Any] | None = None) -> tuple[int, Any]:
        """Sends one request and returns (status code, parsed body).

        404 and 422 are returned so a tool can report "not found". Connection
        problems and other error statuses raise ApiUnavailable or ApiError.
        """
        if method.upper() != "GET":
            raise ReadOnlyViolation(f"Only GET is allowed, not {method}.")
        clean = {k: v for k, v in (params or {}).items() if v is not None}
        try:
            response = self._http.request("GET", path, params=clean)
        except (httpx.TransportError, httpx.TimeoutException) as error:
            raise ApiUnavailable("The ERP API is not reachable.") from error
        if response.status_code in (404, 422):
            return response.status_code, _body(response)
        if response.status_code >= 400:
            raise ApiError(f"The ERP API returned status {response.status_code}.")
        return response.status_code, _body(response)

    def get(self, path: str, params: dict[str, Any] | None = None) -> tuple[int, Any]:
        return self.request("GET", path, params)

    def close(self) -> None:
        self._http.close()


def _body(response: httpx.Response) -> Any:
    try:
        return response.json()
    except ValueError:
        return response.text
