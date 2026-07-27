# Loitering_Detection


※geminiで生成

Webカメラの映像からAI（YOLO）を用いて人物の「居座り（滞在時間）」を検知し、Unity側のクライアントにリアルタイムで共有・可視化するためのシステムです。

## 🛠 システム構成
- **Python (Backend / AI)**: FlaskによるWeb APIサーバー + YOLO（物体検出・トラッキング）
- **Unity (Client / UI)**: Webカメラ映像の受信、判定エリアのUI設定（ドラッグ操作）、ステータス受信

---

## 🚀 セットアップ手順

### 1. Python側の準備（バックエンド）

Pythonフォルダへ移動し、仮想環境を作成・有効化して必要なライブラリをインストールします。

```bash
# リポジトリのPythonディレクトリへ移動
cd Python

# 仮想環境の作成
python -m venv .venv

# 仮想環境の有効化
# Mac / Linux の場合:
source .venv/bin/activate
# Windows (コマンドプロンプト) の場合:
# .venv\Scripts\activate

# 必要なライブラリの一括インストール
pip install -r requirements.txt
```

#### Pythonサーバーの起動方法

```bash
python main.py
```

### 2. Python側の準備（バックエンド）
1. Unity Hubからプロジェクトを開く
   - 推奨バージョン: Unity 6 (6000.3.10f1) 以降
   - Unity Hubの「プロジェクト」タブから「追加」を選択し、本リポジトリ内の `Unity` フォルダ（またはプロジェクトフォルダ）を指定して開きます。
3. シーンのセットアップ


## 📡 APIエンドポイント仕様 (Python)
- `GET /image`:
  - YOLOの認識枠が描画された最新のカメラフレーム（JPEG画像）を返します。
- `GET /status`:
  - 現在の検知状況（JSON形式）を返します。
  - レスポンス例: `{"is_staying": 0, "stay_time": 0.0}`
- `POST /config`（未実装）:
  - Unity側で設定した判定エリアの頂点座標（ポリゴン）を受け取ります。
