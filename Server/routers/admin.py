from pathlib import Path

from fastapi import APIRouter
from fastapi.responses import HTMLResponse, Response

router = APIRouter()

_STATIC = Path(__file__).parent.parent / "static"


@router.get("/admin", response_class=HTMLResponse, include_in_schema=False)
def admin_page():
    return (_STATIC / "admin.html").read_text(encoding="utf-8")


@router.get("/admin.css", include_in_schema=False)
def admin_css():
    return Response(
        content=(_STATIC / "admin.css").read_text(encoding="utf-8"),
        media_type="text/css",
    )


@router.get("/admin.js", include_in_schema=False)
def admin_js():
    return Response(
        content=(_STATIC / "admin.js").read_text(encoding="utf-8"),
        media_type="application/javascript",
    )
