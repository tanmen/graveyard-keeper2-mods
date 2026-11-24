# Mods Menu（mod の設定をまとめる窓）

**ESC の窓（ポーズ）に「Mods」のボタンを足し、押すと mod ごとの木のタブで設定を変えられる窓を開く** BepInEx プラグイン。
タブは、Mods Menu に登録した mod の分だけ出る（Mods Menu 自身は設定の行を持たない）。
この repo の 4 つの mod（[Craft Todo](../CraftTodo/README.md)・[Stock Finder](../StockFinder/README.md)・[Quick Stash](../QuickStash/README.md)・[Trade Finder](../TradeFinder/README.md)）が登録する。
ほかの人の mod も、数行で自分のタブを足せる（下の「For mod authors」）。

2026-11-23 に、mod ごとに散らばっていた設定を 1 つの窓にまとめるために作った。Craft Todo 0.7.0〜0.7.5 の「制作メニューの設定」の窓をもとにしている。

## 窓

- ESC の窓の「操作方法」の下に「Mods」（日本語でも「Mods」）。押すと ESC の窓を閉じて Mods の窓を開き、閉じると ESC の窓に戻る（ゲームの「設定」と同じ流れ）
- 見出しの帯に、登録された mod ごとの木のタブ（4 案から選んだ形）。タブはゲームの人物の窓の見出しの帯をそのまま写す
  （両端の宝石の飾り・タブの区切り・選んだタブの明るい地・マウスを乗せたときの明かり）。ゲームパッドでは LB / RB（人物の窓と同じ `NextTab` / `PrevTab`）で送る
- タブの中は、ゲームの設定の窓と同じ部品の行: 節の見出し・オン / オフ・選ぶ（矢印）・数（− ＋ とつまみ）・キー（欄を押して次に押したキーにする）
- 一番下の段は、選んだタブのボタン（Craft Todo の「メモを全部消す」など）と「OK」（閉じる）
- タブを替えても窓の大きさは変わらない（いちばん行の多いタブの高さにそろえる）。タブが帯に入りきらなければ窓を広げ（広げる幅は 360 まで）、
  それでも入らなければ帯の両端に矢印を出して送る
- 最後に開いていたタブを覚えていて、次に開いたときはそこから出す（cfg の `Window.LastPage`）
- 変えた値はすぐ効き、各 mod の cfg に書かれる（BepInEx の `ConfigEntry` にそのまま書く）
- 窓を開いている間と閉じた直後（0.25 秒）は、各 mod のキーが鳴らないように知らせる（`ModsMenuApi.BlocksHotkeys`。キーを選ぶために押したキーで、
  Quick Stash がしまったり Craft Todo が登録したりしないように）
- ゲームの言語を変えたあと・タブが足されたあとに開くと、窓を組み直す（文言は組むときに書く）
- 人物の窓のタブが引けなかったときは、いちばん上に「Mod ◀ 名前 ▶」の切り替えの行を出す（題は「Mods」）

## 確かめ方（`<ゲーム>/BepInEx/LogOutput.log`）

| 行 | 分かること |
| --- | --- |
| `Loading [Mods Menu 0.1.2]` | 読み込まれた（登録する mod より先に読まれる。各 mod が任意の依存にしているため） |
| `パッチ: 成功 1 / 失敗 0` | ESC の窓への差し込みが当たった |
| `タブを登録した: Craft Todo（tanmen.gk2.crafttodo）` など | mod がタブを登録した |
| `ESC の窓に「Mods」のボタンを足した（タブ N）` | ESC の窓を初めて開いたとき |
| `タブ: 人物の窓の帯を写した（…）`・`タブの幅: …` | 木のタブが組めた（引けなければ `タブ: 人物の窓の帯が引けない` → 切り替えの行になる） |
| `タブ: 見出しの帯の幅 W・全部入るのに要る幅 R → 中身の幅 C` | タブのために窓を広げたか（R が W より大きければ広げる） |
| `Mods の窓を組んだ（タブ N・行 M・木のタブ・幅 C）` | 窓が組めた |
| `タブ: 開いたときの見出しの帯の幅 W・並べる範囲 0+3` | 開いたときに全部のタブが並んだか（`（あふれ）` が付けば矢印が出ている） |
| `キーを変えた: <GUID> <節>.<名前> = <キー>` | 窓でキーを変えた |
| `Mods の窓: ゲームの設定の窓の部品が引けない` | 窓が組めず、ボタンを押しても ESC の窓に戻るだけ |

## ビルド・配置

```bash
dotnet test ModsMenu/ModsMenu.Core.Tests
dotnet build ModsMenu/ModsMenu -p:DeployToGame=true
```

mod 本体のビルドにはゲームの導入先が要る（リポジトリの根の `Game.local.props.example` を写して `Game.local.props` を作るか、`-p:GameDir=...`）。
置き場と ⚠ は [docs/modding.md](../docs/modding.md)。調べたゲームの中身は [docs/game-api-notes.md](docs/game-api-notes.md)。
この repo の 4 つの mod は Mods Menu を参照してビルドする（`ProjectReference`・`Private=false`）ので、どれをビルドしても Mods Menu も一緒にビルドされる。
ただし `-p:DeployToGame=true` は参照に渡さない（`GlobalPropertiesToRemove`）ので、mod を配置しても Mods Menu は置かれない。Mods Menu を変えたら、
`ModsMenu/ModsMenu` を直に配置する（並列に配置したときの写しのぶつかりも無くなる）。

⚠ ほかの mod がこの DLL を参照するので、**アセンブリの版（`AssemblyVersion`）は 1.0.0.0 に留める**。mod の版（`Version`）は modinfo.json と
BepInPlugin と `FileVersion` に出る。公開の口（`ModsMenuApi`・`ModPage`）を壊すときだけ `ModsMenuApi.ApiVersion` とアセンブリの版を上げる。

## 作り

| ファイル | 中身 |
| --- | --- |
| `ModsMenu.Core/` | タブの帯の詰め方（入りきらないときの範囲）・数の刻み（つまみの番号と値）・2 度押し。ゲームに依存しないのでテストできる（型は internal） |
| `ModsMenu/ModsMenuApi.cs` | 公開の口（登録・窓が開いているか・言語）と、ゲームの言語・訳文・UI の根・音の境目 |
| `ModsMenu/ModPage.cs` | 公開の口の 1 タブ分（行を足す口）と、登録の一覧 |
| `ModsMenu/MenuWindow.cs` | Mods の窓（ゲームの設定の窓の枠と行を写して組む。タブごとの行の出し入れ・高さをそろえる・キーを選ぶ・下の段のボタン） |
| `ModsMenu/TabBand.cs` | 木のタブ（人物の窓の見出しの帯を写す。幅を測る・入りきらなければ矢印で送る・ゲームパッドの印） |
| `ModsMenu/PauseMenuButton.cs` | ESC の窓に「Mods」のボタンを足す（「操作方法」のボタンを写す）・差し込み・エラーを包む |

---

## For mod authors

Mods Menu adds a **"Mods" button to the pause (ESC) menu** of Graveyard Keeper 2. It opens a window with one wooden tab per registered mod,
built from the game's own settings widgets. Your rows are bound to your BepInEx `ConfigEntry`s, so changes are saved to your `.cfg` and
raise `SettingChanged` as usual. Only mods that register appear; Mods Menu never reads other mods' configs on its own.

### 1. Reference

Reference `ModsMenu.dll` (from `BepInEx/plugins/ModsMenu/` after installing Mods Menu) with **Copy Local = false**
(`<Private>false</Private>`). Do not ship `ModsMenu.dll` with your mod. `ModsMenu.xml` next to it has the API documentation.
The assembly version stays `1.0.0.0` while the API is compatible (`ModsMenuApi.ApiVersion` is `1`).

### 2. Soft dependency (recommended)

Your mod keeps working without Mods Menu. Touch Mods Menu types only inside a method marked `NoInlining`, and call it only when
Mods Menu is loaded:

```csharp
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

[BepInPlugin("you.yourmod", "Your Mod", "1.0.0")]
[BepInDependency("tanmen.gk2.modsmenu", BepInDependency.DependencyFlags.SoftDependency)]
public class YourPlugin : BaseUnityPlugin
{
    ConfigEntry<bool> enabled;
    ConfigEntry<int> count;
    ConfigEntry<KeyboardShortcut> key;

    void Awake()
    {
        enabled = Config.Bind("General", "Enabled", true, "...");
        count = Config.Bind("General", "Count", 5, new ConfigDescription("...", new AcceptableValueRange<int>(1, 20)));
        key = Config.Bind("Keys", "Action", new KeyboardShortcut(KeyCode.H), "...");

        if (Chainloader.PluginInfos.ContainsKey("tanmen.gk2.modsmenu"))
        {
            RegisterMenu();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void RegisterMenu()
    {
        ModsMenu.ModsMenuApi.Register(this)          // tab title = your plugin name
            .Section("General")
            .Toggle(enabled, "Enabled")
            .Number(count, "Count")                   // range from AcceptableValueRange<int>
            .Section("Keys")
            .Key(key, "Action key");
    }
}
```

While the window is open (and for 0.25 s after it closes) `ModsMenuApi.BlocksHotkeys` is true. Skip your own hotkeys then, so the key
pressed to assign a binding does not also trigger your action (wrap the read in a `NoInlining` method too).

### 3. Rows

All methods return the same `ModPage`, so calls chain. Every label is a `string` or a `Func<string>`; the window is rebuilt when the game
language changes, so a `Func<string>` can return translated text (`ModsMenuApi.Language` is the game's language code, for example `"ja"`).

| Method | Row |
| --- | --- |
| `Section(label)` | Heading between two thin lines |
| `Toggle(ConfigEntry<bool>, label)` | Off / On with arrows (the game's own "Off" / "On" text) |
| `Number(ConfigEntry<int>, label)` | − / + and slider; range from the entry's `AcceptableValueRange<int>` |
| `Number(ConfigEntry<int>, label, min, max)` | Same, explicit range |
| `Number(ConfigEntry<float>, label, min, max, step = 1)` | Same for floats, shown with the step's decimals |
| `Choice<TEnum>(ConfigEntry<TEnum>, label, names = null)` | One value of an enum with arrows (`names` gives the text of each value) |
| `Choice<T>(ConfigEntry<T>, label, IList<T> values, Func<T, string> names)` | One of the given values |
| `Key(ConfigEntry<KeyboardShortcut>, label)` | Click, then press a key (Shift / Ctrl / Alt allowed, Esc cancels, left click and gamepad buttons cannot be assigned) |
| `Button(label, Action onClick, confirmLabel = null)` | Button in the bottom row, left of OK; with `confirmLabel`, needs a second click within 3 s |
| `SetOrder(int)` | Tab position (smaller first, default 100; the author's mods use 10–40) |

Other members: `ModsMenuApi.Register(plugin, title)` (fixed or `Func<string>` title), `Unregister(plugin)`, `IsOpen`,
`Localize(key, fallback)` (text of a game localization key). Registering the same plugin again replaces its tab.

### 4. Things to know

- **Fonts**: the game's fonts only contain the characters used by the game's own texts. Others are drawn as nothing (in Japanese, for
  example, "・" is missing). Keep labels short and plain.
- Rows are about 330 units wide; the label column is about 168, so long labels are cut with "…".
- If a row throws while the window is being built, only that tab is skipped (see `LogOutput.log`).
- Built against Graveyard Keeper 2 v1.012.9 (Steam) and the BepInEx 5.4.23.5 bundled with the game.
