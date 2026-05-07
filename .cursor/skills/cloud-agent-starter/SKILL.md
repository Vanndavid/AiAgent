---
name: cloud-agent-starter
description: Practical setup, run, and test workflows for Cursor Cloud agents working in this repository.
---

# Cloud Agent Starter

Use this skill when you need to run, test, or debug this repository from Cursor Cloud.

## Repository orientation

- This is a small Python standard-library project with an empty runtime `requirements.txt`; there is no database, external service, or frontend build step.
- The agent loop lives in `research_save_agent.py`.
- The local browser UI lives in `ui_app.py` and imports `run_agent_detailed(...)` from `research_save_agent.py`.
- `notes.txt` is the default output written by the fake save-file tool.
- `.venv/` may already exist in Cloud but is untracked; use it if available, or create it locally.

## Cloud setup

1. Start from the repo root:
   - `cd /workspace`
2. Use Python 3.12 or newer:
   - `python3 --version`
3. Create a virtualenv before installing dependencies:
   - `python3 -m venv .venv`
   - `.venv/bin/python -m pip install -r requirements.txt`
   - `source .venv/bin/activate` if you want `python` and `pip` to point at the virtualenv in an interactive shell.
4. No login is required for the default app because `call_llm(...)` is deterministic and does not call an external model provider.
5. If a future change wires in a real provider, store credentials in the Cloud environment rather than in the repo, then document the required variable names here.

## Agent loop area: `research_save_agent.py`

### Run it

- Execute the CLI example:
  - `python3 research_save_agent.py`
- Expected result:
  - stdout includes `Done. I researched and saved notes to notes.txt.`
  - `notes.txt` contains a deterministic fake search summary.

### Test it

- Syntax/import smoke test:
  - `python3 -m py_compile research_save_agent.py ui_app.py`
- Functional smoke test without starting the UI:
  - Run:

```bash
python3 - <<'PY'
from research_save_agent import run_agent_detailed
result = run_agent_detailed("Research the ReAct loop and save concise notes to a file.")
assert result["final_answer"] == "Done. I researched and saved notes to notes.txt."
assert any("Saved" in item for item in result["scratchpad"])
print(result["final_answer"])
PY
```

## UI area: `ui_app.py`

### Run it

- Start the local app from the repo root:
  - `python3 ui_app.py`
- Open the app at:
  - `http://localhost:8000`
- In Cloud, keep long-running servers in tmux so another agent or user can inspect the same session:
  - `SESSION_NAME="agent-ui"; tmux -f /exec-daemon/tmux.portal.conf has-session -t "=$SESSION_NAME" 2>/dev/null || tmux -f /exec-daemon/tmux.portal.conf new-session -d -s "$SESSION_NAME" -c "$PWD" -- "${SHELL:-bash}" -l`
  - `tmux -f /exec-daemon/tmux.portal.conf send-keys -t "$SESSION_NAME:0.0" 'python3 ui_app.py' C-m`

### Test it

- HTTP GET smoke test after the server starts:
  - `curl -fsS http://127.0.0.1:8000/`
  - Confirm the response contains `Research & Save Agent UI`.
- HTTP POST smoke test for the full browser-backed path:
  - `curl -fsS -X POST http://127.0.0.1:8000/run --data-urlencode 'goal=Research the ReAct loop and save concise notes to a file.'`
  - Confirm the response contains both `Final Answer` and `Agent Trace`.
- For UI changes, also use the `computerUse` subagent to submit the form in a browser and save a short walkthrough video.

## Feature flags, mocks, and external services

- There are currently no feature flags.
- The default LLM is already mocked by `call_llm(...)`; do not add provider credentials just to run the app.
- The web search tool is also mocked by `tool_web_search(...)`.
- If you need to test a real LLM or real search service later:
  - add the smallest possible provider adapter,
  - keep the existing fake path runnable without credentials,
  - document the environment variable names and a no-secret mock workflow in this skill.

## Common workflow checks

- Before editing, inspect tracked files:
  - `git status --short`
  - `git ls-files`
- Before committing, run the workflows that touch your changed area:
  - CLI/agent changes: `python3 -m py_compile research_save_agent.py ui_app.py` and the functional smoke test above.
  - UI changes: compile check, start `python3 ui_app.py`, run GET and POST curl checks, then manually verify in the browser.
  - Documentation-only changes: read the changed Markdown and run any commands that the documentation claims should work.
- Avoid committing generated `notes.txt` changes unless the task is specifically about the fixture/output file.

## Updating this skill

- Update this skill whenever you discover a repeatable setup step, test command, environment requirement, feature flag, or debugging trick that would save the next Cloud agent time.
- Keep additions concrete: include exact commands, expected output, and which codebase area they apply to.
- Remove stale instructions when code paths change; this file should stay short enough to scan before implementation work.
