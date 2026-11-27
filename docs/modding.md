# Graveyard Keeper 2 の mod の作り方（2026-11-21 時点の調べ）

ゲーム v1.012.9（Steam、buildid 26734308）・Unity 6000.3.9f1（Mono）で調べた。版が上がったら引き直す。

一次情報はゲームに同梱の **`README_MODDING.txt`**（`GraveyardKeeper2.exe` の隣。ゲーム内で Shift+F10 を押すと
セーブデータの `Mods/Code/README.txt` にも書き出される）。この文書はその要点と、実際に通して分かったこと。

## 1. 2 種類の code mod（どちらも公式に受け付ける）

| 種類 | 中身 | 向き |
| --- | --- | --- |
| **BepInEx プラグイン** | `[BepInPlugin]` の付いたクラスを持つ DLL | Harmony のパッチで既存の処理に差し込む mod。この repo の mod はこちら |
| ネイティブ mod | DLL ＋ `modinfo.json`（`entryType` / `entryMethod` / `loadPhase`）。ゲームが自分で読む | BepInEx 無しで動く（Steam Deck・Linux でも起動オプション不要）。Harmony を使うなら `0Harmony.dll` を同梱する |

1 つの item はどちらか一方だけ。同じコードを両方の形で入れない。

- **Steam 版（Windows）は BepInEx 5.4.23.5 を同梱している**（`winhttp.dll`・`doorstop_config.ini`・`BepInEx/`）。
  プレイヤーが BepInEx を入れる必要は無い。BepInEx 6・IL2CPP 用のプラグインは読まれない
- `BepInEx/patchers/GK2.WorkshopSync/GK2.WorkshopSync.dll` は**ゲーム自身の部品**（導入時刻がゲーム本体と同じ）。
  Workshop・ローカルの mod の `BepInEx/` を、プラグインの読み込みの前にゲームの `BepInEx/` へ写す役。
  ⚠ 他所の「Workshop ローダー」を `BepInEx/patchers` に入れると、この写しが全部止まる（README_MODDING.txt の 4）

## 2. 置き方（BepInEx プラグイン）

Workshop の item も、手元で試すローカルの mod も同じ形:

```
<item>/
  modinfo.json                     任意。Mods の窓に出す名前と版
  BepInEx/
    plugins/
      <YourMod>/                   他の mod と被らない名前
        YourMod.dll
    config/
      <plugin GUID>.cfg            任意。既定の設定（プレイヤーが持っていなければだけ写される）
```

```json
{ "schemaVersion": 1, "id": "yourmod", "name": "Your Mod", "version": "1.0.0" }
```

- **手元で試す置き場**: `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\<name>\`
  （名前が `~` で始まるフォルダは読まれない＝無効化の手軽な方法）
- 起動時に `GK2.WorkshopSync` が `<item>/BepInEx/` を `<ゲーム>/BepInEx/` へ写し、そのあと BepInEx がプラグインを読む。
  **ゲームが動かすのは `<ゲーム>/BepInEx/plugins/` の写し**で、item の中のファイルではない。自分のファイルは
  `Path.GetDirectoryName(Info.Location)` から相対で読む
- 設定は `BepInEx/config`（`Config.Bind`）。**`BepInEx/plugins` に書かない**（更新のたびに差し替え、無効化で消される）
- 写されないもの: `BepInEx/core`・`cache`・`patchers`・`*.log`・`BepInEx.cfg`・`BepInEx/` の外のもの
- 依存は `[BepInDependency]` と、Workshop のページの Required Items の両方に書く
- ZIP・インストーラ（`.bat` / `.ps1`）・自前のローダー（`winhttp.dll` など）・BepInEx 本体・`0Harmony.dll` は item に入れない

反映のタイミング: ゲームを閉じている間に置いた・直したものは次の起動で入る。動いている間に置いたものは再起動後。

## 3. ビルド

- `net472`（または `netstandard2.0`）。参照はゲームの `GraveyardKeeper2_Data/Managed` の `Assembly-CSharp`・`LazyBearTechnology`・
  Unity の DLL と、同梱の `BepInEx/core` の `BepInEx.dll`・`0Harmony.dll`（どれも `<Private>false</Private>`。配らない）
- Unity 6 なので `Object.FindObjectOfType` などは非推奨（`FindAnyObjectByType` / `FindObjectsByType`）。警告をエラーにしているとここで止まる
- 新しい Input System は入っていない（`Unity.InputSystem.dll` が無い）。旧 `Input` と BepInEx の `KeyboardShortcut` が使える
- ゲームのコードを読むには `ilspycmd -p -o <出力先> -r <Managed> <Managed>/Assembly-CSharp.dll`（出力はリポジトリに入れない）

## 4. ログと確かめ方

| どこ | 何が出る |
| --- | --- |
| `<ゲーム>/BepInEx/LogOutput.log` | BepInEx とプラグインのログ。`[GK2.WorkshopSync] Copied plugins/...` で写しが、`Loading [<名前> <版>]` で読み込みが分かる |
| セーブデータの `Player.log` | Unity のログ。`[Mods]` で始まる行が mod の有効化（`Enabled new code mod '...' (local:<id>)`） |

⚠ **MSIX でパッケージされたアプリの中（そのアプリのターミナルなど）から `AppData\LocalLow` を読み書きすると、仮想化で実ディスクとずれる**
ことがある（書いたものがゲームから見えない・古い版が読める。エラーは出ない）。そのときは `\\localhost\C$\Users\<ユーザー>\AppData\LocalLow\...` 越しに扱う。
ゲームの導入先（Steam のライブラリ）は仮想化されない。

⚠ 確かめるために起動したゲームが、そのままセーブ枠を読み込んでプレイ中になっていることがある（2026-11-21、起動から 1 分足らずで
`Continue Game: slotSteam_1` が出ていた。続きを選んだのか自動なのかは未確認）。セーブの書き込み中に落とさないよう、
プロセスを殺さずゲームの中から終える。

## 5. Workshop に上げる

ゲームの中の Workshop Creator を使う（README_MODDING.txt の 5・6。窓の作りはゲームの `UISteamWorkshopCreatorWindow` /
`SteamWorkshopCreatorService` を読んだ。v1.012.9）。

1. 上げるフォルダを組む。mod ごとに、セーブデータの `Mods/Upload/<名前>/` に Release のビルドと `<mod>/workshop/` の中身を置く:

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\stage-workshop.ps1
   ```

   `Mods/Code/` には置かない（手元で試している mod と同じ物が 2 つ読まれないように）。`-Mod CraftTodo` で 1 つだけ組める
2. セーブデータの `Mods/workshop.json` の `"workshopCreatorMode"` を `true` にする（上げ終わったら `false` に戻してよい）
3. ゲームの中で Shift+F11 → 作る窓で、題（Workshop のページの題）を入れ、**Type を `Mod` に替え**（窓を開いた既定は `Translation`）、
   `...` で 1. のフォルダを選んで作る
4. 新しい item は **Unlisted** で作られる。Steam のページで次を済ませてから Public にする:
   - Craft Todo・Stock Finder・Quick Stash・Trade Finder は、Owner Controls の Add/Remove Required Items に **Mods Menu** を足す
     （設定の窓が Mods Menu の中にあるため。任意の依存なので、購読しなくても動く）
   - 初めて上げるときは Steam Workshop の利用規約への同意が要る（同意するまでページは誰にも見えない）
5. 版を上げたら `<mod>/workshop/_changenote.txt` を書き換えて 1. から組み直し、Shift+F11 の一覧からその item を選んで上げ直す
   （上げ直しでは公開の範囲は変わらない）

フォルダの中で決まった名前のもの（どれも Workshop の中身には入らない）:

| 名前 | 中身 |
| --- | --- |
| `_description.bbcode` | ページの説明（BBCode。上げるたびに上書きされる）。この repo では英語の下に日本語 |
| `_changenote.txt` | その回の変更点。無ければ「Initial upload」/「Updated」 |
| `Thumbnail.png`（`.jpg`） | プレビュー画像。**無いと、フォルダを選んだ時点でゲームが真っ黒の 256x256 を作る**。Steam の上限は 1 MB。ゲームは `.png` / `.jpg` / `.jpeg` しか探さず、中身は読まずに Steam へ渡すので、動く GIF にしたいときは中身を GIF のまま `Thumbnail.png` の名前で置く（Quick Stash がそう） |

`_` で始まるファイル・フォルダと、`~` で始まるフォルダも上がらない。

## 6. 参考にした所

- ゲーム同梱の `README_MODDING.txt`（Code mod contract v1）—— 一次情報
- [Thunderstore の BepInExPack（Graveyard Keeper 2）](https://thunderstore.io/c/graveyard-keeper-2/p/BepInEx/BepInExPack/) —— コミュニティの導入手順。
  Steam 版は同梱しているので、手で入れる手順は要らない
- [Best Mods for Graveyard Keeper 2（VGTimes）](https://vgtimes.com/articles/171468-best-mods-for-graveyard-keeper-2.html) —— 既存の mod の例（GK2 Mod Framework など）
