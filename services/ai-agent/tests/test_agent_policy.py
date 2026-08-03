"""Unit tests for deterministic agent tool selection (no network)."""

from __future__ import annotations

from app.agent import call_llm


def test_list_applications_for_pipeline_goal():
    decision = call_llm("", "", "List my job applications", [])
    assert decision["action"] == "tool_call"
    assert decision["tool_call"]["name"] == "list_applications"


def test_rag_retrieve_for_similar_roles():
    decision = call_llm("", "", "Retrieve similar backend engineer roles from RAG", [])
    assert decision["action"] == "tool_call"
    assert decision["tool_call"]["name"] == "rag_retrieve"


def test_create_application_parses_company_role():
    goal = "Create an application at Acme Corp as Backend Engineer"
    decision = call_llm("", "", goal, [])
    assert decision["action"] == "tool_call"
    assert decision["tool_call"]["name"] == "create_application"
    args = decision["tool_call"]["arguments"]
    assert args["company"] == "Acme Corp"
    assert "Backend Engineer" in args["role"]


def test_research_uses_web_search():
    decision = call_llm("", "", "Research the ReAct loop and save notes", [])
    assert decision["action"] == "tool_call"
    assert decision["tool_call"]["name"] == "web_search"


def test_final_answer_after_tools():
    scratchpad = [
        "Step 1 tool=list_applications args={}",
        "Step 1 observation: []",
    ]
    decision = call_llm("", "", "List my job applications", scratchpad)
    assert decision["action"] == "final_answer"
