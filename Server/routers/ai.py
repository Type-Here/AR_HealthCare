from fastapi import APIRouter
from pydantic import BaseModel
from typing import Optional
import llm_client

router = APIRouter()


class SuggestRequest(BaseModel):
    patient: dict
    specialty: str
    mode: str = "physician"   # "student" or "physician"
    context: Optional[str] = ""


class SuggestResponse(BaseModel):
    suggestion: str


@router.post("/suggest", response_model=SuggestResponse)
def suggest(req: SuggestRequest):
    text = llm_client.get_suggestion(req.patient, req.specialty, req.mode, req.context or "")
    return SuggestResponse(suggestion=text)
