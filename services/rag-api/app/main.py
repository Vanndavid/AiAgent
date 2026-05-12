"""
RAG API: vanilla retrieval first.

The active path uses NumPy + FAISS only: load vectors, run `index.search(query, k)`.
A LangChain equivalent is kept at the bottom of this file as comments so you can
uncomment it and the requirements lines when you want to compare frameworks.
"""

import os
from pathlib import Path
from typing import Annotated

import faiss
import numpy as np
from fastapi import FastAPI, HTTPException
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


_index = _load_or_create_index()


@app.get("/health")
def health():
    return {
        "status": "ok",
        "service": "rag-api",
        "faiss_version": faiss.__version__,
        "index_vectors": int(_index.ntotal),
        "retrieval": "vanilla_faiss",
    }


@app.get("/health/faiss")
def faiss_probe():
    probe = np.zeros((1, _DIMENSION), dtype=np.float32)
    distances, _labels = _index.search(probe, min(3, _index.ntotal))
    return {"dimensions": _DIMENSION, "sample_distances": distances[0].tolist()}


class VectorSearchRequest(BaseModel):
    vector: Annotated[list[float], Field(min_length=_DIMENSION, max_length=_DIMENSION)]
    k: int = Field(default=5, ge=1, le=50)


@app.post("/rag/search-by-vector")
def search_by_vector(body: VectorSearchRequest):
    """
    k-nearest neighbors in L2 space: one query row, FAISS returns distances + ids.
    No framework — same pattern most vector DBs wrap under the hood.
    """
    n = int(_index.ntotal)
    if n == 0:
        raise HTTPException(status_code=503, detail="Vector store is empty")
    k = min(body.k, n)
    q = np.asarray(body.vector, dtype=np.float32).reshape(1, _DIMENSION)
    distances, labels = _index.search(q, k)
    results = []
    for dist, idx in zip(distances[0].tolist(), labels[0].tolist()):
        if idx < 0:
            continue
        results.append(
            {
                "faiss_id": int(idx),
                "l2_distance": float(dist),
                "page_content": f"job-chunk-{idx}",
                "metadata": {"faiss_id": int(idx)},
            }
        )
    return {
        "dimensions": _DIMENSION,
        "k": k,
        "approach": "vanilla_faiss",
        "results": results,
    }


# =============================================================================
# LangChain alternative (commented out — for side-by-side learning later)
# Uncomment `langchain-*` lines in requirements.txt, then uncomment below.
# =============================================================================
# from importlib import metadata
#
# from langchain_community.embeddings import FakeEmbeddings
# from langchain_community.vectorstores import FAISS
#
#
# def _faiss_to_langchain(index: faiss.IndexFlatL2) -> FAISS | None:
#     """Wrap the on-disk FAISS index in a LangChain vector store (same vectors)."""
#     n = int(index.ntotal)
#     if n == 0:
#         return None
#     embedding = FakeEmbeddings(size=_DIMENSION)
#     text_embeddings: list[tuple[str, list[float]]] = []
#     metadatas: list[dict] = []
#     for i in range(n):
#         vec = index.reconstruct(i).astype(np.float64, copy=False)
#         text_embeddings.append((f"job-chunk-{i}", vec.tolist()))
#         metadatas.append({"faiss_id": i})
#     return FAISS.from_embeddings(
#         text_embeddings=text_embeddings,
#         embedding=embedding,
#         metadatas=metadatas,
#     )
#
#
# _langchain_store = _faiss_to_langchain(_index)
#
#
# @app.post("/rag/langchain/search-by-vector")
# def langchain_search_by_vector(body: VectorSearchRequest):
#     """Nearest neighbors using the LangChain FAISS wrapper."""
#     if _langchain_store is None:
#         raise HTTPException(status_code=503, detail="Vector store is empty")
#     k = min(body.k, int(_index.ntotal))
#     q = np.asarray(body.vector, dtype=np.float32)
#     docs = _langchain_store.similarity_search_by_vector(q, k=k)
#     return {
#         "dimensions": _DIMENSION,
#         "k": k,
#         "approach": "langchain_faiss",
#         "results": [
#             {"page_content": d.page_content, "metadata": d.metadata} for d in docs
#         ],
#     }
#
# # In health(), you could merge:
# # "langchain": {
# #     "vector_store_ready": _langchain_store is not None,
# #     "langchain_core_version": metadata.version("langchain-core"),
# #     "langchain_community_version": metadata.version("langchain-community"),
# # },
