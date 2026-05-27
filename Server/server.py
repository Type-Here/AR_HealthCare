"""
AR HealthCare Companion Server
Run: uvicorn server:app --host 0.0.0.0 --port 8000
Requires: pip install -r requirements.txt
Requires: ollama pull qwen2.5:1.5b  (or edit config.py for a different model)
"""

from fastapi import FastAPI
from routers import face, patients, ai, checklist, admin

app = FastAPI(
    title="AR HealthCare Companion",
    description="Companion server for AR HealthCare (Magic Leap 2). Provides face recognition, patient records, LLM suggestions, and visit checklists.",
    version="1.0.0",
)

app.include_router(face.router)
app.include_router(patients.router)
app.include_router(ai.router)
app.include_router(checklist.router)
app.include_router(admin.router)


@app.get("/health")
def health():
    return {"status": "ok"}
