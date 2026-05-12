import os
from importlib import metadata
from pathlib import Path
from typing import Annotated

import faiss
import numpy as np
from fastapi import FastAPI, HTTPException
from langchain_community.embeddings import FakeEmbeddings
from langchain_community.vectorstores import FAISS
from pydantic import BaseModel, Field

app = FastAPI(title="Job Assistant RAG", version="0.1.0")

_DIMENSION = 64
_default_index_path = Path(__file__).resolve().parent.parent / "var" / "faiss" / "jobs.index"
_index_path = Path(os.environ.get("FAISS_INDEX_PATH", str(_default_index_path)))


def _load_or_create_index() -> faiss.IndexFlatL2:
    _index_path.parent.mkdir(parents=True, exist_ok=True)
    index = faiss.IndexFlatL2(_DIMENSION)
    if _index_path.exists():
        idx = faiss.read_index(str(_index_path))
        return idx
    rng = np.random.default_rng(42)
    seed = rng.standard_normal((8, _DIMENSION)).astype(np.float32)
    index.add(seed)
    faiss.write_index(index, str(_index_path))
    return index


def _faiss_to_langchain(index: faiss.IndexFlatL2) -> FAISS | None:
    """Wrap the on-disk FAISS index in a LangChain vector store (same vectors)."""
    n = int(index.ntotal)
    if n == 0:
        return None
    embedding = FakeEmbeddings(size=_DIMENSION)
    text_embeddings: list[tuple[str, list[float]]] = []
    metadatas: list[dict] = []
    for i in range(n):
        vec = index.reconstruct(i).astype(np.float64, copy=False)
        text_embeddings.append((f"job-chunk-{i}", vec.tolist()))
        metadatas.append({"faiss_id": i})
    return FAISS.from_embeddings(
        text_embeddings=text_embeddings,
        embedding=embedding,
        metadatas=metadatas,
    )


_index = _load_or_create_index()
_langchain_store = _faiss_to_langchain(_index)


@app.get("/health")
def health():
    return {
        "status": "ok",
        "service": "rag-api",
        "faiss_version": faiss.__version__,
        "index_vectors": int(_index.ntotal),
        "langchain": {
            "vector_store_ready": _langchain_store is not None,
            "langchain_core_version": metadata.version("langchain-core"),
            "langchain_community_version": metadata.version("langchain-community"),
        },
    }


@app.get("/health/faiss")
def faiss_probe():
    probe = np.zeros((1, _DIMENSION), dtype=np.float32)
    distances, _labels = _index.search(probe, min(3, _index.ntotal))
    return {"dimensions": _DIMENSION, "sample_distances": distances[0].tolist()}


class VectorSearchRequest(BaseModel):
    vector: Annotated[list[float], Field(min_length=_DIMENSION, max_length=_DIMENSION)]
    k: int = Field(default=5, ge=1, le=50)


@app.post("/rag/langchain/search-by-vector")
def langchain_search_by_vector(body: VectorSearchRequest):
    """Nearest neighbors using the LangChain FAISS wrapper (query must be length `dimensions`)."""
    if _langchain_store is None:
        raise HTTPException(status_code=503, detail="Vector store is empty")
    k = min(body.k, int(_index.ntotal))
    q = np.asarray(body.vector, dtype=np.float32)
    docs = _langchain_store.similarity_search_by_vector(q, k=k)
    return {
        "dimensions": _DIMENSION,
        "k": k,
        "results": [
            {"page_content": d.page_content, "metadata": d.metadata} for d in docs
        ],
    }
