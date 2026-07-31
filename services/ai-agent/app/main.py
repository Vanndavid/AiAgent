from pydantic import BaseModel, Field
from fastapi import FastAPI

from .agent import run_agent_detailed

app = FastAPI(title="Job Assistant AI Agent", version="0.2.0")


class AgentRunRequest(BaseModel):
    goal: str = Field(min_length=1, max_length=4000)
    max_steps: int = Field(default=6, ge=1, le=20)


@app.get("/health")
def health():
    return {
        "status": "ok",
        "service": "ai-agent",
        "tools": [
            "list_applications",
            "get_application",
            "create_application",
            "rag_retrieve",
            "rag_ingest",
            "web_search",
            "save_file",
        ],
    }


@app.post("/agent/run")
def agent_run(body: AgentRunRequest):
    """Run the ReAct agent loop (applications + RAG tools when relevant)."""
    result = run_agent_detailed(body.goal.strip(), max_steps=body.max_steps)
    return {
        "goal": body.goal.strip(),
        "final_answer": result["final_answer"],
        "scratchpad": result["scratchpad"],
        "tools_used": result["tools_used"],
    }
