HOST = "0.0.0.0"
PORT = 8000

# Ollama settings — change model to qwen2.5:7b if the laptop has a GPU
OLLAMA_BASE_URL = "http://localhost:11434"
OLLAMA_MODEL = "qwen2.5:3b"

# Face recognition
FACE_DB_PATH = "face_db"
FACE_RECOGNITION_THRESHOLD = 0.4   # confidence below this = unknown patient
FACE_MODEL_NAME = "ArcFace"         # DeepFace backend: ArcFace is most accurate
FACE_DETECTOR = "retinaface"        # retinaface is more robust than opencv for real-world use

# OBS Virtual Camera name as seen by ffmpeg avfoundation.
# Run: ffmpeg -f avfoundation -list_devices true -i "" 2>&1 | grep -i obs
# to find the exact name on your machine.
OBS_CAMERA_NAME = "OBS Virtual Camera"
