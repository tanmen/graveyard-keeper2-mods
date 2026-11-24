# ゲーム側の API の調べ（GK2 v1.012.9、2026-11-21）

`Assembly-CSharp.dll` と `LazyBearTechnology.dll` を手元で逆コンパイル（`ilspycmd`。結果はリポジトリに入れない）して読んだ要点。
mod 本体（`CraftTodo/CraftTodo/`）を書くときの拠り所。版が上がったら引き直す。

```bash
M="D:/SteamLibrary/steamapps/common/Graveyard Keeper 2/GraveyardKeeper2_Data/Managed"
ilspycmd -p -o <出力先> -r "$M" "$M/Assembly-CSharp.dll"
ilspycmd -p -o <出力先> -r "$M" "$M/LazyBearTechnology.dll"
```

クラスは `LazyBearTechnology` 名前空間（UI の基盤・文言・スプライト）以外、ほぼ全部が名前空間なし。

## 窓は 2 系統（＋町の建物の窓。下の 3 つ目の節）

### 建物の窓（`UIBuildingWindow`）—— 建築机で開く。スクショの「工場」の窓

- 行は `UIBuildingWidget`（`LazyWidget<UIBuildingWidgetData>`）。タブは `BuildingWindowTab`、末尾の「Move」「取り除く」も同じ行の部品
  （`BuildData.IsMoveSelection` / `BuildingMode == BuildingDef.BuildingMode.Remove`）
- 行を押すと `private void UIBuildingWidget.OnPress()`: `data.CanBuild(data.GetCurrentNeedItems())` が偽なら**何もしない**
  → ここに Prefix を差して「足りなければメモへ」。素材のセルを押しても同じ `OnPress` に来る
- 素材: `data.BuildData.NeedItems`（`List<NeedItemData>`。グループのまま）。名前の id は `BuildData.Definition.id`、
  アイコンは `BuildData.IconId`
- 窓を開いた建築机: 窓の `data`（`UIBuildingWindowData`）の `AssignedWgo.Data`（`WgoData`）。行から窓は `GetComponentInParent<UIBuildingWindow>()`
- 行を押して作れるとき（`BuildManager.OpenBuildingWindow` の中の `OnBuildPressed`）: `BuildingMode.Script` の建物は**その場で建つ**
  （素材を引いて `expressionAfterBuilding` を評価）。それ以外は `BuildManager.EnableBuildMode` で設置の画面に入る
- **設置の確定は `public bool BuildPointer.TryBuildActionInput()` が true を返したとき**（`build_place` の音と `BuildBuilding` の
  イベントもここ）。設置中の建物は `private BuildPointerObject pointerObject` の `BuildData`、選んだ建築机は
  `LazySingleton<BuildManager>.Instance` の `private Wgo currentBuildDesk`（窓を開いた机と同じ）。設置をやめる（戻る・右クリック）と
  `BuildController.DisableBuildMode` → `BuildManager.Disable`（建物の窓を開き直す）

### 製作の窓（`UICraftWindow` → `UICraftSelectionWindow`）—— 作業台で開く

- 作業台の窓はレシピのアイコン（`UICraftPreviewItemCell`）の格子。押すと必ず回数を選ぶ窓
  `UICraftSelectionWindow`（燃料は `UIFuelCraftWindow`）が開く。どれも `UIBaseCraftSelectionWindow` を継ぐ
- 回数を選ぶ窓の「作る」ボタンは `protected LazyButton startCraftButton`。作れないときは `interactable = false`
  （`data.CanStartCraft`）
- **`LazyButton` は押せないときのクリックを `onNotInteractableClick` で知らせる** → `UIBaseCraftSelectionWindow.Init` の
  Postfix でここに聞き手を足す。ゲームパッド・キーは `GameKey.StartCraft` → `protected bool OnStartCraft()`（押せなければ false を返すだけ）
- 素材: `data.CraftItemCellsData`（`UICraftItemCellData`。`initialItem` がグループのまま、`currentItem` は選んだ種類）。
  数は `need.GetCount(data.WgoData) * data.CraftsCount`。`needDurability > 0` のセルは耐久を使う道具
- 窓のボタン: 「作る」は `protected virtual void OnStartCraftPressed()`（`UISingleCraftWindow` は上書きしていて **base を呼ばない**。
  そちらは動いている製作がある（`data.CraftQueue.Count > 0`）とボタンが「更新」になり、回数を直すだけ）。作業台にゾンビの作業者が
  いると「作る」は出ない（`UICraftSelectionWindow.Redraw`）。どちらのボタンも最後は `CraftInteractionHandler.OnCraftPressed` に来て、
  `Player.log` に `Adding Craft: id: …, count: …, addToQueue: …` を出す（「作る」が `True`、「キューに追加する」が `False`）
- 回数: `data.CraftsCount`（既定 1。`MaxCraftsCount` は 999）
- 「キューに追加する」は素材が無くても押せる（キューは素材を待つ）
- **実際に作り始めるのは `CraftComponent` の private `Start(CraftElementBase)`**: キューの要素の `Count` を 1 減らし、複製に素材を引かせて
  （`RemoveRequirements`）、`craftableObject.OnCraftStart(複製)` を呼ぶ。キューを通らない製作（`skipQueue`）は `ProcessInstantCraft` が
  1 回分ずつ同じ順に呼ぶ。`ICraftable` を実装しているのは `WgoData` だけなので、**`public void WgoData.OnCraftStart(CraftElementBase ce)` が
  1 回分を作り始めるたびに 1 度鳴る**（窓の「作る」・キュー・ゾンビの作業のどれでも）。始まり済みの要素を続けるときは呼ばれない。
  メモはここで 1 減らす（0.4.2〜）。0.4.1 までは窓の「作る」で減らしていて、2026-11-21 に「キューに追加する」でパンくずを作ったとき
  （`addToQueue: False`）メモが減らなかった
- 完了は private `Finish()`（`UpdateCountOnFinish` → 出来上がりを出して `OnCraftFinish`）。`Made craft output as common item [...]` が出る

### 町の建物の窓（`UITownBuildingWindow`）—— 町の看板などで開く。住居の「大きな家を修理する」の窓（2026-11-22 に読んだ。0.7.1 の拠り所）

- 3 系統目の窓。`UITownBuildingWindow : LazyWindow<UITownBuildingWindowData>`。データは `AssignedWgo`（窓を開いた机。看板・建物）・
  `BuildsToDisplay`（`List<TownBuildingDef>`）・`OnBuildPressed`・`PlayerData`。窓の題は `UIInfoWidget` に `AssignedWgo.Data` を渡したもので、
  名前は `LLBase.L(wgoData.id)`（制作メニューの制作機の区切りと同じ引き方）
- 行は `UITownBuildingWidget : LazyWidget<UITownBuildingWidgetData>`。データは `TownBuildingDef`・`CraftItemCellsData`（`TownBuildingDef.needItems` から）・
  `MultiInventory`（窓が作った `new MultiInventory(PlayerData)` = 持ち物＋いまいるゾーン）・`GetCurrentNeedItems()`。名前は `LLBase.L(TownBuildingDef.id)`、
  アイコンは `TownBuildingDef.iconId`（無ければ `i_b_goc_tent`）。素材のセルは行の子で、押すと行の private `OnPress()` に来る。
  ゲームはこの行にツールチップを出さない（`OnOver` は null）
- 開く所は 2 つ: `TownBuildingPlaceInteractionHandler.Interact`（看板）と `Flow_OpenTownBuildingWindow`（スクリプト。`isForLevelUp` なら
  繋がった建物の机）。どちらも `OnBuildPressed` で `new MultiInventory(PlayerController.PlayerData).HasItemsById(...)` が偽なら**何もしない**
- **建てるのは製作**: ゲームは町の建物ごとに `town_building_craft:＋建物の id`（`LazyConsts.TOWN_BUILDING_CRAFT_PREFIX`）の `CraftDef` を持つ
  （`GameBalance.CreateTownBuildingCrafts`。素材は建物と同じ `needItems`、`onCraftEndExpressions` の頭が `CreateTownBuilding()`）。
  足りれば窓を閉じて暗転し、素材を引いて、`AssignedWgo.Data.CraftComponent.ProcessInstantCraft` でその製作を 1 回作る →
  **`WgoData.OnCraftStart` が窓を開いた机の上で 1 度鳴る**（`ce.Def.id` が `town_building_craft:…`）。クエストのスクリプトで建てる
  `TownSystem.StartTownBuildingCraftOnWgoFromScript` も `TryStartCraft` で同じ製作を始める
- → 制作メニューは町の建物を製作のメモ（`TodoKind.Craft`、レシピの id は `town_building_craft:…`、作る場所は窓を開いた机）として持ち、
  減らすのは製作と同じ `CraftUnitStartedPatch`。名前・ツールチップだけ頭を外して `TownBuildingDef` で引く（`GameBridge.TownBuildingIdOf`）

## マウスの下のレシピを引く（登録キー）

- ゲームの UI は uGUI ＋ EventSystem（入力は `RewiredStandaloneInputModule`）。`EventSystem.current.RaycastAll` に
  `Input.mousePosition` の `PointerEventData` を渡すと、当たった部品が手前から返る。親を `GetComponentInParent` で辿って部品を見分ける
- 部品と、その `data`（`LazyWidget<T>` の protected な field）:
  `UIBuildingWidget`（`UIBuildingWidgetData.BuildData`）・`UITownBuildingWidget`（`UITownBuildingWidgetData.TownBuildingDef`。窓を開いた机は
  `GetComponentInParent<UITownBuildingWindow>()` の `data.AssignedWgo.Data`）・`UICraftPreviewItemCell`（`UICraftPreviewItemCellData.CraftDef` / `WgoData` /
  `IsTab` / `IsUnknown`）・`UICraftQueueElementWidget`（`UICraftQueueElementWidgetData.CraftQueueElement`（`CraftElementBase.Def` / `Count`）・
  `WgoData`）・`UICraftWidget`（`UIBaseCraftWidgetData.CraftDefinition` / `CraftsCount`）・`UIBaseCraftSelectionWindow`（窓全体）
- レシピの素材は `CraftDefBase.needItems`。耐久を使う道具は `CraftDef.hasDurabilityUseItem` / `durabilityUseItem`
- 旧 `Input` が効く（新しい Input System は入っていない）ので、BepInEx の `KeyboardShortcut` に `KeyCode.Mouse2` を渡せる
- ⚠ BepInEx 5.4.23.5 の `KeyboardShortcut.IsDown()` は、**ショートカットに無いキー（マウスのボタン以外の全部）が押されていると false**
  （`ModifierKeyTest` の `_modifierBlockKeyCodes`）。だから `Mouse2` は Shift を押している間は鳴らず、`Mouse2 + LeftShift` と重ならない。
  そのかわり、移動キーを押したままだと登録キーも効かない
- UI の効果音: `LazyAudio.PlayAndForget("gui_click")`（ゲームの中で 6 か所。`gui_hover_light` は 20 か所）

## 在庫の数え方

- 作業台が素材を探す範囲は `WgoData.GetCraftableMultiInventory()`: 作業台の `WorldZoneData` の収納（`new MultiInventory(zone)`。
  `includePlayerInventory` 既定 false）＋ 作業台の在庫 ＋ プレイヤーが同じゾーンにいれば持ち物
- 建物の窓は `new MultiInventory(PlayerData)`（持ち物 ＋ プレイヤーのいるゾーン）
- → メモの「使える数」は **持ち物 ＋ 作る場所のゾーンの収納**（どこにいても、戻ったときの数になる）
- 作る場所は `WgoData.UniqueId`（`SGuid`。`ToString()` が id の文字列）で持ち、`MainGame.WorldData.GetWgoData(new SGuid(id))` で引き直す
- グループ素材の中身: `new NeedItemData(groupId, 1).TryGetGroupItemDefs(out List<ItemDef>)`

## 名前・アイコン・言語

- 文言: `LLBase.L(id)` / `LLBase.HasL(id)`。言語は `LLBase.CurrentLang`（日本語は `"ja"`）
- アイテム名: `ItemDef.GetHeader()`（星付きの品質も扱う）。`GameBalance.Me.GetDataOrNull<ItemDef>(id)`
- 設置物の名前: `LLBase.L(wgoData.id)`（`UIInfoWidgetData` と同じ）
- スプライト: `LazySingletonSO<EasySpritesCollection>.Instance.GetSprite(id)`。アイテムは `ItemDef.iconId`
- 書体は言語ごとに違う。`TextStyle.ApplyStyle(label)` がいまの言語の書体と材質を選ぶ（`StaticFontLabel` を付けた文字は切り替えない）。
  建物の行の `UIBuildingWidget` に `canBuildStyle` / `canNotBuildStyle` / `descriptionDefaultStyle` と `nameLabel` / `descriptionLabel` がある

## 画面に自分の UI を足す

- ゲーム自身の `UISteamWorkshopCreatorWindow` がコードで uGUI を組んでいる。UI の根は `GUIElements.Instance` の
  `private UIFitter uiFitter`（無ければ `GUIElements.Root`）。その下に `Canvas`（`overrideSorting`・`sortingOrder`）と `GraphicRaycaster`
- 並び順の定数（`GUIElements`）: 吹き出し 40 / HUD 50 / カットシーン 300 / 台詞 350 / ツールチップ 700 / 暗転 800
- `GUIElements.SetVisibilityState(false)`（演出で UI を消す）は根の `CanvasGroup.alpha` を 0 にする → 根の下に置けば一緒に消える
- HUD（`HUD`）は `Hide()` で GameObject ごと非表示になる。隠れる理由は `HudStateType`（MainMenu / Cinematic / BuildController / CinematicsScene）。
  戦闘中は `HUD.Mode == HUDMode.Fight`。左上の枠は `private GameObject leftUpGroup`（MainScene の `HUD/LeftUpHudWidget`、日の輪の 120x120）
- 左上の「？」は `private LazyButton tutorialListBtn`（`LeftUpHudWidget/TutorialButton`、44x44。輪の左上の角から (23, -120) に中心 →
  **輪の下端より 22 下まで出る**）。`HasViewedTutorials()` かつゲームパッドでなく、操作できる間だけ出る（`UpdateTutorialListButton`。
  窓を開くと操作できなくなるので消える）
- 左下の `ControlsWidgetMouse`（戦闘の操作の 160x52）は戦闘中だけ。ふだんの左下は空いている
- ゲームは UI の上ではマウスの左クリックの攻撃などを止める（`MouseAimHelper.IsPointerOverUI` = `EventSystem.IsPointerOverGameObject`）。
  当たる面（raycastTarget）を広げると、その上で攻撃できなくなる
- Unity 6: `FindObjectOfType` は非推奨（`FindAnyObjectByType`）

## UI の絵（見た目を合わせるとき）

**ゲームの UI はドット絵**。sprite は ppu 50 で、窓の部品は 1 単位 = 絵の 1 画素で置かれている（日の輪 120x120 の絵が 120x120 の枠、
エネルギーの枠の背景 164x52 が 164x52）。アイテム・建物のアイコンはたいてい 32x32（`i_wood` は 43x27、`i_b_builder_sawmill` は 42x41 など
はみ出すものもある。ゲームのセルも等倍で中央に置くだけなので、はみ出たまま描く）。引き伸ばすと画素が崩れるので、絵は等倍か 2 のべき乗倍で使う。

- HUD・窓は Addressables ではなく **`Assets/_Scenes/MainScene.unity` に常駐**（`HUD`・`LeftUpHudWidget`・`WorldZoneWidget`・
  `UITooltip`・`UIBuildingWindow`・`UICraftWindow` など）。閉じていても在るので `Resources.FindObjectsOfTypeAll<T>()` で引ける
- 建物の窓の板: `UIBuildingWindow/Content/Frame` の Image（`comm-frame_1-border`、250x100、縁 L87 B34 R87 T34、Sliced。
  角飾りの付いた青灰の枠で中は透明）＋ その子の `BackMask`（4 辺を 13 内側へ）の下の `Back`（`comm-frame_bg_2`、636x406 の石板。
  **Simple のまま引き伸ばさずマスクで切り抜く**）。制作メニューの本体の板はこれを写している（`PanelSkin`）
- **所持品の窓（制作メニューの制作機の区切りと、0.4.4 の 1 件ごとの形の元。MainScene に常駐）**: `InventoryWidget` の `Container`（縦並び・`reverseArrangement`・
  spacing -1 → 見出し板を地に 1 重ねて上に描く）に、見出し板 `InventoryHeaderWidget`（`comm-header_2`、226x26、縁 L80 R80。
  field `headerBackgroundActiveSprite`・文字は `header` に `headerActiveStyle`（TextStyle）、大きさ 16、板いっぱいに中央揃え。
  両端の飾り `DecotLeft` / `DecotRight` は `comm-header_2-dec_side_1`（22x26）を端にぴったり、右は scale -1）と、
  セルの下の茶色の地 `Grid`（`widget_inventory-items_bg-active`、178x48、縁 60/20/60/20、Sliced。field `backgroundActiveSprite`。
  GridLayoutGroup は上 3・下 5 の余白、マス 42）。地の絵の行は 0 が上の縁・1〜43 が同じ行の繰り返し・44〜47 が下の縁
  （2026-11-21 に画素で数えた）なので、縦の縁を 1 / 4 にした写しを作っても見え方は変わらない（Sliced は縁の合計 40 より低く置くと
  縁を縮めて描くので、制作メニューの 0.5.1〜 の札 1 枚だけの地はこの写しを使う）。地は `UIAtlas` にある
- **セル `UIItemCell`**（窓ごとに地が違う。所持品は `background` の sprite が `widget_items_cell-inventory_item_cell`、44x44）:
  マス 42 の中央に地 44、アイコン `icon` は中央（ItemIcon の枠 48x48）、数 `countLabel` は右下の角から 5・5 内側に 44x10・右上揃え・
  大きさ 16（TextStyle は `countLabelNormal` / 足りないとき `countLabelRed`）
- **アイテムのアイコンは純粋な青 (0,0,255) の縁を持つ**。セルは専用の材質（`icon.material`）に
  `ImageExtensions.BlueColorReplace(image, colors.NormalColor)`（public static の拡張メソッド）で縁の色を渡して描く。
  `colors` は `ImageColors`（所持品は (0.1216, 0.0986, 0.0941)）。材質を写さずに描くと縁が青いまま出る
- **技術ツリーの札 `TechTreeElementWidget`（制作メニューの料理ごとの札の元。0.5.0〜）**: MainScene にプールの元が常駐
  （bundle `c60e66cc…` の path_id 1213、126x64）。`Container` の下に状態ごとの子があり、基底 `TechTreeElementBaseWidget` の
  `hiddenObj` / `visibleObj` / `availableObj` / `unlockedObj` が指す: `Hidden`（`tech_tree-tech_plate-unknown`）・
  `Visible/Back`（`-unavailable`）・`Available/Back`（`-available`、緑）・`Unlocked/Back`（`-opened`、金）。札の後ろの `Image` は
  `-slot`（130x66）、選んだときの枠 `Selection` は `tech_frame_selection`（Sliced）。名札の文字は状態ごとの `HeaderLabel`
  （field `headers` の配列。上端に寄せて y -8・124x16・大きさ 16・中央・色 FEE6B8）で、`TextStyleComponent`（LazyBearTechnology）が
  TextStyle を持つ（`CurrentTextStyle`。金の札と緑の札で別の TextStyle）。中のアイコンは `Content`（高さ 32、札の中心から 1 下）に
  `LinkedEntityWidget`（44x44 の `comm-item_cell-dark`、`icon` は 48x48 で材質付き）を並べ、足に `PriceLabel`（技術点の値段）。
  名札の TextStyle は小さな書体の `tiny_font-tech_palte_opened` / `tiny_font-tech_palte_available`（大きさ 16）。
  `icon` の縁の色は材質の `_Color`（元の白のまま）ではなく、同じ GameObject の `BlueColorReplaceComponent` が Image の色
  （(0.1216, 0.1216, 0.1569) = 1F1F28）を `BlueColorReplace` に渡す（0.5.0〜0.5.3 は材質の白を読んで縁が白く出た）
- **`TextStyle.ApplyStyle` は揃え方を戻す**: 中で `LanguageModLoader.ApplyDirection` が、初めて当てたときの `alignment` を
  `LanguageRtlLabelState` に覚えておき、当てるたびにそれへ戻す（右から左の言語で反転するため）。揃え方を決める前に 1 度当てた文字に
  もう 1 度当てると、揃え方が初めの値（TMP の既定の左上）に戻る。制作メニューの 0.5.3 は名札の文字がこれで左上にずれた →
  当てる前の揃え方を取っておき、当てたあとに戻す
- **ゲームのツールチップ `UITooltip`（2026-11-21 に逆コンパイルと MainScene の bundle で調べた。制作メニューのセルが 0.6.0 から使う: `TooltipBridge`）**:
  窓は 1 つ（private static `instance`。`Init` で入る）で、出し方ごとに中身の部品の並び（`List<LazyWidgetDataBase>`）を組み立てて
  private `ShowAtTargetAndCalculateOffsets(list, target, …)` に渡す。**public static の `Show(list, target, ForceCornerPosition = Auto,
  follow = true, TooltipPlacementPriority = TopRight)` がそれをそのまま呼ぶ**ので、出すのに反射は要らない（`instance` が空だと落ちるので、
  空かどうかだけ反射で見る）。`UITooltip.Hide()` / `HideImmediately()` で消す。`IsTooltipShowingAtTarget(target)` は follow で出したときだけ当たる。
  ゲームパッドのときは出さない（`UIMouseTooltip.IsAvailable`）。出すたびに `gui_hover_light` が鳴る。窓を開く・閉じるとゲームが消す。
  ⚠ 相手の RectTransform が壊れても**窓は残る**（`Update` は Unity の null 比較で追うのを止めるだけ）→ 自分のセルが消えるとき（`OnDisable`）に、
  自分に出ていれば `HideImmediately`。ゲームの `UIMouseTooltip` は `OnPointerExit` で `eventData.fullyExited` のときだけ消す
  - 反射で引くもの（private static）: `AddItemWidgets(list, ItemDef, addHeader, headerPrefix, notShowStudyWidget, nowShowCraftedAt, IReadOnlyList<ItemDef>)`・
    `AddItemEffectWidgets(list, ItemDef, IReadOnlyList<ItemDef>)`・`TryAddRequiredExtensionWidget(list, CraftDef, WgoData)`・
    `AddBuildingWidgets(list, BuildingDef, addHeader, headerPrefix)`（見出しだけ）・`TryAddDescriptionWidget(list, id)`（`<id>_d` があれば足して true）。
    文字の形は private の TextStyle 欄（`headerTextStyle`・`headerBoldTextStyle`・`…NotEnough`（赤）・`…Gold`・`smallDescriptionTextStyle`・
    `selectedSmallDescriptionTextStyle`）。`TextStyle.ApplyStyleToString` は null を返すことがある（ゲームも `?? ""`）
  - `TryAddRequiredExtensionWidget` は、直前の部品が「作成場所」の文なら、その文に改行して続けて書く（`WgoData` の拡張で作れるなら出さない）
  - 「レシピ名＋技能のアイコン＋レベル」（`ShowCraftInfo`）: 技能は制作機の `WGODef.talent`、届いているかは
    `IWorker.GetMasteryLevelForTalentBranch(talentId, CraftDefBase = null) >= craftDef.talentLock`（`MainGame.PlayerController` は `PlayerController` で
    IWorker）。`"id".FontIcon()`（`<sprite name="id">`）と `NOBR()` は LazyBearTechnology の `UnityEngine.UI.UIExtensions`
  - 素材の並び `new UITooltipNeedsItemWidgetData(CraftDef, MultiInventory)`: `needItems`＋耐久の道具。持っている数は
    `MultiInventory.GetTotalCount(品)`（グループは持っている品を先にした最初の品）。プレイヤーの持ち物が変わると描き直す
  - 「作成場所」の索引 `GameBalance.craftInItemsCacheShownInTooltips`（`CreateCraftInItemsCache`）: `isHidden`・`rem_`・`set_`・`town_building_craft:` を除き、
    `outputItems`・`addItemsToWgoOnStart` / `OnFinish`・`dropFromWgoItemsEnd` / `Start` に出る品ごとに `craftsIn` を集める（スター群・グループは中の品へ開く）。
    `doNotShowInTooltips` のレシピはツールチップの方には入れない。制作メニューの「作り方の索引」も同じ選び方で、レシピ（CraftDef）の方を持つ
    （加えて作成場所の無いもの・`fake_`・`mix` を除く）
  - 置いてある台: `MainGame.WorldData.GetWgoDataList(id)`（鍵は `WgoData.id` = 定義の id。`craftsIn` の文字列と同じ）
  - 見た目: 地 `hint-frame`（羊皮紙色 156x114・Sliced 縁 20）・中の余白 14・部品の間 2・最小の幅 60。区切りは `hint-text_separator`（96x6）。
    角の矢印 `hint-frame_tail`（10x10）。文字の形は `small_font-hint_header`（見出し 5E2100）・`small_font-hint_regular_text`（説明 784F26）・
    `tiny_font-hint_additional_text`（小さい文 946E44）・`…_selected`（784F26）・`small_font-hint_receive`（緑 357200）・
    `small_font-hint_give_away`（赤 E73B3B）・`small_font_bold-hint_header_1`（太字 4E3012）。素材の並びのセルは `comm-item_cell-dark`（44x44）
  - 位置: 相手の RectTransform の上端の中央から `offsetAdd`（右 3・上 4）の点に、窓の左下の角（左から 17・下から 6）を合わせて右上に出す
    （画面に入らなければ左右・上下を入れ替える）
  - 中身の種類: `ShowItemCell`（所持品のアイテム。private static `AddItemWidgets(list, itemDef, …)`: 名前・区切り・説明 `<id>_d`・
    効果・作成場所・使い方）／`ShowCraftInfo`（制作の窓のレシピ。「レシピ名＋技能のアイコン＋レベル」・効果・要る拡張・素材の並び
    （持っている数/要る数）。`UIItemCell` の `DisplayingOutputPreview` と制作機の `WgoData` が要る）／`ShowLinkedEntity`（技術ツリーのセル。
    名前・アイテムの中身・作成場所・素材の並び）／`ShowSimpleInfo(target, text, …, header)`（見出しと文だけ。public）
  - 訳文は `Resources/Locales/lng_<言語>`（`resources.assets` の `LL`。型の情報が無いので UnityPy では生のバイトを
    m_GameObject・m_Enabled・m_Script・m_Name・aliases1・aliases2・id・txtIds・txts の順に読む。日本語は 9,494 件）。
    素材（ニンジンなど）には説明文の `<id>_d` が無いものが多い
- **札の名札の文字は帯の真ん中より 1 上に出る**（日本語、2026-11-21 に 0.5.4 の画面を画素で数えた）: `HeaderLabel` と同じ枠
  （上端から 0〜16・上下中央 `m_VerticalAlignment` 512・余白 0。bundle で確かめた）に置くと、文字（縁取り込みで 12 行）が札の行 1〜12、
  帯は行 1〜14 なので上 0・下 2。制作メニューは枠を 1 下げる（`PlateParts.NameTop`）。所持品の見出し板（帯 23 行）の文字は 14 行で
  上 4・下 5（差が奇数なので、これ以上は寄せられない）
- **札の絵の行**（`-opened` / `-available` の 126x64。2026-11-21 に画素で数えた）: 0 黒い縁（列 2〜123）・1〜14 名札の帯・15〜16 区切り・
  17〜50 中・51〜60 足（値段の欄）・61 金の線（列 1〜124）・62 黒い縁（列 2〜123）・63 透明。縁はどれも左右の端にしか無く、
  中央の列 32〜93 は 1 回おきに左右反転して繰り返しても模様がつながる。札のセル `tech_tree-tech_plate-item_cell`（32x32、
  中 (39,38,42)・縁 (103,102,105)・角は透明）は札の部品からは参照されておらず、SpriteAtlas から名前で引く。
  札の絵もセルも `UIAtlasCompressed`（石板の地 `comm-frame_bg_2` と同じアトラス。四角い詰め方・回転なし・端の切り詰めなし）にあるので、
  `textureRect` の中の範囲を `Sprite.Create` で切れる（制作メニューは足を外した 54 行と、横に広げる切れ端をこれで作る）
- **人物の窓の木のタブ帯**（`main_window-header_1` 228x26 縁 L96 R96・タブ `main_window-header_1-button` 100x26 縁 20/20・
  宝石 `-dec_left` / `-dec_right` 60x26・両端 `-dec_side_3` 28x26）は `CharacterWindow_Big.prefab`（Addressables で開いたときだけ読まれる）
  にしか無い。**枠の中に帯を入れる形**は同じ prefab の `Content/Pages/CharMainPage/MultiInventoryWidget/Frame`: 枠 `comm-frame_1-border` の
  子の `HeaderGroup` が上・左右から 11 内側に高さ 26（帯を Sliced、両端に `-dec_side_3` をぴったり・右は scale -1、文字は大きさ 16 で、
  右の閉じるボタンの分 44 を空けて中央）、`BackMask` は左右 12・上下 13 内側（制作メニューの 0.4.1 はこの形。タブと宝石は使わない）。
  sprite はどれも SpriteAtlas に入っていて（帯とタブは `UIAtlas`、飾りは `UIAtlasCompressed`。所持品の見出し板と同じアトラス）、
  sprite が `m_SpriteAtlas` でアトラスを指すので、MainScene の絵が読まれた時点でアトラスも読まれている →
  `Resources.FindObjectsOfTypeAll<SpriteAtlas>()` の `GetSprite(名前)` で引ける（呼ぶたびに複製を作るので覚えておく）
- **UI の倍率**: `ResolutionConfig.GetUiLayout`。`PixelSize`（設定。2560x1440 なら既定 2）が `min(幅/658, 高さ/424)` 以下なら
  その値がそのまま UI の倍率。2560x1440・PixelSize 2 → 倍率 2（UI は 1280x720 単位）。`Player.log` の
  `ApplyScreenMode Resolution: … PixelSize: …` で確かめられる
- ほかの候補（どれも `UIAtlas`）: 右上の地名の枠 `hud-world_zone-location`（150x36、縁 60/8/60/12。縦に伸ばすと斑点が筋になる）、
  左上の技術点の板 `hud-gameres_frame`（68x20、縦の縁なし）、ツールチップ `hint-frame`（羊皮紙色 156x114、縁 20。文字は暗い色が前提）
- 入れ子の Canvas は `referencePixelsPerUnit` を根の Canvas に揃える（Sliced の縁の太さがこの値で決まる。既定の 100 のままだと
  ゲームの 50 に対して縁が 2 倍になる）
- **石板の地 `comm-frame_bg_2`（636x406）は上下の端ほど明るいグラデーション**（行の平均の明るさが端 41.7・真ん中 29.3）で、
  上 3 行・下 1 行は 1 色だけのベタ。ゲームの窓は 1 枚を下に揃えて置き、上をマスクで切るだけ（所持品の窓の `Back` は
  `comm-frame_bg_1` を下端に揃えて Simple）。406 を超えて Tiled で敷くと継ぎ目にベタの帯が出る → 制作メニューはベタの行を削った範囲を
  `Sprite.Create` で作り、1 枚おきに上下反転して継ぐ（0.4.2〜）
- **スクロール**: 所持品の窓（`UIMultiInventoryWindow/Container/MIW_CloseBtn`）は `Inventory Scroll`（ScrollRect。枠の上から 36・下から 13・
  左右 12 内側）＋ `Viewport` ＋ `InventoryContent` で、**スクロールバーは無い**。下に `FadeBot`（`comm-content_fade_1`、10x10、縁 L4 R4、
  色 (26,25,31) で下 4 行だけ不透明度 12→109）を、枠の左右 13 内側の幅で中身の下端に重ねる。ホイールはゲームの
  `SmoothMouseWheelScroll.EnsureForItem(scrollRect, 1 マスの高さ)`（所持品は 42。滑らかに動くぶん、止まる位置が小数になる）。
  ゲームの Viewport はほとんど `RectMask2D`。アイコンの材質のシェーダー `Unlit/UI Transparent Blue Color Replace` は
  `UNITY_UI_CLIP_RECT` を持つので `RectMask2D` で切り抜ける（bundle `6a5cfae2…` のシェーダーのキーワードで確かめた）

**素材の数**: 製作の窓のセルは `needItems` ＋（`hasDurabilityUseItem` なら）`durabilityUseItem`（`UIBaseCraftSelectionWindowData.FillCraftItemCellsData`）。
道具はふつう `needItems` の中にあり、`needItemsDurabilityUseIndex` がその位置を指す。2026-11-21 に `GameBalance`（下）のバイト列から
`List<NeedItemData>` を走査した結果、材料の列は 2620 本（1 種 1790・2 種 386・3 種 439・4 種以上 5 本。4 種以上は箱や作物の一覧・
報酬・お酒の候補でレシピではない）。対照として野菜サラダ（`gr_onions` ×2・`carrot` ×2・`beet` ×2）が拾えている。
`hasDurabilityUseItem` の数は走査では確かめきれなかったので、mod が起動時に `GameBalance.Me` から数えてログに出す（`レシピの素材のセルの数`）。
0.4.1 の起動で出た値（v1.012.9）: 製作 833 件のセルは 0 個 245・1 個 157・2 個 222・3 個 209（最大 3、`hasDurabilityUseItem` は 0 件）、
建物 622 件は 0 個 350・1 個 32・2 個 87・3 個 153（最大 3）→ セルは作る物＋素材 3 つまで

資産を手元で見る（読むだけ。出力はリポジトリに入れない）: `UnityPy`（1.25 で確認）で
`<ゲーム>/GraveyardKeeper2_Data/StreamingAssets/aa/StandaloneWindows64/*.bundle` を読む。⚠ bundle には Unity の版が書かれていないので
`UnityPy.config.FALLBACK_UNITY_VERSION = "6000.3.9f1"` を先に設定する（しないと**全部の bundle が読めず 0 件**になる）。
MonoScript は別の bundle にあるので、Image は型の木のキー（`m_Sprite` と `m_FillCenter`）で見分ける。sprite の参照先は
`externals` の CAB 名で、どの bundle がその CAB を持つかは bundle を開いて `files` のキーで引く（2026-11-21 は UIAtlas が
`e41f208c…`、UIAtlasCompressed が `83e91d60…`、MainScene が `c60e66cc…`。名前はハッシュなので版が上がったら引き直す）。
ゲームの定義 `GameBalance` は `Resources.Load("GameBalance")` で、`<ゲーム>/GraveyardKeeper2_Data/resources.assets` の MonoBehaviour
（4.3MB）。こちらは**型の木が省かれている**ので UnityPy でもそのままは読めない（`get_raw_data()` の生のバイト列を、逆コンパイルした
型の並びに沿って読む。`NeedItemData` は string `id`・`LazyExpression count`（string 2 つ・int・float・bool）・int `groupType`）

## Mod の窓・ESC の窓・設定の窓（2026-11-21 に読んだ。制作メニューの 0.7.0 の設定の窓の拠り所）

- **ゲームの Mods の窓は `LazyBearTechnology.UICodeModsWindow`**（Assembly-CSharp の中）。開けるのはメインメニューの丸いボタン
  （`UIMainMenuWindow.OnModsButtonClicked`）だけで、ゲーム中は開けない。中身は mod ごとの札（名前・版・状態）と「有効 / 無効」
  「Workshop」のボタンだけで、mod の設定を載せる口は無い。版は modinfo.json の `version`（README_MODDING.txt の 7）
- この窓は**コードで組まれていて、自作の窓の手本になる**: UI の根（`GUIElements` の private `uiFitter`）の下に GameObject を作り、
  `Canvas`（overrideSorting・sortingOrder 480）・`GraphicRaycaster`・`GamepadNavigationController` を付けてから
  `LazyWindow<LazyWidgetDataBase>` を継いだ部品を AddComponent し、`Init()` → `Open(null)`。見た目は `UIGameSettingsWindow` の
  `GenericWindowLayout`（枠 `comm-frame_1-border`・暗い地 `comm-content_bg_dark-side-small`・木の帯 `main_window-header_1`・閉じるボタン）を
  Instantiate して `Content` の子を消し、`Frame/HeaderGroup/Header` の LocalizedLabel を消して題を書く。閉じるボタンは
  `Frame/HeaderGroup/CloseButton` の LazyButton を `closeButton` に入れておくと `LazyWindow.Init` が Close につなぐ。
  `LazyWindow` を継ぐと ESC（`GameKey.Back` → `OnPressedBack`）で閉じ、窓の重なり（`LazyWindowsStackController`）にも載る。
  抽象のメンバーは `TestDraw()` だけ
- **設定の窓 `UIGameSettingsWindow` の部品**（MainScene に常駐。`Resources.FindObjectsOfTypeAll` で引ける）: private の
  `vSyncButton` などが `UISwitchButton`（行 330x16: 名前 `LeftName` 168 幅・`ToLeft` / `ToRight` 25x25 の矢印・値の欄 `Back`
  （`comm-cell_dark_2`、118x17）の中に `Value`）。`Initialize(Action<int>, string[] fields, index, header)`・`UpdateField(index, fireCallback)`。
  音量は `UISlider`（`Minus`・`Slider`（118x18）・`Plus`・`Value`）で、`Initialize(Action<float>, value, step)` は 0〜100 を step で刻む作り
  （`slider.maxValue = 100 / step`）。別の範囲にするなら private の `slider`（UnityEngine.UI.Slider）の min / max と `step`・`onChangedCallback` を
  直に入れる（− ＋ は `slider.value` を 1 ずつ動かす）。下の「OK」は `lazyButton`（`UIDialogWindowButton`、`Draw(new UIDialogWindowData.ButtonData(...))`）。
  中身の並びは VerticalLayoutGroup（上 3・下 2・間 6）。写した部品は `LocalizedLabel.IgnoreLocalize = true` にしないと文言が訳文に戻る
- オン / オフの文言はゲームの訳文 `ui_off` / `ui_on`（日本語は「オフ」「オン」）
- **ESC の窓は `UIGamePauseWindow`**（MainScene に常駐）。ボタンは private の `settingsBtn`・`tutorialListBtn`（実行時に作る）・
  `controlsBtn`・`goToMenuBtn`（162x26、`comm-btn-simple_red-active`）。`Init` でゲーム自身が `goToMenuBtn` を写して「不具合を報告」を足す
  （`CreateBugReportButton`: Instantiate して SetSiblingIndex、`onClick.RemoveAllListeners` → AddListener、`SetCallbacksIntoGamepadNavigationItem`）。
  「設定」を押すと `Close()` してから設定の窓を開き、その `onClosed` で `Open(null)` し直す。制作メニューは `Open` の Prefix で
  `controlsBtn` を写して「操作方法」の下に足す（`Init` が mod より先に済んでいても足せるように）
- **右下の知らせは `UINotificator`**（`LazySingleton`。アイテムを拾った・所持品がいっぱいなどと同じ部品）。知らせは右下に付け
  （anchor (1,0)）、`appearPoint` から `startPoint` へ滑り込み、`displayingTime`（既定 4 秒）で引っ込む。重なったら上へ積む。
  public の `ShowSimpleTextNotification(string locale)` は訳文の鍵しか受けない（`LLBase.L(locale)` を出す）ので、文をそのまま出すなら
  private の `ShowSimpleTextNotification(string locale, string text)` を反射で呼ぶ（制作メニューの F9 の知らせ。`GameNotice`）。
  ⚠ `LazySingleton<T>.Instance` は、見つからないと空の GameObject に部品を付けて作ってしまうので、`Resources.FindObjectsOfTypeAll` で探す
- 確かめの窓は `LazyUI.GetWindow<UIDialogWindow>().Open(new UIDialogWindowData(題, 文, はい, いいえ))`。自作の窓（並び順 480）との
  重なり順は確かめていないので、制作メニューの「メモを全部消す」は確かめの窓を使わず 2 度押しにした

## ゲームの状態

- `MainGame.Instance`、`MainGame.PlayerData`、`MainGame.WorldData`
- セーブ枠: `MainGame.Instance.SaveSlotData.slotName`（`Steam_1` など。ファイルは `Steam_1.dat` と `.info`）。`ContinueGame` と新しいゲーム
  （`StartNewGameWithSlotName`。保存の日時 `saveDateTime` は空）で設定される
- **セーブの流れ（2026-11-21 に読んだ。制作メニューの 0.6.1 はメモをこれに合わせる: `TodoStorage`）**:
  - ゲームが保存するのは `EnergySystem.StopSleeping`（寝て起きたとき。`sleepWithoutSavingGame` なら保存しない）の
    `SaveSystem.Save(MainGame.Instance.SaveSlotData, MainGame.Instance.GameSave)` と、枠の写し（別の GameSave）だけ。終了時には保存しない
  - `Save` は同期で、`RedirectSaveFromBackupSlot`（バックアップの枠からの保存は、元の枠があれば `GetNameForNewSlot` の新しい枠へ）→
    `PrepareToSave`（`saveDateTime = DateTime.Now.ToString(CurrentCulture)`・`day` など）→ 書き込み → 成功なら `callbackSuccessful`。
    枠の名前と日時はこの中で `SaveSlotData` の上で書き換わる
  - バックアップは書き込みのたびにファイル名を回す（`_backup_3` を消し、2→3・1→2・本体→1。`JsonFileSerializer` など）。中の
    `saveDateTime` はそのまま。`SaveSystem.GetRootSlotName("Steam_1_backup_2")` が `Steam_1`
  - 新しいゲームの枠の名前は、まだ使っていない最小の番号（`Steam_<n>`）。消した枠の名前はまた使われる。PC では枠の数の制限は無い
    （`IsLimitedSaveSlotsEnabled` が false）
  - 読み込みのたびに別の `GameSave` ができる（メモリのキャッシュから読んでもバイト列から作り直す）→ 参照が替わったらロードか新しいゲーム
  - セーブにはずっと変わらない id が無い（`SaveSlotData` は名前・日数・日時・品質など）。時点を見分けるのは枠の名前＋保存の日時
- `MainGame.OnGameStarted` / `OnGoToMainMenu`（static の `Action`）
