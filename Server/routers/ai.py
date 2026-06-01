import asyncio
import threading
from queue import Queue as ThreadQueue
from typing import Optional

from fastapi import APIRouter
from pydantic import BaseModel

import llm_client

router = APIRouter()


class SuggestRequest(BaseModel):
    patient: dict
    specialty: str
    mode: str = "physician"
    context: Optional[str] = ""


class SuggestResponse(BaseModel):
    suggestion: str


@router.post("/suggest", response_model=SuggestResponse)
async def suggest(req: SuggestRequest):
    from routers.stream import push_ai_event, push_log_event

    summary = f"{req.patient.get('display_name', '?')} | {req.specialty} | {req.mode}"
    await push_ai_event("prompt", summary)
    await push_log_event("AI", f"Richiesta: {summary}")

    q: ThreadQueue = ThreadQueue()

    def _stream_worker():
        try:
            for token in llm_client.get_suggestion_stream(
                req.patient, req.specialty, req.mode, req.context or ""
            ):
                q.put(token)
        except Exception as e:
            q.put(RuntimeError(str(e)))
        finally:
            q.put(None)

    threading.Thread(target=_stream_worker, daemon=True).start()

    full = []
    loop = asyncio.get_event_loop()
    while True:
        token = await loop.run_in_executor(None, q.get)
        if token is None:
            break
        if isinstance(token, RuntimeError):
            msg = str(token)
            await push_ai_event("error", msg)
            await push_log_event("ERROR", f"Ollama: {msg}")
            return SuggestResponse(suggestion=f"Errore: {msg}")
        full.append(token)
        await push_ai_event("token", token)

    result = "".join(full)
    await push_ai_event("done", result)
    await push_log_event("AI", f"Risposta pronta ({len(result)} caratteri)")
    return SuggestResponse(suggestion=result)
