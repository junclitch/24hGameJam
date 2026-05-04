# `.gitignore` 設定の説明

このリポジトリのルートに置かれている `.gitignore` の方針と、各セクションで何を除外しているかをまとめた資料です。

## 全体方針

- **対象**: Unity 2D プロジェクト（Windows 開発環境）
- **置き場所**: リポジトリルート（`C:\GitHub\24hGameJam\.gitignore`）
- **設計原則**:
  1. Unity / IDE が **再生成できるもの** はコミットしない
  2. **個人環境に依存するもの** はコミットしない（レイアウト・キャッシュ等）
  3. **シークレット** は事故防止のため幅広く除外
  4. **AI アシスタントの設定** はチーム共有しない（メンバーごとに使うツールが違うため）
  5. **再現に必要なもの**（パッケージ定義、プロジェクト設定）は必ずコミットする

## セクション別の説明

### 1. Unity が生成するフォルダ

| パターン | 内容 | 除外理由 |
|---|---|---|
| `[Ll]ibrary/` | アセットインポートキャッシュ | 容量が大きく、Unity が再生成可能 |
| `[Tt]emp/` | エディタ実行中の一時ファイル | エディタ起動のたびに作り直される |
| `[Oo]bj/` | C# コンパイル中間ファイル | 自動生成 |
| `[Bb]uild/` `[Bb]uilds/` | ビルド成果物 | 巨大かつ再ビルド可能 |
| `[Ll]ogs/` | エディタログ | 個人の実行履歴 |
| `[Uu]ser[Ss]ettings/` | エディタの個人設定 | 開発者ごとに異なる |
| `[Mm]emoryCaptures/` | プロファイラのキャプチャ | 大容量・個人作業の成果物 |
| `[Rr]ecordings/` | エディタ録画 | 大容量・個人作業の成果物 |

`[Ll]ibrary/` のように `[]` でくくっているのは、大文字小文字どちらでもマッチさせるため（macOS の大文字小文字を区別しないファイルシステム対策）。

#### `.meta` ファイルのネゲーション

```
!/[Aa]ssets/**/*.meta
```

`Assets/` 配下の `.meta` ファイルは**必ずコミットする**ためのネゲーションルール。`.meta` ファイルには Unity 内部での GUID や import 設定が記録されており、欠落するとアセット参照が壊れる。

#### Rider プラグイン

```
/[Aa]ssets/Plugins/Editor/JetBrains*
```

Rider が自動配置するエディタ拡張は除外（Rider 利用者の環境で再生成される）。

### 2. Visual Studio / Rider / VSCode

| パターン | 何を除外するか |
|---|---|
| `.vs/` | VS のキャッシュ・ウィンドウレイアウト・ブレークポイント等 |
| `.idea/` | Rider / IntelliJ 系のワークスペース設定 |
| `.vscode/` | VSCode のワークスペース設定 |
| `*.csproj` `*.sln` `*.unityproj` | Unity が毎回再生成する。手動編集しても上書きされる |
| `*.suo` `*.user` `*.userprefs` | 個人ごとのソリューション設定 |
| `*.pidb` `*.booproj` `*.svd` `*.pdb` `*.mdb` `*.opendb` `*.VC.db` | デバッグ・古い MonoDevelop・VC++ などの中間ファイル |
| `ExportedObj/` `.consulo/` | 各種 IDE のエクスポート/設定フォルダ |
| `sysinfo.txt` | Unity のクラッシュレポート |

### 3. ビルド成果物

| パターン | 用途 |
|---|---|
| `*.apk` `*.aab` | Android ビルド |
| `*.ipa` | iOS ビルド |
| `*.app` | macOS ビルド |
| `*.unitypackage` | Unity パッケージのエクスポート |
| `crashlytics-build.properties` | Firebase Crashlytics の生成ファイル |

### 4. OS が作るファイル

| パターン | OS |
|---|---|
| `.DS_Store` | macOS |
| `Thumbs.db` | Windows（フォルダのサムネキャッシュ） |
| `desktop.ini` | Windows |

### 5. シークレット / 署名鍵

事故防止のため幅広く除外している。**ジャムでは使う機会が少ないが、誤コミットの被害が大きい**ため保険として入れている。

| パターン | 内容 |
|---|---|
| `.env` `.env.*` | 環境変数（API キー等が入りがち） |
| `*.keystore` `*.jks` | Android アプリ署名鍵 |
| `*.p12` | 証明書全般（iOS 署名等で使用） |
| `*.mobileprovision` | iOS プロビジョニングプロファイル |

これらをコミットしてしまうと、Git の履歴に永久に残ってしまうため、**.gitignore で防ぐのが基本**。

### 6. AI アシスタント関連

メンバーごとに使う AI ツールが異なるため（Claude Code / GitHub Copilot / Antigravity 等）、それぞれの設定ファイルを共有しないようにしている。

| ツール | 除外パターン |
|---|---|
| Claude Code | `.claude/`, `CLAUDE.md`, `.clauderules` |
| GitHub Copilot | `.copilot/`, `.github/copilot-instructions.md`, `.github/copilot-*.md`, `copilot-*.md` |
| Google Antigravity | `.antigravity/`, `.antigravityignore` |
| Gemini | `.geminiignore`, `.agent/`, `GEMINI.md` |

各メンバーは自分が使うツールの設定をローカルでだけ持つ運用。

## 追跡（コミット）されるもの

逆に、**必ずコミットされる重要ファイル**は以下のとおり。

| パス | 役割 |
|---|---|
| `Assets/` 配下すべて | ゲームの全アセット（スクリプト、シーン、画像、`.meta` 含む） |
| `Packages/manifest.json` | 使用パッケージ一覧。これがないと依存パッケージが復元できない |
| `Packages/packages-lock.json` | パッケージのバージョンロック |
| `ProjectSettings/` 配下すべて | プロジェクト設定（入力設定、グラフィックス設定、ビルド設定 等） |
| `ProjectSettings/ProjectVersion.txt` | Unity のバージョン情報。チームで Unity バージョンを揃えるのに必要 |
| `24hGameJam/.vsconfig` | VS Installer 用の必要コンポーネント定義（VS 利用メンバーの環境構築を補助） |

## 検証方法

特定のファイルが除外/追跡されているかを確認するには:

```powershell
# 除外されているか確認（出力があれば除外、なければ追跡）
git check-ignore -v <path>

# 例
git check-ignore -v 24hGameJam/Library/
git check-ignore -v 24hGameJam/Assets/Scenes/SampleScene.unity
```

## 変更時の注意

- ルールを追加するときは、**実在のパスで `git check-ignore -v` で検証する**
- 一度追跡してしまったファイルを後から除外したい場合は、`.gitignore` に追加するだけでは効かない。`git rm --cached <path>` でインデックスから外す必要がある
- AI アシスタント関連のファイル名はツールのアップデートで変わることがあるため、新しいツールを使い始めたら都度見直す
