"""Job Assistant RAG API — FAISS + LangChain with text ingest/query."""

from __future__ import annotations

import json
import os
from importlib import metadata
from pathlib import Path
from typing import Annotated, Any

import faiss
from fastapi import FastAPI, HTTPException
from langchain_community.embeddings import FakeEmbeddings
from langchain_community.vectorstores import FAISS
from langchain_core.documents import Document
from pydantic import BaseModel, Field

app = FastAPI(title="Job Assistant RAG", version="0.2.0")

_DIMENSION = 64
_default_dir = Path(__file__).resolve().parent.parent / "var" / "faiss"
_store_dir = Path(os.environ.get("FAISS_STORE_DIR", str(_default_dir)))
_legacy_index = Path(os.environ.get("FAISS_INDEX_PATH", str(_default_dir / "jobs.index")))

_embeddings = FakeEmbeddings(size=_DIMENSION)
_store: FAISS | None = None

_SEED_DOCS = [
    Document(
        page_content=(
            "Acme Corp is hiring a Senior Backend Engineer. Stack: C#, ASP.NET Core, "
            "PostgreSQL, Docker. Remote-friendly. Focus on APIs and reliability."
        ),
        metadata={"company": "Acme Corp", "role": "Senior Backend Engineer", "source": "seed"},
    ),
    Document(
        page_content=(
            "BrightLabs seeks a Full-Stack Developer. React, TypeScript, Node, and "
            "Postgres. Build product dashboards and internal tools."
        ),
        metadata={"company": "BrightLabs", "role": "Full-Stack Developer", "source": "seed"},
    ),
    Document(
        page_content=(
            "NovaAI is looking for an ML Engineer for retrieval-augmented generation. "
            "Experience with FAISS, embeddings, and FastAPI preferred."
        ),
        metadata={"company": "NovaAI", "role": "ML Engineer", "source": "seed"},
    ),
    Document(
        page_content=(
            "Harbor Systems needs a Platform Engineer. Kubernetes, CI/CD, observability, "
            "and developer experience for a multi-service monorepo."
        ),
        metadata={"company": "Harbor Systems", "role": "Platform Engineer", "source": "seed"},
    ),
]


def _docs_path() -> Path:
    return _store_dir / "jobs.docs.json"


def _persist(store: FAISS) -> None:
    _store_dir.mkdir(parents=True, exist_ok=True)
    # LangChain save_local writes index.faiss + index.pkl; we use custom names via folder.
    store.save_local(str(_store_dir), index_name="jobs")
    docs = [
        {"page_content": d.page_content, "metadata": d.metadata}
        for d in store.docstore._dict.values()  # type: ignore[attr-defined]
    ]
    _docs_path().write_text(json.dumps(docs, indent=2), encoding="utf-8")


def _load_or_create_store() -> FAISS:
    faiss_file = _store_dir / "jobs.faiss"
    pkl_file = _store_dir / "jobs.pkl"
    if faiss_file.exists() and pkl_file.exists():
        return FAISS.load_local(
            str(_store_dir),
            _embeddings,
            index_name="jobs",
            allow_dangerous_deserialization=True,
        )

    # Migrate away from legacy raw IndexFlatL2 seed file if present alone.
    store = FAISS.from_documents(_SEED_DOCS, _embeddings)
    _persist(store)
    if _legacy_index.exists() and _legacy_index.resolve() != faiss_file.resolve():
        # Leave legacy file; new store is authoritative.
        pass
    return store


def _doc_count(store: FAISS | None) -> int:
    if store is None:
        return 0
    return len(store.index_to_docstore_id)


_store = _load_or_create_store()


@app.get("/health")
def health():
    return {
        "status": "ok",
        "service": "rag-api",
        "faiss_version": faiss.__version__,
        "index_vectors": _doc_count(_store),
        "dimensions": _DIMENSION,
        "langchain": {
            "vector_store_ready": _store is not None and _doc_count(_store) > 0,
            "langchain_core_version": metadata.version("langchain-core"),
            "langchain_community_version": metadata.version("langchain-community"),
        },
    }


@app.get("/health/faiss")
def faiss_probe():
    if _store is None or _doc_count(_store) == 0:
        raise HTTPException(status_code=503, detail="Vector store is empty")
    docs = _store.similarity_search("backend engineer postgresql", k=min(3, _doc_count(_store)))
    return {
        "dimensions": _DIMENSION,
        "sample_results": [
            {"page_content": d.page_content[:120], "metadata": d.metadata} for d in docs
        ],
    }


class IngestRequest(BaseModel):
    texts: Annotated[list[str], Field(min_length=1, max_length=100)]
    metadatas: list[dict[str, Any]] | None = None


class IngestResponse(BaseModel):
    ingested: int
    total_vectors: int


@app.post("/rag/ingest", response_model=IngestResponse)
def ingest(body: IngestRequest):
    """Add text chunks to the FAISS store (FakeEmbeddings — no API key required)."""
    global _store
    texts = [t.strip() for t in body.texts if t and t.strip()]
    if not texts:
        raise HTTPException(status_code=400, detail="No non-empty texts provided.")
    metadatas = body.metadatas or [{} for _ in texts]
    if len(metadatas) != len(texts):
        raise HTTPException(status_code=400, detail="metadatas length must match texts.")

    docs = [Document(page_content=t, metadata=m) for t, m in zip(texts, metadatas)]
    if _store is None or _doc_count(_store) == 0:
        _store = FAISS.from_documents(docs, _embeddings)
    else:
        _store.add_documents(docs)
    _persist(_store)
    return IngestResponse(ingested=len(docs), total_vectors=_doc_count(_store))


class QueryRequest(BaseModel):
    query: Annotated[str, Field(min_length=1, max_length=4000)]
    k: int = Field(default=5, ge=1, le=50)


@app.post("/rag/query")
def query(body: QueryRequest):
    """Nearest-neighbor text retrieval over ingested job chunks."""
    if _store is None or _doc_count(_store) == 0:
        raise HTTPException(status_code=503, detail="Vector store is empty")
    k = min(body.k, _doc_count(_store))
    docs = _store.similarity_search(body.query.strip(), k=k)
    return {
        "query": body.query.strip(),
        "k": k,
        "results": [
            {"page_content": d.page_content, "metadata": d.metadata} for d in docs
        ],
    }


class VectorSearchRequest(BaseModel):
    vector: Annotated[list[float], Field(min_length=_DIMENSION, max_length=_DIMENSION)]
    k: int = Field(default=5, ge=1, le=50)


@app.post("/rag/langchain/search-by-vector")
def langchain_search_by_vector(body: VectorSearchRequest):
    """Nearest neighbors using a raw embedding vector of length `dimensions`."""
    if _store is None or _doc_count(_store) == 0:
        raise HTTPException(status_code=503, detail="Vector store is empty")
    k = min(body.k, _doc_count(_store))
    docs = _store.similarity_search_by_vector(body.vector, k=k)
    return {
        "dimensions": _DIMENSION,
        "k": k,
        "results": [
            {"page_content": d.page_content, "metadata": d.metadata} for d in docs
        ],
    }
