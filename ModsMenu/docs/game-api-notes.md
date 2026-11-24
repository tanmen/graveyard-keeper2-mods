# Mods Menu が使うゲームの中身（2026-11-23 に読んだ）

ゲーム v1.012.9（Steam）を `ilspycmd` で逆コンパイルした結果と、Addressables の bundle を UnityPy 1.25.3 で読んだ結果。版が上がったら引き直す。
窓の組み方（GenericWindowLayout・設定の窓の行・ESC の窓）の土台は [Craft Todo の調べ](../../CraftTodo/docs/game-api-notes.md) の
「Mod の窓・ESC の窓・設定の窓」。ここには Mods Menu で新しく分かったことだけを書く。

## 人物の窓の木のタブ（TabBand の拠り所）

- タブは `CharacterWindow` の private の `charPageTabButtonParent`（GameObject。帯）と `charPageTabButtonPrefab`（`CharPageTabButton`）。
  `Init` で手本を隠し、ページごとに `charPageTabButtonPrefab.Copy(帯)` → `Init(ページの名前, 押したとき, 最後のタブか)`
- 窓は `CharacterWindow_Big.prefab`（bundle `73c8a4dad644a69f711e2f8d7c8588ac.bundle`）と `CharacterWindow_Small.prefab`（`4671f638e362b6a68579f7f98d0a0ead.bundle`）。
  どちらを読むかは `GUIElements.LoadWindowFrom` が画面の大きさの種類（`uiWindowSizeType`）で決める。bundle の名前はハッシュなので版が上がったら引き直す
  （23,543 個の bundle の `container` のキーを並列で引いて 5 分。引いた道具はリポジトリに入れていない）
- **`LazyUI.GetWindow<CharacterWindow>()` で、開かずに読み込める**: キャッシュに無ければ `GUIElements.LoadWindowFrom` が
  `Addressables.LoadAssetAsync<GameObject>(key).WaitForCompletion()` → `Instantiate(UI の根)` → 中の `ILazyGUIElement` を全部 `Init` → 隠して取っておく。
  ゲームが人物の窓を初めて開くときと同じ流れなので、こちらから読ませても副作用は小さい（同じ物がゲームのキャッシュに入る）
- 帯 `Content/HeaderGroup/Windows Tabs Content`（Big の値）: anchor (0,0.5)-(1,0.5)・高さ 26、`HorizontalLayoutGroup`（間 **-1**・中央・幅を子に合わせる）＋
  `ContentSizeFitter`（横 preferred）→ 見出しの帯の真ん中に、タブの合計の幅で置かれる。子:
  - `DecorLeft` / `DecorRight`: 宝石の飾り 60x26（ignoreLayout）。帯の端から x ∓9 に中心 → **帯の外へ 39 はみ出す**
  - `PrevTabGamepadHelper` / `NextTabGamepadHelper`: `<sprite name=xbox_button-LB>` / `-RB` の文字 30x30（ignoreLayout、帯の端から ∓17）
  - タブ `Window Tab Button`: `CharPageTabButton`＋`LazyButton`＋`ContentSizeFitter`。中の `Content` が左右 13 の余白の `HorizontalLayoutGroup` で、
    子は `RaycastZone`（透明・当たり）・`DecorSeparator`（区切り 40x26、右端。`rightSeparator`。最後のタブでは消す）・`ActiveBack`（選んだタブの地。`activeGameObject`）・
    `Selection`（マウスを乗せたときの明かり）・`Label`（大きさ 16、`ContentSizeFitter`）・`Action Indicator`（6x10）→ **タブの幅 = 文字の幅＋26**（「Character」で 61＋26）
- `CharPageTabButton.UpdateState(選んだか)`: `ActiveBack` を出し入れし、文字の形（`selectedLabelStyle` / `normalLabelStyle`）を当て、選んだタブは押せなくする。
  `Init(name, …)` の `name` は訳文の鍵で、`OnEnable` のたびに `LLBase.L(name)` で文字を書き直す → **自前の文字を出すなら `name` に null を渡す**（`SetLabel` は空なら何もしない）
- 見出しの帯 `Content/HeaderGroup`（Big）: 左右 11 内側・上 11・高さ 26。子は `Background`（木の帯、Sliced）・`Windows Tabs Content`・`CloseButton`（26x26、右端）だけで、
  **題の文字は無い**（所持品の窓の中の `MultiInventoryWidget/Frame/HeaderGroup` は題 `Header` と両端の `DecorCommonLeft` / `Right` 28x26 を持つ）
- タブを送るキーは `GameKey.NextTab` / `PrevTab`（`LazyWindow.GetGameKeyDelegates` を上書きして足す。人物の窓と同じ）。ゲームパッドのときは `LazyAudio.PlayAndForget("tab_click")`

## 設定の窓の行の細かいところ

- `UISwitchButton.Initialize(callback, fields, index, header)` の `fields` は**そのまま出す**（訳さない）。選択の行に好きな文言を渡せる
- `UISlider` はつまみが動くと `amountLabel.text = (slider.value * step).ToString()` を書いてから `onChangedCallback(slider.value * step)` を呼ぶ。
  つまみを刻みの番号で持つ（Mods Menu の `StepScale`）なら、呼び返しの中で欄を実際の値に書き直す（しないと欄に番号が出る）
- `GenericWindowLayout` は `UpdateSize(isSmall)` で見出しを出し入れし、中身の上の余白を 47（見出しあり）/ 24 に替える
- **設定の窓の枠の幅は、中身の入れ物 `Content` の幅で決まる**（MainScene の bundle `c60e66cc68d755ea127a83fe6db6bfb2.bundle` の
  `UI/UIRoot/UIGameSettingsWindow/GenericWIndowLayout` を読んだ。綴りの `WIndow` はゲームのまま）:
  - 根: `ContentSizeFitter`（横も縦も preferred）＋`VerticalLayoutGroup`（余白 左右 24・上 47・下 23、子の幅は決めない）＋`LayoutElement`（最小 200x100）。
    根の大きさを直に変えても fitter が上書きする
  - `Content`: 幅 **340 に固定**（`ContentSizeFitter` は縦だけ）＋`VerticalLayoutGroup`（間 6・上 3・下 2・真ん中そろえ・子の幅は決めない）。行は 330x16
  - だから枠は 340＋48＝388、見出しの帯（`Frame/HeaderGroup`、左右 11 内側）は 366。窓を広げるなら `Content.sizeDelta.x` を足す
    （Mods Menu 0.1.0 は `Content` の中の行を広げて効かず、3 枚目のタブが矢印の先に隠れた。2026-11-23）
  - `Frame` は根いっぱい、`Frame/BackMask` は左右 13・上 36・下 13 内側（大きさ -26x-49、位置 y -11.5）

## 日本語の書体に有る字の数え方

- ゲームの日本語の書体は訳文に出てくる字しか持たない（Craft Todo 0.7.0 で「・」「縦」が描かれなかった）
- **`resources.assets` を丸ごと数えると見分けられない**（ほかの言語の訳文などに「縦」が 18 回・「・」が 1 回ある）。日本語の訳文 `lng_ja`
  （`LL` の MonoBehaviour、1,955,504 バイト）の生のバイト列だけで数えると、どちらも 0 回になる（2026-11-23）。名前は生のバイトの 28 バイト目からの長さ付き文字列
  （m_GameObject 12・m_Enabled 4・m_Script 12 のあと）。訳文の言語は de・en・es・fr・ja・ko・pl・pt-br・ru・tr・zh_cn の 11
- 数える道具はリポジトリに入れていない。作るなら、対照に「縦」を混ぜて 0 になるのを見てから使う
