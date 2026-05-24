import json
import os
from fastapi import APIRouter, HTTPException

router = APIRouter()

_DB_PATH = os.path.join(os.path.dirname(__file__), "..", "data", "patients.json")
_patients: dict = {}


def _load():
    global _patients
    try:
        with open(_DB_PATH, "r", encoding="utf-8") as f:
            _patients = json.load(f)
    except FileNotFoundError:
        _patients = {}


_load()


@router.get("/patient/{patient_id}")
def get_patient(patient_id: str):
    if not _patients:
        _load()
    record = _patients.get(patient_id)
    if record is None:
        raise HTTPException(status_code=404, detail=f"Patient '{patient_id}' not found")
    return record
