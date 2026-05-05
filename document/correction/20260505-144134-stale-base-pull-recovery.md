# 是正報告書: 古いベースで作業した状態からの pull 取り込み手順

報告日: 2026-05-05
対象: 開発フロー (Git 運用) / `Assets/ScrollAction/`
対象セッション: 2026-05-05 14:00 〜 14:45

---

## 1. 事象

ユーザーが最新でない `main` ベースで作業を進めていた。リモートには `feature/AddSquat` (Crouch=しゃがみ実装) がマージされた `8962e2f` まで進んでおり、ローカルは `6b854e8` ベースに留まっていた。さらにローカルには大量の未コミット変更（修正8ファイル + 新規10エントリ）があり、そのまま `git pull` するとエラーになる状況。

ローカルの作業内容:

- **修正(M)**: 3 シーン (`ScrollActio_bako.unity` / `ScrollAction.unity` / `Shop.unity`), `Player.prefab`, `Shop.prefab`, C# 3 ファイル (`PlayerActionContext.cs` / `PlayerAnimatorBridge.cs` / `PlayerController.cs`)
- **新規(??)**: Jetpack 系一式 (`JetpackAction.cs`, `Jetpack.asset`, `JetpackGaugeUI.cs`), `BackgroundRoot.prefab`, `ForegroundRoot.prefab`, `UI/` フォルダ

リモート側で先行していた変更:

- Crouch（しゃがみ）アクションの実装一式 (`CrouchAction.cs`, `Crouch.asset`, `crouch.anim`)
- 既存3 C# (`PlayerActionContext.cs` / `PlayerAnimatorBridge.cs` / `PlayerController.cs`) の改修
- Player.controller / ActionInventoryDefaults.asset の調整

---

## 2. 取った方針 (フロー全体)

1. `git stash push -u -m "before-pull-2026-05-05"` で全変更（未追跡含む）を退避
2. `git pull --ff-only` で fast-forward 取り込み（`6b854e8` → `8962e2f`、成功）
3. 衝突しうるファイル群について `git diff stash@{0} -- <path>` で差分を精査
4. ファイルごとに「stash 採用 / HEAD 採用 / 手動マージ保留」を判定
5. `git checkout stash@{0} -- <path>` (追跡), `git checkout stash@{0}^3 -- <path>` (未追跡) で個別復元
6. C# は HEAD 採用後に Jetpack 用の入力経路だけ最小限で追記
7. stash@{0} は **drop せず温存** (手戻り保険)

---

## 3. 重要発見

### ローカルとリモートが同じ「Crouch 機能」を二重実装していた

| 観点 | ローカル(stash) | リモート(pull) |
|---|---|---|
| `jetpackHeld` フィールド | 削除 | 削除 |
| `crouchPressed` フィールド | 追加 | 追加 |
| `IsCrouching` プロパティ | 追加 | 追加 |
| Animator `IsCrouching` ハッシュ | 追加 | 追加 |
| ↓/S キー入力検出 | 実装 | 実装 |
| 違い | コメント文言の細部のみ | （同上） |

リモート側にはさらに `CrouchAction.cs` / `Crouch.asset` / `crouch.anim` という**完成形の関連ファイル**まで揃っていた。

### Jetpack 機能はローカル独自の作業

- `JetpackAction.cs` は `ctx.jetpackHeld` を読む実装
- 旧 HEAD (`6b854e8`) には `jetpackHeld` 経路があった
- 新 HEAD (`8962e2f`) ではリモート側の Crouch 改修により `jetpackHeld` が削除されている
- → そのまま JetpackAction を取り込むと**コンパイルエラー**になる

---

## 4. ファイルごとの判定

| ファイル | 判定 | 理由 |
|---|---|---|
| `ScrollActio_bako.unity` | **HEAD 維持** | リモートが背景 GameObject を多数追加。stash 側に無いので採用すると消える |
| `ScrollAction.unity` | **HEAD 維持** | リモートが PrefabInstance を多数追加 |
| `Shop.unity` | **手動マージ保留** | 両方が独立に進化（後述） |
| `Player.prefab` | **stash 採用** | リモート未変更。`JetpackGaugeUI` コンポーネント追加 |
| `Shop.prefab` | **stash 採用** | リモート未変更。商品リストに Jetpack を追加 |
| `Jetpack.asset` (.meta) | **stash 採用** | 新規 |
| `BackgroundRoot.prefab` (.meta) | **stash 採用** | 新規 |
| `ForegroundRoot.prefab` (.meta) | **stash 採用** | 新規 |
| `JetpackAction.cs` (.meta) | **stash 採用** | 新規（※下記の追加修正必須） |
| `UI/JetpackGaugeUI.cs` (.meta), `UI.meta` | **stash 採用** | 新規 |
| `PlayerActionContext.cs` | **HEAD 維持 + 追記** | Crouch は HEAD 採用。Jetpack 用 `jetpackHeld` を1行追加 |
| `PlayerAnimatorBridge.cs` | **HEAD 維持** | Crouch 置換が HEAD で完了済み |
| `PlayerController.cs` | **HEAD 維持 + 追記** | Crouch は HEAD 採用。Jetpack 入力処理と ctx 反映を追加 |

---

## 5. 追加したコード (HEAD ベース ＋ 最小追記)

### `PlayerActionContext.cs`

```csharp
// ジェットパック等「長押し継続」入力。jumpRequested とは別に、押している間 true が立つ
public bool jetpackHeld;
```

### `PlayerController.cs`

フィールド追加:

```csharp
// ジャンプ系キーが押され続けているか (JetpackAction が読む長押し状態)
private bool jetpackHeld;
```

`Update()` 末尾に追加:

```csharp
// ジェットパック用の長押し継続入力。ジャンプキーと同経路だが押下フレームではなく isPressed で読む
jetpackHeld = kb.spaceKey.isPressed || kb.upArrowKey.isPressed || kb.wKey.isPressed;
```

`FixedUpdate()` の ctx 設定部に追加:

```csharp
ctx.jetpackHeld = jetpackHeld;
```

---

## 6. Shop.unity の手動マージ要素（保留中、ユーザー作業）

stash 側で Shop.unity に入っていた独自要素（HEAD ベースに追加したい候補）:

- **BackgroundRoot PrefabInstance**: guid `322c4d552d18d1749981362511170651` を `(28.4, 0, 0)` に配置
- **ForegroundRoot PrefabInstance**: guid `9b29f6c41bf15f148a499f2f46b70826` を `(16.6, 0, 0)` に配置 (IsActive=0)
- **ShopController 子オブジェクト** (`fileID 1660819644623600953`) のスケール: stash 側 `(2, 2, 1)` vs HEAD 側 `(1, 1, 0.5)` → どちらを採用するか要確認

stash 版 Shop.unity をテキストで参照したい場合:

```powershell
git show 'stash@{0}:24hGameJam/Assets/Scenes/Shop.unity' > C:/GitHub/24hGameJam/MCP_Screenshots/Shop.unity.stashed.txt
```

(`MCP_Screenshots/` は gitignore 済みなので追跡されない)

Editor で並べて比較したい場合は `Assets/Scenes/_TempMerge/Shop_stashed.unity` 等に一時配置 → マージ後に削除。

---

## 7. 再利用可能な手順 (今回得た知見)

### A. 「未コミット変更があるまま古いベースで作業していたとき」の標準フロー

1. **状況把握**

    ```powershell
    git status --short
    git fetch origin
    git log --oneline HEAD..origin/main -n 20
    ```

2. **Unity Editor を閉じる** (.meta 自動生成・シーン再書き出しによる stash 競合を避けるため)
3. **退避** (`-u` 必須: untracked も含めて退避)

    ```powershell
    git stash push -u -m "before-pull-<date>"
    git stash list   # 確認
    ```

4. **fast-forward 取り込み**

    ```powershell
    git pull --ff-only
    ```

5. **差分精査**

    ```powershell
    # 追跡済みファイルの差分 (stash→HEAD)
    git diff 'stash@{0}' -- <path>

    # 未追跡ファイルは stash@{0}^3 ツリーに格納されている
    git show 'stash@{0}^3':<path>
    ```

6. **ファイルごとに方針決定** → 個別復元

    ```powershell
    # stash 採用 (追跡済み)
    git checkout 'stash@{0}' -- <path>

    # stash 採用 (未追跡 = 新規追加分)
    git checkout 'stash@{0}^3' -- <path>

    # HEAD 維持: 何もしない
    ```

7. **依存関係の整合性チェック**
    - 取り込んだファイルが API 削除等で動かなくなっていないか
    - 必要最小限の追記でつなぐ（YAGNI 原則: 過剰な互換レイヤを足さない）
    - Unity の場合: コンパイル確認 → シーン/Prefab の参照切れチェック
8. **stash 保管期間**
    - 動作確認＋コミットまでは drop しない
    - 完全に問題ないことを確認後 `git stash drop stash@{0}`

### B. PowerShell で stash 参照するときの落とし穴

- `stash@{0}` は **単一引用符で囲む** (`'stash@{0}'`)。素のままだと `{0}` が format 指定子と解釈される可能性あり
- `git stash show -p stash@{0} -- <path>` は **「Too many revisions」エラー**になる。`git diff stash@{0} -- <path>` を使う
- 未追跡ファイルは `stash@{0}` ツリーには**含まれない**。`stash@{0}^3` に格納されている
  (stash の親構造: `^1`=stash時のHEAD, `^2`=index, `^3`=untracked)

### C. Unity プロジェクト固有の注意

- 退避前に Editor を閉じる
- 同じ機能を「ローカルとリモートが二重実装」している可能性を疑う
  - 単純な pop ではなく差分精査を入れる
- ScriptableObject (`.asset`) や Prefab は YAML テキストなので diff 可能
- 競合解決は最終的に Editor で開いて視覚確認

---

## 8. 残課題 (このセッション後にユーザー対応)

- [ ] Unity Editor 起動 → コンパイル確認 (`JetpackAction.cs` が `ctx.jetpackHeld` を解決できているか)
- [ ] Crouch 機能 (↓/S キー) の動作確認
- [ ] Jetpack 機能 (Space / W / ↑ 長押し) の動作確認
- [ ] `Shop.unity` の手動マージ (上記6.の3要素)
- [ ] 全て問題なければ `git stash drop stash@{0}`

---

## 9. 教訓

- **「未コミット変更を抱えたまま古いベースで進める」事故は起きる**。最初に `git fetch && git log HEAD..origin/<branch>` で先行コミットを確認する習慣をつけると未然に防げる
- **stash pop は便利だが、HEAD と stash で同じ箇所を別実装している場合は手作業の差分判断が必要**。`pop → 競合解決` より `差分精査 → ファイルごと checkout` の方が事故が少ないケースもある
- **「同意図の重複実装」が見つかったら、より完成度の高い側（関連 asset まで揃っている方）を採用する**のが安全。コメントの細部の違いに惑わされない
