# Minimalist AI Agent (No LangChain/CrewAI)

This repo contains a from-scratch Python example of an **agentic loop** for a "Research & Save" workflow.

## ReAct in one minute

**ReAct = Reason + Act + Observe**

1. **Reason**: The model thinks about the goal and chooses the next step.
2. **Act**: The model requests a tool call (search, save file, etc.).
3. **Observe**: Tool output is fed back to the model.
4. Repeat until the model emits `final_answer`.

In this repo, the LLM must always emit strict JSON so your loop can parse decisions deterministically.

## Phases

### Phase 1 — Brain setup
See `research_save_agent.py` for:
- `SYSTEM_PROMPT` that enforces JSON shape.
- `build_user_prompt(...)` that injects goal + scratchpad + tool schema.

### Phase 2 — The loop
See `run_agent(...)`:
- `while` loop tracks `scratchpad` and `steps`.
- Calls `call_llm(...)` each turn.
- Executes tool requests and appends observations.
- Stops on `final_answer`, `max_steps`, or tool error policy.

### Phase 3 — Tool integration
See:
- `TOOL_REGISTRY`: maps tool names to Python callables.
- `get_tool_schemas()`: exposes allowed tool names/arguments to the model.
- `execute_tool(...)`: validates + dispatches tool calls.

## Run

```bash
python research_save_agent.py
```

By default this runs with a deterministic fake LLM so you can see the plumbing clearly.
To use a real model, replace `call_llm(...)` with your provider call and keep the same JSON contract.
