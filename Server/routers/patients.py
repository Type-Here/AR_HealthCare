import json
import os
import shutil
from typing import List, Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field, field_validator

router = APIRouter()

_DB_PATH    = os.path.join(os.path.dirname(__file__), "..", "data", "patients.json")
_FACE_DB    = os.path.join(os.path.dirname(__file__), "..", "face_db")
_patients: dict = {}

_VALID_SPECIALTIES = {"orthopedics", "neurology", "cardiology", "general"}


def _load():
    global _patients
    try:
        with open(_DB_PATH, "r", encoding="utf-8") as f:
            _patients = json.load(f)
    except FileNotFoundError:
        _patients = {}


def _save():
    with open(_DB_PATH, "w", encoding="utf-8") as f:
        json.dump(_patients, f, indent=2, ensure_ascii=False)


_load()


# ── Shared base model ────────────────────────────────────────────────────────

class _PatientFields(BaseModel):
    display_name: str
    age: int = Field(ge=0, le=150)
    diagnosis: str
    specialty: str
    planned_procedure: str
    procedure_date: str
    current_treatment: str
    history: Optional[List[str]] = []
    notes: Optional[str] = ""

    @field_validator("specialty")
    @classmethod
    def specialty_valid(cls, v: str) -> str:
        if v not in _VALID_SPECIALTIES:
            raise ValueError(
                f"specialty deve essere uno di: {', '.join(sorted(_VALID_SPECIALTIES))}"
            )
        return v

    @field_validator("display_name", "diagnosis", "planned_procedure",
                     "procedure_date", "current_treatment")
    @classmethod
    def not_empty(cls, v: str) -> str:
        if not v.strip():
            raise ValueError("il campo non può essere vuoto")
        return v.strip()


class CreatePatientRequest(_PatientFields):
    id: str

    @field_validator("id")
    @classmethod
    def id_valid(cls, v: str) -> str:
        v = v.strip().lower()
        if not v:
            raise ValueError("id non può essere vuoto")
        if " " in v:
            raise ValueError("id non può contenere spazi — usa underscore (es. mario_rossi)")
        return v


class UpdatePatientRequest(_PatientFields):
    pass


# ── GET single patient ───────────────────────────────────────────────────────

@router.get("/patient/{patient_id}")
def get_patient(patient_id: str):
    if not _patients:
        _load()
    record = _patients.get(patient_id)
    if record is None:
        raise HTTPException(status_code=404, detail=f"Paziente '{patient_id}' non trovato")
    return record


# ── GET all patients ─────────────────────────────────────────────────────────

@router.get("/patients")
def get_all_patients():
    if not _patients:
        _load()
    return {"patients": _patients}


# ── POST create patient ──────────────────────────────────────────────────────

@router.post("/patient", status_code=201)
def create_patient(req: CreatePatientRequest):
    if not _patients:
        _load()
    if req.id in _patients:
        raise HTTPException(status_code=409, detail=f"Paziente '{req.id}' esiste già")

    record = req.model_dump()
    _patients[req.id] = record
    _save()

    # Crea cartella face_db per il nuovo paziente (se non esiste già)
    os.makedirs(os.path.join(_FACE_DB, req.id), exist_ok=True)

    return record


# ── PUT update patient ───────────────────────────────────────────────────────

@router.put("/patient/{patient_id}")
def update_patient(patient_id: str, req: UpdatePatientRequest):
    if not _patients:
        _load()
    if patient_id not in _patients:
        raise HTTPException(status_code=404, detail=f"Paziente '{patient_id}' non trovato")

    record = req.model_dump()
    record["id"] = patient_id
    _patients[patient_id] = record
    _save()
    return record


# ── DELETE patient ───────────────────────────────────────────────────────────

@router.delete("/patient/{patient_id}", status_code=200)
def delete_patient(patient_id: str):
    if not _patients:
        _load()
    if patient_id not in _patients:
        raise HTTPException(status_code=404, detail=f"Paziente '{patient_id}' non trovato")

    del _patients[patient_id]
    _save()

    # Rimuovi cartella face_db del paziente (foto incluse)
    face_dir = os.path.join(_FACE_DB, patient_id)
    if os.path.isdir(face_dir):
        shutil.rmtree(face_dir)

    return {"deleted": patient_id}
