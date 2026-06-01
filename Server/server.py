"""
AR HealthCare Companion Server
Run: uvicorn server:app --host 0.0.0.0 --port 8000
Requires: pip install -r requirements.txt
Requires: ollama pull qwen2.5:3b  (or edit config.py for a different model)
"""

import asyncio
from pathlib import Path

from fastapi import FastAPI, Request
from fastapi.staticfiles import StaticFiles

from routers import face, patients, ai, checklist, admin, stream

app = FastAPI(
    title="AR HealthCare Companion",
    description="Companion server for AR HealthCare (Magic Leap 2). Provides face recognition, patient records, LLM suggestions, and visit checklists.",
    version="1.0.0",
)

_STATIC = Path(__file__).parent / "static"
app.mount("/static", StaticFiles(directory=str(_STATIC)), name="static")

app.include_router(face.router)
app.include_router(patients.router)
app.include_router(ai.router)
app.include_router(checklist.router)
app.include_router(stream.router)
app.include_router(admin.router)

# Paths excluded from request logging (too noisy)
_LOG_SKIP = {"/health", "/stream/video", "/stream/ai", "/stream/logs"}


@app.middleware("http")
async def log_requests(request: Request, call_next):
    response = await call_next(request)
    path = request.url.path
    if not path.startswith("/static") and path not in _LOG_SKIP:
        from routers.stream import push_log_event
        level = "ERROR" if response.status_code >= 500 else ("WARN" if response.status_code >= 400 else "INFO")
        asyncio.create_task(
            push_log_event(level, f"{request.method} {path} {response.status_code}")
        )
    return response


@app.get("/health")
def health():
    return {"status": "ok"}
