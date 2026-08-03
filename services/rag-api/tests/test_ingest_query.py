"""RAG ingest → query round-trip using an isolated store directory."""

from __future__ import annotations

import importlib
import json
import os
import sys
from pathlib import Path


def test_ingest_and_query_roundtrip(tmp_path: Path):
    store = tmp_path / "faiss"
    store.mkdir()
    os.environ["FAISS_STORE_DIR"] = str(store)

    # Reload module so it picks up the temp store dir.
    for name in list(sys.modules):
        if name == "app.main" or name.startswith("app.main."):
            del sys.modules[name]

    root = str(Path(__file__).resolve().parents[1])
    if root not in sys.path:
        sys.path.insert(0, root)
    main = importlib.import_module("app.main")

    before = main.health()["index_vectors"]
    ingested = main.ingest(
        main.IngestRequest(
            texts=["Zephyr Labs seeks a Rust systems engineer for networking."],
            metadatas=[{"company": "Zephyr Labs", "role": "Systems Engineer"}],
        )
    )
    assert ingested.ingested == 1
    assert ingested.total_vectors == before + 1

    docs = json.loads((store / "jobs.docs.json").read_text(encoding="utf-8"))
    assert any("Zephyr" in d["page_content"] for d in docs)

    result = main.query(main.QueryRequest(query="Rust systems engineer networking", k=3))
    assert result["k"] >= 1
    assert len(result["results"]) >= 1
