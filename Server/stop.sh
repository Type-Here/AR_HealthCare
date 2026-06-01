#!/bin/bash

echo "Arresto uvicorn..."
pkill -f "uvicorn server:app" 2>/dev/null && echo "uvicorn fermato." || echo "uvicorn non era in esecuzione."

echo "Arresto Ollama..."
pkill -f "ollama run qwen2.5:3b" 2>/dev/null
pkill -f "ollama" 2>/dev/null && echo "Ollama fermato." || echo "Ollama non era in esecuzione."

echo "Deactivate conda..."
source ~/miniforge3/bin/activate
conda deactivate

echo "Fatto."
