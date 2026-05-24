from fastapi import APIRouter, HTTPException
from pydantic import BaseModel
import face_engine

router = APIRouter()


class FaceRequest(BaseModel):
    image_b64: str


class FaceResponse(BaseModel):
    patient_id: str | None
    confidence: float


@router.post("/recognize-face", response_model=FaceResponse)
def recognize_face(req: FaceRequest):
    if not req.image_b64:
        raise HTTPException(status_code=400, detail="image_b64 is required")
    try:
        patient_id, confidence = face_engine.recognize_from_b64(req.image_b64)
        return FaceResponse(patient_id=patient_id, confidence=round(confidence, 3))
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Recognition error: {e}")
