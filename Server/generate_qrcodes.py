"""
generate_qrcodes.py — genera un PNG per ogni paziente in data/patients.json
Eseguire dalla cartella Server/:
    python generate_qrcodes.py
I file vengono salvati in Server/qrcodes/<patient_id>.png, pronti per la stampa.
"""

import json
import os
from pathlib import Path

try:
    import qrcode
except ImportError:
    raise SystemExit(
        "Libreria qrcode non trovata.\n"
        "Installa con:  pip install qrcode[pil]"
    )

# Percorsi (relativi alla cartella Server/)
PATIENTS_FILE = Path(__file__).parent / "data" / "patients.json"
OUTPUT_DIR = Path(__file__).parent / "qrcodes"

OUTPUT_DIR.mkdir(exist_ok=True)

with open(PATIENTS_FILE, encoding="utf-8") as f:
    patients = json.load(f)

for patient_id, record in patients.items():
    qr = qrcode.QRCode(
        version=1,
        error_correction=qrcode.constants.ERROR_CORRECT_M,
        box_size=10,
        border=4,
    )
    qr.add_data(patient_id)
    qr.make(fit=True)

    img = qr.make_image(fill_color="black", back_color="white")
    out_path = OUTPUT_DIR / f"{patient_id}.png"
    img.save(out_path)
    print(f"  ✓ {out_path}  ({record.get('display_name', patient_id)})")

print(f"\nGenerati {len(patients)} QR code in '{OUTPUT_DIR}'")
