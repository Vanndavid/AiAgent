"""HTTP tools that call the Job Assistant API and RAG service."""

from __future__ import annotations

import json
import os
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


def _api_base() -> str:
    return os.environ.get("JOB_ASSISTANT_API_BASE_URL", "http://127.0.0.1:5287").rstrip("/")


def _rag_base() -> str:
    return os.environ.get("RAG_API_BASE_URL", "http://127.0.0.1:8001").rstrip("/")


def _http_json(method: str, url: str, payload: dict[str, Any] | None = None, timeout: float = 15.0) -> Any:
    data = None
    headers = {"Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(url, data=data, headers=headers, method=method)
    try:
        with urlopen(req, timeout=timeout) as resp:
            body = resp.read().decode("utf-8")
            return json.loads(body) if body else None
    except HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        # Prefer JSON error payloads; otherwise truncate HTML/stack traces.
        try:
            parsed = json.loads(detail)
            return {"error": f"HTTP {exc.code}", "detail": parsed, "url": url}
        except json.JSONDecodeError:
            first_line = detail.strip().splitlines()[0] if detail.strip() else ""
            return {
                "error": f"HTTP {exc.code}",
                "detail": (first_line or detail)[:400],
                "url": url,
            }
    except URLError as exc:
        return {"error": "unreachable", "detail": str(exc.reason), "url": url}
    except TimeoutError:
        return {"error": "timeout", "detail": f"Timed out calling {url}", "url": url}
    except json.JSONDecodeError as exc:
        return {"error": "invalid_json", "detail": str(exc), "url": url}


def tool_list_applications() -> str:
    """List job applications from the .NET API."""
    result = _http_json("GET", f"{_api_base()}/api/applications")
    return json.dumps(result, indent=2, default=str)


def tool_get_application(application_id: str) -> str:
    """Fetch one application by id."""
    result = _http_json("GET", f"{_api_base()}/api/applications/{application_id}")
    return json.dumps(result, indent=2, default=str)


def tool_create_application(
    company: str,
    role: str,
    status: str = "saved",
    notes: str | None = None,
) -> str:
    """Create a job application via the .NET API."""
    payload: dict[str, Any] = {
        "company": company,
        "role": role,
        "status": status,
    }
    if notes:
        payload["notes"] = notes
    result = _http_json("POST", f"{_api_base()}/api/applications", payload)
    return json.dumps(result, indent=2, default=str)


def tool_rag_retrieve(query: str, k: int = 5) -> str:
    """Retrieve similar job description chunks from the RAG service."""
    result = _http_json(
        "POST",
        f"{_rag_base()}/rag/query",
        {"query": query, "k": max(1, min(int(k), 20))},
    )
    return json.dumps(result, indent=2, default=str)


def tool_rag_ingest(text: str, company: str | None = None, role: str | None = None) -> str:
    """Ingest a job description (or notes) into the RAG index."""
    metadata: dict[str, Any] = {"source": "agent"}
    if company:
        metadata["company"] = company
    if role:
        metadata["role"] = role
    result = _http_json(
        "POST",
        f"{_rag_base()}/rag/ingest",
        {"texts": [text], "metadatas": [metadata]},
    )
    return json.dumps(result, indent=2, default=str)
