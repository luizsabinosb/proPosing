"""
Script de teste rápido da API
Testa os endpoints básicos sem precisar de imagem real
"""
try:
    import requests
except ImportError:
    print("❌ Erro: biblioteca 'requests' não instalada")
    print("   Execute: pip install requests")
    import sys
    sys.exit(1)

import requests
import base64
import cv2
import numpy as np
import sys
from pathlib import Path

BASE_URL = "http://localhost:8000"

def create_test_image() -> str:
    """Cria uma imagem de teste (frame preto)"""
    # Cria uma imagem simples de 640x480
    img = np.zeros((480, 640, 3), dtype=np.uint8)
    img.fill(50)  # Cinza escuro
    
    # Codifica como JPEG
    success, encoded_image = cv2.imencode('.jpg', img)
    if not success:
        raise ValueError("Erro ao criar imagem de teste")
    
    # Converte para Base64
    image_base64 = base64.b64encode(encoded_image.tobytes()).decode('utf-8')
    return image_base64

def test_health():
    """Testa endpoint de health check"""
    print("🔍 Testando health check...")
    try:
        response = requests.get(f"{BASE_URL}/health", timeout=5)
        if response.status_code == 200:
            print("✅ Health check OK:", response.json())
            return True
        else:
            print(f"❌ Health check falhou: {response.status_code}")
            return False
    except requests.exceptions.ConnectionError:
        print("❌ Erro: Não foi possível conectar ao servidor")
        print("   Certifique-se de que o backend está rodando:")
        print("   cd backend && uvicorn app.main:app --reload")
        return False
    except Exception as e:
        print(f"❌ Erro: {e}")
        return False

def test_root():
    """Testa endpoint raiz"""
    print("\n🔍 Testando endpoint raiz...")
    try:
        response = requests.get(f"{BASE_URL}/", timeout=5)
        if response.status_code == 200:
            print("✅ Endpoint raiz OK:", response.json())
            return True
        else:
            print(f"❌ Endpoint raiz falhou: {response.status_code}")
            return False
    except Exception as e:
        print(f"❌ Erro: {e}")
        return False

def test_select_pose():
    """Testa seleção de pose"""
    print("\n🔍 Testando seleção de pose...")
    try:
        response = requests.post(
            f"{BASE_URL}/api/v1/pose/select",
            json={"pose_mode": "enquadramento"},
            timeout=5
        )
        if response.status_code == 200:
            data = response.json()
            print("✅ Seleção de pose OK:")
            print(f"   Pose: {data['pose_name']}")
            return True
        else:
            print(f"❌ Seleção de pose falhou: {response.status_code}")
            print(f"   Resposta: {response.text}")
            return False
    except Exception as e:
        print(f"❌ Erro: {e}")
        return False

def test_evaluate_pose():
    """Testa avaliação de pose"""
    print("\n🔍 Testando avaliação de pose...")
    try:
        # Cria imagem de teste
        image_base64 = create_test_image()
        
        # Envia requisição
        response = requests.post(
            f"{BASE_URL}/api/v1/pose/evaluate",
            json={
                "image": image_base64,
                "pose_mode": "enquadramento",
                "camera_width": 640
            },
            timeout=10
        )
        
        if response.status_code == 200:
            data = response.json()
            print("✅ Avaliação de pose OK:")
            print(f"   Status: {data['status']}")
            print(f"   Tempo: {data['processing_time_ms']}ms")
            if data.get('pose_quality'):
                print(f"   Feedback: {data['pose_quality'][:100]}...")
            print(f"   Landmarks: {len(data.get('landmarks', []))}")
            return True
        else:
            print(f"❌ Avaliação de pose falhou: {response.status_code}")
            print(f"   Resposta: {response.text[:200]}")
            return False
    except Exception as e:
        print(f"❌ Erro: {e}")
        import traceback
        traceback.print_exc()
        return False

def main():
    """Executa todos os testes"""
    print("=" * 60)
    print("🧪 Testando ProPosing API")
    print("=" * 60)
    
    # Verifica se servidor está rodando
    if not test_health():
        print("\n❌ Servidor não está rodando!")
        print("\n📝 Para iniciar o servidor:")
        print("   cd backend")
        print("   pip3 install -r requirements.txt")
        print("   python3 -m uvicorn app.main:app --host 0.0.0.0 --port 8000")
        sys.exit(1)
    
    # Testa endpoints
    results = []
    results.append(test_root())
    results.append(test_select_pose())
    results.append(test_evaluate_pose())
    
    # Resumo
    print("\n" + "=" * 60)
    print("📊 Resumo dos Testes")
    print("=" * 60)
    passed = sum(results)
    total = len(results)
    print(f"✅ Passou: {passed}/{total}")
    
    if passed == total:
        print("\n🎉 Todos os testes passaram!")
        return 0
    else:
        print(f"\n⚠️ {total - passed} teste(s) falharam")
        return 1

if __name__ == "__main__":
    sys.exit(main())

