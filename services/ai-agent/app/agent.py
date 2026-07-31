"""Minimal Research & Save agent loop (framework-free).

Uses a deterministic fake LLM by default so the loop is runnable without API keys.
Replace `call_llm` with a real provider while keeping the JSON contract.

Tools can reach the Job Assistant API (applications) and the RAG service.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any, Callable, Dict, List, TypedDict

from .tools import (
    tool_create_application,
    tool_get_application,
    tool_list_applications,
    tool_rag_ingest,
    tool_rag_retrieve,
)


class ToolCall(TypedDict):
    name: str
    arguments: Dict[str, Any]


class AgentDecision(TypedDict, total=False):
    thought: str
    action: str  # "tool_call" | "final_answer"
    tool_call: ToolCall
    final_answer: str


class AgentRunResult(TypedDict):
    final_answer: str
    scratchpad: List[str]
    tools_used: List[str]


SYSTEM_PROMPT = """
You are Job Assistant's careful AI agent. You MUST output JSON only.

Valid output schema:
{
  "thought": "short private reasoning summary",
  "action": "tool_call" | "final_answer",
  "tool_call": {
    "name": "<tool name>",
    "arguments": {"arg": "value"}
  },
  "final_answer": "required when action=final_answer"
}

Rules:
- Never output markdown.
- Prefer applications + RAG tools when the goal is about jobs or applications.
- Use only provided tools.
- Finish when goal is satisfied.
""".strip()

_NOTES_DIR = Path(__file__).resolve().parent.parent / "var" / "notes"
_NOTES_DIR.mkdir(parents=True, exist_ok=True)


def tool_web_search(query: str) -> str:
    """Stub for a real search API call."""
    return f"Top result summary for: {query}"


def tool_save_file(path: str, content: str) -> str:
    target = Path(path)
    if not target.is_absolute():
        target = _NOTES_DIR / target.name
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")
    return f"Saved {len(content)} chars to {target}"


TOOL_REGISTRY: Dict[str, Callable[..., str]] = {
    "web_search": tool_web_search,
    "save_file": tool_save_file,
    "list_applications": tool_list_applications,
    "get_application": tool_get_application,
    "create_application": tool_create_application,
    "rag_retrieve": tool_rag_retrieve,
    "rag_ingest": tool_rag_ingest,
}


def get_tool_schemas() -> List[Dict[str, Any]]:
    return [
        {
            "name": "list_applications",
            "description": "List saved job applications from Postgres via the API.",
            "args": {},
        },
        {
            "name": "get_application",
            "description": "Get one job application by UUID.",
            "args": {"application_id": "string"},
        },
        {
            "name": "create_application",
            "description": "Create a job application (company, role, optional status/notes).",
            "args": {
                "company": "string",
                "role": "string",
                "status": "string?",
                "notes": "string?",
            },
        },
        {
            "name": "rag_retrieve",
            "description": "Retrieve similar job description chunks from the RAG index.",
            "args": {"query": "string", "k": "number?"},
        },
        {
            "name": "rag_ingest",
            "description": "Ingest a job description or notes into the RAG index.",
            "args": {"text": "string", "company": "string?", "role": "string?"},
        },
        {
            "name": "web_search",
            "description": "Search the web for a query (stub).",
            "args": {"query": "string"},
        },
        {
            "name": "save_file",
            "description": "Save content to a local notes file.",
            "args": {"path": "string", "content": "string"},
        },
    ]


def build_user_prompt(goal: str, scratchpad: List[str], tools: List[Dict[str, Any]]) -> str:
    return (
        f"GOAL:\n{goal}\n\n"
        f"TOOLS:\n{json.dumps(tools, indent=2)}\n\n"
        f"OBSERVATIONS SO FAR:\n" + ("\n".join(scratchpad) if scratchpad else "(none)")
    )


def _scratchpad_mentions(scratchpad: List[str], tool_name: str) -> bool:
    needle = f"tool={tool_name}"
    return any(needle in line for line in scratchpad)


def _extract_company_role(goal: str) -> tuple[str, str]:
    """Best-effort parse of 'Company / Role' style phrases from the goal."""
    # Patterns: "at Acme as Backend Engineer", "Acme Corp Backend Engineer", "company X role Y"
    m = re.search(
        r"(?:at|for)\s+([A-Za-z0-9][\w .&-]{1,40}?)\s+(?:as|for)\s+(?:a\s+|an\s+)?(.+?)(?:\.|$)",
        goal,
        re.I,
    )
    if m:
        return m.group(1).strip(" ,."), m.group(2).strip(" ,.")

    m = re.search(
        r"company\s*[:=]\s*([^,;]+).*?role\s*[:=]\s*([^,;]+)",
        goal,
        re.I | re.S,
    )
    if m:
        return m.group(1).strip(), m.group(2).strip()

    return "Example Corp", "Software Engineer"


def _extract_query(goal: str) -> str:
    m = re.search(r"(?:similar to|about|for|regarding|retrieve|search)\s+(.+)$", goal, re.I)
    if m:
        return m.group(1).strip(" .")[:200]
    return goal.strip()[:200]


def call_llm(system_prompt: str, user_prompt: str, goal: str, scratchpad: List[str]) -> AgentDecision:
    """Deterministic fake model so the loop is runnable without API keys.

    Chooses tools based on goal keywords and prior observations.
    """
    _ = system_prompt, user_prompt
    goal_l = goal.lower()

    wants_apps = any(
        w in goal_l
        for w in ("application", "applications", "pipeline", "tracker", "my jobs")
    )
    wants_create = any(w in goal_l for w in ("create", "add", "save application", "track"))
    wants_rag = any(
        w in goal_l
        for w in ("rag", "retrieve", "similar", "job description", "jd", "embedding", "ingest")
    )
    wants_research = any(w in goal_l for w in ("research", "search", "react", "notes"))

    if wants_create and wants_apps and not _scratchpad_mentions(scratchpad, "create_application"):
        company, role = _extract_company_role(goal)
        return {
            "thought": "Create the requested job application via the API.",
            "action": "tool_call",
            "tool_call": {
                "name": "create_application",
                "arguments": {
                    "company": company,
                    "role": role,
                    "status": "saved",
                    "notes": f"Created by agent for goal: {goal[:120]}",
                },
            },
        }

    if wants_apps and not _scratchpad_mentions(scratchpad, "list_applications"):
        return {
            "thought": "List current applications from the API.",
            "action": "tool_call",
            "tool_call": {"name": "list_applications", "arguments": {}},
        }

    if wants_rag and "ingest" in goal_l and not _scratchpad_mentions(scratchpad, "rag_ingest"):
        return {
            "thought": "Ingest the provided text into the RAG index.",
            "action": "tool_call",
            "tool_call": {
                "name": "rag_ingest",
                "arguments": {
                    "text": goal,
                    "company": "Agent",
                    "role": "Ingested note",
                },
            },
        }

    if wants_rag and not _scratchpad_mentions(scratchpad, "rag_retrieve"):
        return {
            "thought": "Retrieve similar job chunks from RAG.",
            "action": "tool_call",
            "tool_call": {
                "name": "rag_retrieve",
                "arguments": {"query": _extract_query(goal), "k": 3},
            },
        }

    # Default research path (backward compatible).
    if wants_research or (not wants_apps and not wants_rag):
        if not _scratchpad_mentions(scratchpad, "web_search") and "Saved" not in "\n".join(
            scratchpad
        ):
            return {
                "thought": "Need info, then persist it.",
                "action": "tool_call",
                "tool_call": {
                    "name": "web_search",
                    "arguments": {"query": goal[:120] or "minimal ReAct agent loop python"},
                },
            }
        if _scratchpad_mentions(scratchpad, "web_search") and not _scratchpad_mentions(
            scratchpad, "save_file"
        ):
            # Will be handled by execute path; still allow explicit save.
            search_line = next((s for s in scratchpad if "Top result" in s), goal)
            return {
                "thought": "Persist research notes.",
                "action": "tool_call",
                "tool_call": {
                    "name": "save_file",
                    "arguments": {"path": "notes.txt", "content": search_line},
                },
            }

    # Summarize what we observed.
    summary_bits = [line for line in scratchpad if "observation:" in line][-4:]
    summary = "\n".join(summary_bits) if summary_bits else "No tool observations."
    return {
        "thought": "Goal addressed with available tools.",
        "action": "final_answer",
        "final_answer": f"Done.\n\n{summary}",
    }


def execute_tool(tool_call: ToolCall) -> str:
    name = tool_call["name"]
    args = tool_call.get("arguments", {})

    if name not in TOOL_REGISTRY:
        return f"ERROR: Unknown tool '{name}'"

    try:
        return TOOL_REGISTRY[name](**args)
    except TypeError as exc:
        return f"ERROR: Bad arguments for tool '{name}': {exc}"


def run_agent_detailed(goal: str, max_steps: int = 6) -> AgentRunResult:
    scratchpad: List[str] = []
    tools_used: List[str] = []

    for step in range(1, max_steps + 1):
        user_prompt = build_user_prompt(goal, scratchpad, get_tool_schemas())
        decision = call_llm(SYSTEM_PROMPT, user_prompt, goal, scratchpad)

        action = decision.get("action")
        thought = decision.get("thought", "")
        scratchpad.append(f"Step {step} thought: {thought}")

        if action == "final_answer":
            return {
                "final_answer": decision.get("final_answer", "No answer provided."),
                "scratchpad": scratchpad,
                "tools_used": tools_used,
            }

        if action == "tool_call":
            tool_call = decision["tool_call"]
            name = tool_call["name"]
            tools_used.append(name)
            scratchpad.append(f"Step {step} tool={name} args={json.dumps(tool_call.get('arguments', {}))}")
            result = execute_tool(tool_call)
            scratchpad.append(f"Step {step} observation: {result}")

            # Backward-compatible auto-save after stub web search when research-only.
            if name == "web_search" and result.startswith("Top result"):
                if not _scratchpad_mentions(scratchpad, "save_file"):
                    save_result = execute_tool(
                        {
                            "name": "save_file",
                            "arguments": {"path": "notes.txt", "content": result},
                        }
                    )
                    tools_used.append("save_file")
                    scratchpad.append(f"Step {step} tool=save_file args={{\"path\": \"notes.txt\"}}")
                    scratchpad.append(f"Step {step} observation: {save_result}")
            continue

        return {
            "final_answer": f"Stopped: invalid action '{action}'",
            "scratchpad": scratchpad,
            "tools_used": tools_used,
        }

    return {
        "final_answer": "Stopped: max steps reached",
        "scratchpad": scratchpad,
        "tools_used": tools_used,
    }


def run_agent(goal: str, max_steps: int = 6) -> str:
    return run_agent_detailed(goal, max_steps)["final_answer"]
