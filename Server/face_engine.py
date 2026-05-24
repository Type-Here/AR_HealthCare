"""DeepFace wrapper for patient face recognition against face_db/."""

import base64
import os
import tempfile
from typing import Optional, Tuple

from PIL import Image
import io

import config

# DeepFace is a heavy import — lazy-load to keep startup fast
_deepface = None

def _get_deepface():
    global _deepface
    if _deepface is None:
        from deepface import DeepFace
        _deepface = DeepFace
    return _deepface


def recognize_from_b64(image_b64: str) -> Tuple[Optional[str], float]:
    """
    Decode a base64 JPEG, run DeepFace.find() against face_db/, return (patient_id, confidence).
    Returns (None, 0.0) if no match above threshold.
    """
    DeepFace = _get_deepface()

    # Decode image to a temp file (DeepFace.find needs a file path or array)
    try:
        img_bytes = base64.b64decode(image_b64)
        img = Image.open(io.BytesIO(img_bytes)).convert("RGB")
    except Exception as e:
        raise ValueError(f"Failed to decode image: {e}")

    with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as tmp:
        tmp_path = tmp.name
        img.save(tmp_path, "JPEG")

    try:
        results = DeepFace.find(
            img_path=tmp_path,
            db_path=config.FACE_DB_PATH,
            model_name=config.FACE_MODEL_NAME,
            detector_backend=config.FACE_DETECTOR,
            enforce_detection=False,
            silent=True,
        )
    except Exception as e:
        return None, 0.0
    finally:
        os.unlink(tmp_path)

    # DeepFace.find returns a list of DataFrames (one per face detected in the query image)
    if not results or len(results) == 0:
        return None, 0.0

    df = results[0]
    if df.empty:
        return None, 0.0

    # Sort by distance ascending (lower = more similar)
    distance_col = [c for c in df.columns if "distance" in c.lower()]
    if distance_col:
        df = df.sort_values(distance_col[0])

    best_match_path: str = df.iloc[0]["identity"]
    best_distance = float(df.iloc[0][distance_col[0]]) if distance_col else 1.0

    # Convert distance to confidence (0-1, higher = more confident)
    # ArcFace cosine distance: 0 = identical, 1 = completely different
    confidence = max(0.0, 1.0 - best_distance)

    if confidence < config.FACE_RECOGNITION_THRESHOLD:
        return None, confidence

    # Extract patient_id from path: face_db/<patient_id>/<image.jpg>
    parts = best_match_path.replace("\\", "/").split("/")
    patient_id = parts[-2] if len(parts) >= 2 else None

    return patient_id, confidence
