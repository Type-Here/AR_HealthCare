"""Ollama HTTP client for LLM suggestions."""

import json

import requests
import config


def build_prompt(patient: dict, specialty: str, mode: str, context: str) -> str:
    name      = patient.get("display_name", "Unknown")
    age       = patient.get("age", "?")
    diagnosis = patient.get("diagnosis", "N/A")
    procedure = patient.get("planned_procedure", "N/A")
    treatment = patient.get("current_treatment", "N/A")
    notes     = patient.get("notes", "")

    patient_summary = (
        f"Patient: {name}, {age} years old.\n"
        f"Diagnosis: {diagnosis}.\n"
        f"Planned procedure: {procedure}.\n"
        f"Current treatment: {treatment}.\n"
        f"Notes: {notes}"
    )

    no_markdown = "Do not use markdown, headers, or any special formatting. Respond in plain text only."

    if mode == "student":
        system = (
            "You are a medical education assistant helping a medical student learn during a clinical visit. "
            f"The student is observing a {specialty} case. "
            "Provide clear, educational explanations. Include relevant anatomy, pathophysiology, and clinical reasoning. "
            f"Keep your response concise (under 120 words). {no_markdown}"
        )
    else:
        system = (
            "You are a clinical decision support assistant for an attending physician. "
            f"Specialty: {specialty}. "
            "Be concise and clinically precise. Focus on differential diagnosis considerations, "
            f"red flags, and management reminders. Under 120 words. {no_markdown}"
        )

    user = f"{patient_summary}\n\n{context if context else 'Provide relevant clinical guidance for this visit.'}"

    return system, user


def get_suggestion(patient: dict, specialty: str, mode: str, context: str) -> str:
    system_prompt, user_prompt = build_prompt(patient, specialty, mode, context)

    payload = {
        "model": config.OLLAMA_MODEL,
        "prompt": f"[SYSTEM]\n{system_prompt}\n\n[USER]\n{user_prompt}",
        "stream": False,
        "options": {
            "temperature": 0.4,
            "num_predict": 256,
        },
    }

    try:
        response = requests.post(
            f"{config.OLLAMA_BASE_URL}/api/generate",
            json=payload,
            timeout=60,
        )
        response.raise_for_status()
        return response.json().get("response", "").strip()
    except requests.exceptions.ConnectionError:
        return "Error: Ollama is not running. Start it with: ollama serve"
    except Exception as e:
        return f"Error: {e}"


def get_suggestion_stream(patient: dict, specialty: str, mode: str, context: str):
    """Yields response tokens one by one from Ollama streaming API."""
    system_prompt, user_prompt = build_prompt(patient, specialty, mode, context)

    payload = {
        "model": config.OLLAMA_MODEL,
        "prompt": f"[SYSTEM]\n{system_prompt}\n\n[USER]\n{user_prompt}",
        "stream": True,
        "options": {
            "temperature": 0.4,
            "num_predict": 256,
        },
    }

    try:
        with requests.post(
            f"{config.OLLAMA_BASE_URL}/api/generate",
            json=payload,
            stream=True,
            timeout=60,
        ) as r:
            r.raise_for_status()
            for line in r.iter_lines():
                if line:
                    chunk = json.loads(line)
                    if chunk.get("response"):
                        yield chunk["response"]
                    if chunk.get("done"):
                        break
    except requests.exceptions.ConnectionError:
        yield "Error: Ollama is not running. Start it with: ollama serve"
    except Exception as e:
        yield f"Error: {e}"
