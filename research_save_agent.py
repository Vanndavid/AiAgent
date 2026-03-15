"""Minimal Research & Save agent loop (framework-free).

Focus: make the plumbing obvious.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Callable, Dict, List, TypedDict


class ToolCall(TypedDict):
    name: str
    arguments: Dict[str, Any]


class AgentDecision(TypedDict, total=False):
    thought: str
    action: str  # "tool_call" | "final_answer"
    tool_call: ToolCall
    final_answer: str


SYSTEM_PROMPT = """
You are a careful AI agent. You MUST output JSON only.

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
- If more info is needed, call a tool.
- Use only provided tools.
- Finish when goal is satisfied.
""".strip()


# ---------- Tools ----------

def tool_web_search(query: str) -> str:
    """Stub for a real search API call."""
    # Replace with real Google/SerpAPI request.
    return f"Top result summary for: {query}"


def tool_save_file(path: str, content: str) -> str:
    Path(path).write_text(content, encoding="utf-8")
    return f"Saved {len(content)} chars to {path}"


TOOL_REGISTRY: Dict[str, Callable[..., str]] = {
    "web_search": tool_web_search,
    "save_file": tool_save_file,
}


def get_tool_schemas() -> List[Dict[str, Any]]:
    return [
        {
            "name": "web_search",
            "description": "Search the web for a query.",
            "args": {"query": "string"},
        },
        {
            "name": "save_file",
            "description": "Save content to a local file.",
            "args": {"path": "string", "content": "string"},
        },
    ]


# ---------- Brain I/O ----------

def build_user_prompt(goal: str, scratchpad: List[str], tools: List[Dict[str, Any]]) -> str:
    return (
        f"GOAL:\n{goal}\n\n"
        f"TOOLS:\n{json.dumps(tools, indent=2)}\n\n"
        f"OBSERVATIONS SO FAR:\n" + "\n".join(scratchpad)
    )


def call_llm(system_prompt: str, user_prompt: str) -> AgentDecision:
    """Deterministic fake model so the loop is runnable without API keys.

    Replace this with your real LLM call. Keep the same JSON contract.
    """
    if "Saved" not in user_prompt:
        return {
            "thought": "Need info, then persist it.",
            "action": "tool_call",
            "tool_call": {
                "name": "web_search",
                "arguments": {"query": "minimal ReAct agent loop python"},
            },
        }
    return {
        "thought": "Research captured and saved.",
        "action": "final_answer",
        "final_answer": "Done. I researched and saved notes to notes.txt.",
    }


# ---------- Actuator ----------

def execute_tool(tool_call: ToolCall) -> str:
    name = tool_call["name"]
    args = tool_call.get("arguments", {})

    if name not in TOOL_REGISTRY:
        return f"ERROR: Unknown tool '{name}'"

    try:
        return TOOL_REGISTRY[name](**args)
    except TypeError as exc:
        return f"ERROR: Bad arguments for tool '{name}': {exc}"


# ---------- Agentic loop ----------

def run_agent(goal: str, max_steps: int = 6) -> str:
    scratchpad: List[str] = []

    for step in range(1, max_steps + 1):
        user_prompt = build_user_prompt(goal, scratchpad, get_tool_schemas())
        decision = call_llm(SYSTEM_PROMPT, user_prompt)

        action = decision.get("action")
        thought = decision.get("thought", "")
        scratchpad.append(f"Step {step} thought: {thought}")

        if action == "final_answer":
            return decision.get("final_answer", "No answer provided.")

        if action == "tool_call":
            result = execute_tool(decision["tool_call"])
            scratchpad.append(f"Step {step} observation: {result}")
            # A minimal self-trigger to demonstrate save flow in fake setup.
            if result.startswith("Top result"):
                save_result = execute_tool(
                    {
                        "name": "save_file",
                        "arguments": {"path": "notes.txt", "content": result},
                    }
                )
                scratchpad.append(f"Step {step} observation: {save_result}")
            continue

        return f"Stopped: invalid action '{action}'"

    return "Stopped: max steps reached"


if __name__ == "__main__":
    goal_text = "Research the ReAct loop and save concise notes to a file."
    print(run_agent(goal_text))
