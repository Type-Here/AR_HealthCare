#!/bin/bash

# Activate conda environment
source ~/miniforge3/bin/activate
conda activate iv

# Start Ollama in background
echo "Avvio Ollama (qwen2.5:3b)..."
ollama run qwen2.5:3b &
OLLAMA_PID=$!

# Trap Ctrl+C to also kill Ollama
cleanup() {
    echo ""
    echo "Arresto Ollama..."
    kill $OLLAMA_PID 2>/dev/null
    exit 0
}
trap cleanup SIGINT SIGTERM

# Wait for Ollama to be ready
sleep 3

# Start uvicorn in foreground
echo "Avvio server FastAPI..."
uvicorn server:app --host 0.0.0.0 --port 8000

# If uvicorn exits normally, kill Ollama too
kill $OLLAMA_PID 2>/dev/null
