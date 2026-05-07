# AGENTS.md

## Cursor Cloud specific instructions

This is a minimal Python project (stdlib only, zero pip dependencies) implementing a ReAct AI agent loop.

### Services

| Service | Command | Port |
|---------|---------|------|
| CLI agent | `python research_save_agent.py` | N/A |
| Web UI | `python ui_app.py` | 8000 |

### Development notes

- **No external dependencies**: The project uses only Python standard library. No `pip install` step is needed for the application itself.
- **Virtual environment**: A venv at `.venv/` is recommended. Create with `python3 -m venv .venv && source .venv/bin/activate`.
- **Linting**: Use `ruff check *.py` (install ruff in the venv with `pip install ruff`).
- **No test suite**: The project has no automated test framework. Verify correctness by running the CLI agent and checking the web UI.
- **Deterministic fake LLM**: By default `call_llm()` in `research_save_agent.py` is a deterministic stub—no API keys required.
- **File output**: The agent writes `notes.txt` in the working directory as part of its "save_file" tool action.
- **Web UI startup**: `python ui_app.py` binds to `0.0.0.0:8000`. Test with `curl -X POST http://localhost:8000/run -d "goal=your+goal+here"`.
