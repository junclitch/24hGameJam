# UnityMCP 導入手順

このリポジトリに [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) を導入した手順をまとめた資料です。AIアシスタント（Claude Code等）から Unity Editor を操作できるようにするための MCP サーバーです。

## 概要

- **対象パッケージ**: `com.coplaydev.unity-mcp`
- **取得元**: GitHub (`https://github.com/CoplayDev/unity-mcp.git`)
- **ブランチ**: `main`
- **サブパス**: `/MCPForUnity`
- **検証時バージョン**: v9.6.8

## 全体構成

UnityMCP は3層構造になっている。共有範囲を理解した上で進めること。

| レイヤ | 役割 | リポジトリ共有 |
|---|---|---|
| Unity パッケージ（ブリッジ） | Unity Editor 内で動作。MCPサーバーと通信し、Editor 操作を実行 | ✓ コミット |
| MCP サーバー（Python製） | AIクライアントと Unity ブリッジを中継 | ✗ 各自インストール |
| AIクライアント設定（`.mcp.json`等） | Claude Code 等が MCP サーバーへ接続するための設定 | ✗ 各自生成 |

## A. リポジトリへのセットアップ（一度だけ・コミット対象）

### A-1. Unity パッケージの追加

`24hGameJam/Packages/manifest.json` の `dependencies` に以下を追加。

```json
{
  "dependencies": {
    "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
    ...
  }
}
```

URL の構成:

| 部分 | 意味 |
|---|---|
| `https://github.com/CoplayDev/unity-mcp.git` | GitHub リポジトリの URL |
| `?path=/MCPForUnity` | リポジトリ内のサブディレクトリ指定 |
| `#main` | 追跡するブランチ（タグやコミットハッシュも指定可） |

### A-2. パッケージの自動解決

Unity Editor を起動（または Package Manager をリフレッシュ）すると、自動的にパッケージが取得され `Packages/packages-lock.json` が更新される。

更新後の `packages-lock.json` には以下が追加される:

```json
"com.coplaydev.unity-mcp": {
  "version": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
  "depth": 0,
  "source": "git",
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.0.2",
    "com.unity.test-framework": "1.1.31"
  },
  "hash": "b92c05a25820cfc9f59ce4094eb46aaec8632ea2"
}
```

依存パッケージとして `com.unity.nuget.newtonsoft-json` も自動的に解決される。

### A-3. コミット対象ファイル

| ファイル | 役割 |
|---|---|
| `24hGameJam/Packages/manifest.json` | パッケージ依存定義（Git URL含む） |
| `24hGameJam/Packages/packages-lock.json` | バージョンロック（hash値で固定） |

両方をコミットすることで、他メンバーが clone した際に **同じバージョンの UnityMCP パッケージ** が自動復元される。

## B. 個人環境セットアップ（各メンバーが自分のPCで実施）

ここから先は **コミット対象外**。各メンバーが MCP を使いたい場合のみ実施する。使わない場合はスキップしても Unity プロジェクト自体には影響しない。

### B-1. 前提ソフトのインストール

MCP サーバー本体は Python 製のため、以下が必須。

| ソフト | バージョン | 入手元 |
|---|---|---|
| Python | 3.10 以降 | https://www.python.org/downloads/windows/ |
| uv (パッケージマネージャ) | 最新 | 後述の PowerShell コマンド |

**Python インストール時の注意**: 「Add Python to PATH」のチェックを必ず ON にすること。

**uv のインストール (PowerShell)**:
```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

確認:
```powershell
python --version
uv --version
```

### B-2. Local Setup ウィンドウで前提確認

Unity Editor のメニューから **Local Setup Window** を開く。

- Python と UV Package Manager が両方 **緑色 (Found)** になっていることを確認
- 赤色 (Not Found) の場合は B-1 のインストールを見直し、**Refresh** ボタンで再検出
- 全部緑になったら **Done** をクリック

### B-3. MCP メインウィンドウの操作

Unity Editor のメニューから **Toggle MCP Window** を開く。`MCP For Unity` というウィンドウが開き、`Connect` タブに以下のセクションが並んでいる。

#### Local Server セクション

- Transport: `HTTP Local`（デフォルト）
- HTTP URL: `http://127.0.0.1:8080`
- **Start Server** をクリック → サーバー起動
- 状態表示が `No Session ●` から接続済み状態に変わる
  - 初回は uv が MCP サーバー本体を自動ダウンロードするため少し時間がかかる

#### Client Configuration セクション

- Client: ドロップダウンから **Claude Code** を選択
- Claude CLI Path / Client Project Dir が自動検出されていることを確認
- **Configure** をクリック → `.mcp.json` が `Client Project Dir` に自動生成される
  - 状態表示が `Not Configured ●` から緑色に変わる
- **Install Skills** をクリック（任意・推奨）
  - `~/.claude/skills/` に Unity MCP 用の Skill ファイルが追加される
  - AI が Unity 操作の標準手順・ベストプラクティスを参照できるようになり、操作品質が向上する

> **メモ**: Configure 後すぐに状態表示が緑（Configured）に変わる。これは `.mcp.json` が正しく生成されたことを示す Unity 側の表示で、Claude Code がツールを認識したかどうかとは別物。

### B-4. 動作確認

Claude Code で「Unity の現在のシーンに何があるか教えて」のようなプロンプトを投げ、Unity 側に問い合わせるツール呼び出しが発生すれば接続成功。

Claude Code は新しい `.mcp.json` を動的に検知することがあるため、再起動なしで使えるケースが多い。**ツールが認識されない場合のみ** Claude Code を再起動する。

### Unity Editor のメニュー一覧

| メニュー | 用途 |
|---|---|
| Toggle MCP Window | メインの操作パネル（サーバー起動・クライアント設定）。通常はこれを使う |
| Local Setup Window | 初回環境構築用（Python/uv の検出）。前提が揃わないときに使う |
| Edit EditorPrefs | EditorPrefs に保存された MCP 設定を直接編集（上級者向け） |

## C. .gitignore 設定

UnityMCP 導入に伴い、以下を `.gitignore` に追加済み。

### C-1. MCP クライアント設定ファイル

```gitignore
# =============================================================
# MCP (Model Context Protocol) settings
# =============================================================
.mcp.json
mcp.json
.cursor/mcp.json
.mcp/
```

**ignore する理由**:
- `.mcp.json` には環境依存のローカルパス（CLI パス、プロジェクトディレクトリ等）が含まれることがあり、他人の環境にコピーすると壊れる
- メンバーによって使うAIクライアント（Claude Code / Cursor / VSCode 等）が異なる
- MCP を使う・使わないも個人の選択

### C-2. 個人ワークスペース

```gitignore
# =============================================================
# Indivisual workspace
# =============================================================
colla_workspace/
```

UnityMCP を介して AI が一時ファイル・キャプチャ等を保存する作業領域。メンバーごとに作業内容が異なるため共有しない。

## トラブルシューティング

### パッケージが解決されない

- Unity Editor の Package Manager を一度閉じて再度開く
- `24hGameJam/Library/PackageCache/` を削除してから Unity を再起動（キャッシュクリア）
- ネットワーク経由で GitHub からの取得に失敗していないか確認（プロキシ環境等）

### Local Setup Window で Python / uv が Not Found のまま

- インストール後にターミナルや Unity Editor を再起動して PATH を再読込
- `python --version` `uv --version` がコマンドプロンプトで通ることを確認
- Python インストール時に「Add Python to PATH」を忘れた場合は再インストール、または手動で環境変数 PATH を追加

### Configure を押しても Claude Code でツールが使えない

- `Client Project Dir` が正しい Unity プロジェクトのパスか確認
- 生成された `.mcp.json` が Claude Code の認識する場所にあるか確認
- Claude Code を完全終了してから再起動（動的検知が効かないケースの保険）

### バージョンを固定したい

`#main` を特定のコミットハッシュやタグに変更する。

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v1.2.3"
```

`main` を追跡したままだと、他メンバーが pull したタイミングで取得バージョンが変わる可能性がある。安定運用したい場合は固定を推奨。

## 参考リンク

- CoplayDev/unity-mcp: https://github.com/CoplayDev/unity-mcp
- Unity Package Manager (Git URL): https://docs.unity3d.com/Manual/upm-git.html
- Python (Windows): https://www.python.org/downloads/windows/
- uv インストールガイド: https://docs.astral.sh/uv/getting-started/installation/
