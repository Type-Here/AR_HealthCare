import json
import os
from fastapi import APIRouter, HTTPException

router = APIRouter()

_DB_PATH = os.path.join(os.path.dirname(__file__), "..", "data", "checklists.json")
_checklists: dict = {}


def _load():
    global _checklists
    try:
        with open(_DB_PATH, "r", encoding="utf-8") as f:
            _checklists = json.load(f)
    except FileNotFoundError:
        _checklists = {}


_load()


@router.get("/checklist/{specialty}")
def get_checklist(specialty: str):
    if not _checklists:
        _load()
    items = _checklists.get(specialty)
    if items is None:
        # Return empty checklist rather than 404 so the client doesn't error
        return {"specialty": specialty, "items": []}
    return {"specialty": specialty, "items": items}
