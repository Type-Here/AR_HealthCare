from pathlib import Path

from fastapi import APIRouter
from fastapi.responses import FileResponse

router = APIRouter()

_STATIC = Path(__file__).parent.parent / "static"


@router.get("/admin", include_in_schema=False)
def admin_page():
    return FileResponse(_STATIC / "admin.html")


@router.get("/monitor", include_in_schema=False)
def monitor_page():
    return FileResponse(_STATIC / "monitor.html")


@router.get("/chat", include_in_schema=False)
def chat_page():
    return FileResponse(_STATIC / "chat.html")


@router.get("/logs", include_in_schema=False)
def logs_page():
    return FileResponse(_STATIC / "logs.html")
