"""Streaming endpoints: OBS MJPEG video, AI SSE, server log SSE."""

import asyncio
import base64
import json
from datetime import datetime

from fastapi import APIRouter
from fastapi.responses import StreamingResponse

import config

router = APIRouter()

# ── AI SSE ─────────────────────────────────────────────────────────────────────
_ai_subscribers: list[asyncio.Queue] = []


async def push_ai_event(event_type: str, text: str) -> None:
    for q in _ai_subscribers:
        await q.put({"type": event_type, "text": text})


async def _ai_sse_gen():
    q: asyncio.Queue = asyncio.Queue()
    _ai_subscribers.append(q)
    try:
        yield "retry: 3000\n\n"
        while True:
            try:
                event = await asyncio.wait_for(q.get(), timeout=20.0)
                yield f"data: {json.dumps(event)}\n\n"
            except asyncio.TimeoutError:
                yield ": keepalive\n\n"
    finally:
        _ai_subscribers.remove(q)


@router.get("/stream/ai", include_in_schema=False)
async def ai_stream():
    return StreamingResponse(
        _ai_sse_gen(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


# ── Log SSE ────────────────────────────────────────────────────────────────────
_log_subscribers: list[asyncio.Queue] = []
_log_history: list[dict] = []
_MAX_LOG_HISTORY = 300


async def push_log_event(level: str, message: str) -> None:
    entry = {
        "level": level,
        "message": message,
        "ts": datetime.now().strftime("%H:%M:%S"),
    }
    _log_history.append(entry)
    if len(_log_history) > _MAX_LOG_HISTORY:
        _log_history.pop(0)
    for q in _log_subscribers:
        await q.put(entry)


async def _log_sse_gen():
    q: asyncio.Queue = asyncio.Queue()
    _log_subscribers.append(q)
    try:
        for entry in _log_history:
            yield f"data: {json.dumps(entry)}\n\n"
        yield "retry: 3000\n\n"
        while True:
            try:
                event = await asyncio.wait_for(q.get(), timeout=20.0)
                yield f"data: {json.dumps(event)}\n\n"
            except asyncio.TimeoutError:
                yield ": keepalive\n\n"
    finally:
        _log_subscribers.remove(q)


@router.get("/stream/logs", include_in_schema=False)
async def logs_stream():
    return StreamingResponse(
        _log_sse_gen(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


# ── Video MJPEG — OBS Virtual Camera via ffmpeg ────────────────────────────────
async def _obs_mjpeg_gen():
    cmd = [
        "ffmpeg",
        "-f", "avfoundation",
        "-framerate", "10",
        "-i", config.OBS_CAMERA_NAME,
        "-vf", "scale=960:540",
        "-f", "image2pipe",
        "-vcodec", "mjpeg",
        "-q:v", "5",
        "pipe:1",
    ]
    proc = await asyncio.create_subprocess_exec(
        *cmd,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.DEVNULL,
    )
    buf = b""
    try:
        while True:
            chunk = await proc.stdout.read(65536)
            if not chunk:
                break
            buf += chunk
            while True:
                s = buf.find(b"\xff\xd8")
                if s == -1:
                    buf = b""
                    break
                e = buf.find(b"\xff\xd9", s + 2)
                if e == -1:
                    buf = buf[s:]
                    break
                frame = buf[s : e + 2]
                buf = buf[e + 2 :]
                yield (
                    b"--frame\r\n"
                    b"Content-Type: image/jpeg\r\n\r\n"
                    + frame
                    + b"\r\n"
                )
    finally:
        try:
            proc.kill()
        except Exception:
            pass


@router.get("/stream/video", include_in_schema=False)
async def video_stream():
    return StreamingResponse(
        _obs_mjpeg_gen(),
        media_type="multipart/x-mixed-replace; boundary=frame",
    )
