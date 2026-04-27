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
See `run_agent_detailed(...)`:
- loop tracks `scratchpad` and `steps`.
- calls `call_llm(...)` each turn.
- executes tool requests and appends observations.
- stops on `final_answer`, `max_steps`, or invalid action.

### Phase 3 — Tool integration
See:
- `TOOL_REGISTRY`: maps tool names to Python callables.
- `get_tool_schemas()`: exposes allowed tool names/arguments to the model.
- `execute_tool(...)`: validates + dispatches tool calls.

## Step-by-step: how to run

1. **Go to the project folder**
   ```bash
   cd /workspace/AiAgent
   ```

2. **(Recommended) Create and activate a virtual environment**
   ```bash
   python -m venv .venv
   source .venv/bin/activate
   ```

3. **Run the CLI example**
   ```bash
   python research_save_agent.py
   ```

4. **Run the local UI**
   ```bash
   python ui_app.py
   ```

5. **Open the app in your browser**
   - `http://localhost:8000`
   - Enter a goal and inspect:
- Final answer
- Full agent trace (Reason/Observation log)

By default this uses a deterministic fake `call_llm(...)` so the plumbing is easy to inspect.
To use a real model, replace `call_llm(...)` with your provider call and keep the same JSON contract.
