import os
from pathlib import Path

import faiss
import numpy as np
from fastapi import FastAPI

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
    }


@app.get("/health/faiss")
def faiss_probe():
    probe = np.zeros((1, _DIMENSION), dtype=np.float32)
    distances, _labels = _index.search(probe, min(3, _index.ntotal))
    return {"dimensions": _DIMENSION, "sample_distances": distances[0].tolist()}
