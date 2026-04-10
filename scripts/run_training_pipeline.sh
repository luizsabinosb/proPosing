#!/bin/bash
# run_training_pipeline.sh
# Roda o pipeline completo de treinamento de uma vez só.
# Execute da raiz do projeto:  bash scripts/run_training_pipeline.sh

set -e
cd "$(dirname "$0")/.."

echo ""
echo "============================================================"
echo "  ProPosing — Pipeline de Treinamento"
echo "============================================================"

echo ""
echo "Passo 1/3 — Processar imagens de referência (ml/pose_info)..."
python3 treinamento/process_pose_info.py

echo ""
echo "Passo 2/3 — Consolidar todas as fontes de dados..."
python3 treinamento/consolidate_training_data.py

echo ""
echo "Passo 3/3 — Treinar modelos ML..."
python3 -c "
import sys
# Auto-escolhe 'Ambos' (opção 3) sem interação manual
import unittest.mock as mock
with mock.patch('builtins.input', return_value='3'):
    exec(open('treinamento/train_model.py').read())
"

echo ""
echo "============================================================"
echo "  Treinamento concluído!"
echo "  Modelos salvos em: ml/models/"
echo "  Reinicie o backend para carregar os novos modelos."
echo "============================================================"
