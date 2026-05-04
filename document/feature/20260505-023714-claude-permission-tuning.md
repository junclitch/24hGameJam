# Claude Code 権限プロンプト削減 設定まとめ

実装日: 2026-05-05
対象ファイル: `.claude/settings.local.json`

## 背景・動機

Claude Code を使ってゲーム開発をしていると、**「許可しますか？(y/n)」プロンプトが毎回出てきてフロー中断される**のがつらい。
特にこのプロジェクトでは:

- 既に `.claude/` 配下のフォルダはアクセス許可済み
- UnityMCP 経由のオペレーションも信頼できる
- 自分一人での開発（`.claude/` は gitignore 済みなので他人に影響しない）

つまり「**許可済みフォルダ内なら Claude が何やっても OK**」という運用だが、設定が追いついていなかった。

## やったこと

### 1. 既存の使用パターン調査（`fewer-permission-prompts` Skill）

過去 36 セッション（24hGameJam + GRIDLOCKDUNGEON）で計 5,315 ツール呼び出しをスキャンして、何が頻出しているか分析。

**上位パターン:**

| 回数 | パターン | 種別 |
|---|---|---|
| 122 | `mcp__UnityMCP__execute_code` | 任意コード実行 |
| 48 | `mcp__UnityMCP__manage_scene` | シーン操作 |
| 37 | `mcp__UnityMCP__read_console` | 読み取り |
| 30 | `mcp__UnityMCP__refresh_unity` | エディタ操作 |
| 多数 | `Bash(grep|ls|find|cat|wc...)` | ファイル系 |
| 多数 | `Bash(git ...)` | git 系 |

**結論:**
- **read-only 系の追加候補はゼロ**（既に `settings.local.json` の allowlist でカバー済み + Claude Code 標準の auto-allow に含まれている）
- → **ホワイトリストで対処するのは限界**

### 2. `defaultMode` モードの切り替え（採用）

`permissions.defaultMode: "acceptEdits"` を追加。これで **Edit / Write / NotebookEdit が自動承認**になる。

`.claude/settings.local.json` の最終形:

```jsonc
{
  "permissions": {
    "allow": [
      // ... 既存エントリそのまま ...
    ],
    "defaultMode": "acceptEdits"
  }
}
```

## モードの選択肢

| モード | 意味 | リスク |
|---|---|---|
| `default` | 全部聞く（出荷時設定） | 低 |
| `acceptEdits` | Edit/Write は自動、Bash 等の破壊操作は確認 | **中（採用）** |
| `bypassPermissions` | 全部自動承認（`--dangerously-skip-permissions` 相当） | 高 |
| `plan` | プランモード固定 | （別用途） |

## 反映方法

設定ファイルを書き換えただけでは反映されないことがあるので、以下のいずれか:

1. Claude Code を一度終了して再起動
2. セッション中に `/config` コマンドを開く（設定ファイルが再読込される）

## 副作用・注意点

- **gitignore 済みなので公開リスクなし**: `.claude/` は `.gitignore` の 86行目で除外済み（`settings.local.json` は元々 personal 用途のファイルなので問題なし）
- **Bash の破壊的コマンドは引き続き確認される**: `rm -rf`, `git push --force` などは `acceptEdits` でも止まる
- **他人にこの設定を共有したくない場合**は、コミット対象の `settings.json` ではなく `settings.local.json` を使うのが正解（今回の選択）
- **元に戻したい時**: `defaultMode` を `"default"` に変えるか、行ごと削除する

## 参考: その他のプロンプト削減手段

| 方法 | コマンド/設定 | 効果範囲 |
|---|---|---|
| 起動時のみ全許可 | `claude --dangerously-skip-permissions` | そのセッションのみ |
| 個別 Bash パターン許可 | `permissions.allow` に `Bash(npm *)` 等 | 永続 |
| ドメイン別 WebFetch 許可 | `permissions.allow` に `WebFetch(domain:github.com)` 等 | 永続 |
| ホワイトリスト一括見直し | `/fewer-permission-prompts` Skill | 過去ログから自動抽出 |

## 関連ファイル

- `.claude/settings.local.json` — 設定本体
- `.gitignore` 86行目 — `.claude/` 除外ルール

## まとめ

- **やったこと**: `permissions.defaultMode: "acceptEdits"` を追加
- **効果**: コード編集系のプロンプトが出なくなる
- **残るプロンプト**: 破壊的 Bash、未許可の MCP ツール、外部書き込み系
