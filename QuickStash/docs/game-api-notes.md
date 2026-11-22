# ゲーム側の調べ（Quick Stash。GK2 v1.012.9、2026-11-22）

`Assembly-CSharp.dll` と `LazyBearTechnology.dll` を手元で逆コンパイル（`ilspycmd`。結果はリポジトリに入れない）して読んだ要点。
宝箱の選び方・名前は [Stock Finder の調べ](../../StockFinder/docs/game-api-notes.md)、UI の絵・セル・知らせは [Craft Todo の調べ](../../CraftTodo/docs/game-api-notes.md)。

## キーボードの割り当て

- ゲームの入力は `LazyInput`（LazyBearTechnology）。キーボードは `KeyboardController` が `GameBindings.keyBindings`
  （`GameKey` → `KeyCode` ＋ `additionalKeyCodes`）を旧 `Input.GetKey` で見る。Rewired はマウスのホイールの軸（18）とゲームパッドだけ
- 割り当ての保存は `GameSettings.keyboardKeybindings` / `defaultKeyboardKeybindings`（`gameKeyValue` と `keyCode` の組）を JSON にして
  PlayerPrefs の `settings`（Windows はレジストリ `HKCU\Software\Lazy Bear Games\Graveyard Keeper 2` の `settings_h<ハッシュ>`、UTF-8 の REG_BINARY）
- 2026-11-22 の手元の値（既定と同じ・38 件）:

| キー | GameKey |
| --- | --- |
| W / A / S / D | Up / Left / Down / Right（回数の窓では Max / Decrease / Min / Increase） |
| E | Interaction・SpeechSkip2・回数の窓の Apply |
| Q | QuestTree・回数の窓の Cancel |
| F | Action |
| R | Rotate・ChangeWeapon |
| T | TechTree |
| I | Inventory |
| M | Map |
| P | Inspirations |
| Tab | CharacterWindow |
| Space | Select・SpeechSkip・Attack |
| Esc | Back・InGameMenu |
| 1〜4 | UseHotBarItem1〜4 |
| 左 Shift | AttackFocus |
| 矢印 | 回数の窓のスライダー |
| マウスの左 / 右 | LeftClick・Build / RightClick |

- 直書きのキー: `Tab`（植える状態を抜ける）・`E` / `Esc`（休む状態を抜ける）・`Shift+F5`（不具合の報告）・`Shift+F10` / `Shift+F11`（mod の README・Workshop）。
  ホイールの押し込み（`GetMouseButton(2)` / `InputButton.Middle` / `KeyCode.Mouse2`）はゲームのどこにも無い
- → Q・WASD の近くで空いているのは `Z X C V G`。Quick Stash の既定は 0.1.0 が `Z`、0.2.0 から `G`（本人が試して選んだ）

## 品が飛ぶ演出（0.2.0）

- ゲーム自身の飛ぶ演出 `FlyingTechPoint`（技術点が世界から HUD の数へ飛ぶ）は UI の部品: 出だしを `CameraSystem.WorldToScreenPoint(world)`
  （public static。描画の解像度を上げ下げしているときの直しも入っている）で画面の位置にして `transform.position` に置き、`DOMove(to, 1f + delay)`
  （`Ease.OutCubic`）で飛ばす。DOTween（`DOTween.dll`）は同梱されている
- → Quick Stash も UI の部品で飛ばす。ただし行き先も世界の物（宝箱）なので、体と宝箱の画面の位置を毎フレーム直して線の上を進める（DOTween は使わない）。
  画面の位置から UI の位置へは `RectTransformUtility.ScreenPointToLocalPointInRectangle`（根の Canvas が Overlay なら UI のカメラは null）
- 体: `PlayerController.MovablePosition`（足元。`playerData.position`）と `PlayerController.BubblePoint`（吹き出しの位置の Transform）の間
- 宝箱: `GameScene.GetWgoViewGlobal(wgoData.UniqueId)`（public static。読み込まれている物の見た目 `Wgo`）の Renderer の bounds の真ん中。
  見た目が無ければ `WgoData.Position`。⚠ `Wgo` の基底は Sirenix の `SerializedMonoBehaviour` なので、`Wgo` の部品の口（`isActiveAndEnabled` など）を使うと
  `Sirenix.Serialization.dll` の参照が要る（無いと CS0012）。粒子の `ParticleSystemRenderer` は `UnityEngine.ParticleSystemModule.dll` にある
- 在庫から物へ: `new MultiInventory(zone)` は在庫しか返さないので、`zone.wgoDataList`（`SGuid`）を `GameSave.worldData.GetWgoData` で引いて
  `Inventory.Data` → `WgoData` の表を作る（`MultiInventory` と同じ選び方）
- アイコン: `EasySpritesCollection.GetSprite(ItemDef.iconId)`。青い縁は所持品のセルの `icon.material` と `colors.NormalColor` を写して
  `ImageExtensions.BlueColorReplace` で置き換える（Craft Todo の調べの「UI の絵」）
- 並び順: HUD 50・窓 400〜・ツールチップ 700（`GUIElements` と `LazyWindowsStackController`）。演出は 60（HUD より手前・窓より奥）

## 所持品から宝箱へ「同じ品」を移す（ゲームのボタン）

- 宝箱の窓（`UIBaseChestWindow` / `UIBaseChestWindowData`）の所持品の見出しにある「同じ品を全部移す」ボタン
  （`InventoryHeaderWidget.moveAllSimilarItemsToBagBtn`）は、`宝箱.TakeAllItemsExistingInMeFromOtherInventory(所持品)` を呼ぶ。
  宝箱がプレイヤーの品を受けない（`Inventory.BlocksPlayerItemInsert` = `TakeOnlySerializedItemProperty`）ならボタンを出さない
- `Item.TakeAllItemsExistingInMeFromOtherInventory(other, ignoreMyBags = true, ignoreOtherBags = true)`:
  宝箱の一番上の段にある id の集合を作り、所持品を後ろから見て、**`Definition.stackCount > 1` の品だけ**、id が集合にあれば
  `AddItemToInventory(new Item(id, count), out added, null, ignoreMyBags)`（入るだけ入れる。属性は持ち越さない）→ 入った数だけ
  `other.RemoveItemFromInventory(id, n, IsItemTheSameByItemId, …, ignoreOtherBags)`。所持品の袋の中は見ない
- 足す: `Item.AddItemToInventory(source, out List<Item> added, ignoredBag, ignoreAllBags)` は public。入る数（`CanAddItemCountToInventory`）
  だけ入れて true（0 なら false）。既存の山を埋めてから空きの枠に新しい山を作る。`AutoExpandSerializedItemProperty` なら枠を増やす
- 引く: `Inventory.RemoveItemById(id, count, ignoredBag, sourceBag, ignoreAllBags)` は public で、`OnItemsRemove` を鳴らす。
  `ignoreAllBags: true` なら一番上の段だけ
- 窓の描き直し: `InventoryWidgetBase` は在庫の `OnItemsAdd` / `OnItemsRemove` で `Redraw` する → `Inventory.NotifyItemsAdded(list)`（public）を呼べば
  開いている窓も描き直る。所持品から減っても右下の知らせは出ない（`OnItemsRemove` を聞いているのは窓の部品だけ）
- 宝箱の窓が「入れられる」と見る決まり（`ItemAvailabilityConditionLeft`）: TakeOnly でない・黒の一覧
  （`BlackListFilterSerializedItemProperty.BlackList.Contains(ItemDef)`）に無い・白の一覧（`WhiteListFilterSerializedItemProperty`）が空か、それに有る
- 移すときの音は `item_put`（`InventoryUIItemMoveOpHandler`）

## その場所の宝箱

- `PlayerData.CurrentWorldZoneData`（いまいる場所。無ければ null）と `new MultiInventory(zone)`（`includePlayerInventory` 既定 false）。
  中身は `zone.wgoDataList` のうち `Definition.inventorySize != 0` かつ `OpenInMultiInventory` の物の `WgoData.Inventory`。
  `Sort()` は燃料の入れ物を後ろへ回すだけ。別の物の在庫を指す物（`hasRefToOtherWgoInventory`）は同じ在庫が 2 度出うるので `Inventory.Data` で重複を除く
- ホットバー: `PlayerData.pinnedItems`（`string[4]`、品の id）。使うときは `inventory.GetItemById(id)` で所持品から引く →
  しまうとホットバーが空になるので、既定で残す

## 効く場面

- 歩いているときの入力は `FreePlayerState.Update` → `PlayerInputHandler.UpdateInput`。操作を奪う理由は `PlayerController` の
  `IsControlsEnabled`（`TakenControlType`: ByWork / ByUI / ByLadder / ByFlow / ByTeleport / BySleep / ByAttack / ByDeath / ByFishing /
  ByBuilding / BySelf / ByCinematics / BySceneLoading / ByRest）。`IsControlsEnabledExcept(params TakenControlType[])` で一部を除いて見られる
- 開いている窓: `LazyWindowsStackController.ActiveWindow`（いちばん上。窓を開くと積み、閉じると下の窓、全部閉じると null）。
  所持品の窓は `CharacterWindow`（Tab / I）、宝箱の窓は `UIBaseChestWindow`（`UIChestWindow` と物資保管庫などの `UIConveyor*ChestWindow`）
- → 窓が無ければ `IsControlsEnabled`、所持品の窓・宝箱の窓なら `IsControlsEnabledExcept(ByUI)` のときだけ受ける。ほかの窓では受けない

## 所持品のセルとロックの印

- 所持品の窓の部品は `InventoryWidget`（`Data.Inventory`。人物の窓は `gameSave.playerData.inventory`、宝箱の窓は渡された所持品をそのまま包む）。
  袋は `BagInventoryWidget : InventoryWidget`（在庫は袋）。`Redraw()` は public override で、セルを `UIPrefabsPooler` から取り、
  品のあるセルを `Draw(item, …)`、空きを `DrawEmptyWithState`。セルを返す前に必ず `UIItemCell.Flush()`
- セル `UIItemCell`（MainScene の bundle の部品）: `Root`（セルいっぱい、42x42）の下に `Background`（44x44）・`ItemIcon`（48x48 中央）・
  `CountLabel`（右下）・`PriceLabel`（左上 40x40。商人の窓だけ）・`StarIcon`（48x48 の絵 `item_star_N` の右上 14x13 に星）・
  `StatusIcon`（左下 11x15）・`NonInteractableImg`・`Selection`。品は `DisplayingItem`
- 南京錠の絵 `icon_lock` は 2 つある: アトラス `IconsCompressed` の 11x13（ぴったり）と `Icons` の 24x24（南京錠は左上から 6,5 の所に 11x13）。
  `EasySpritesCollection.GetSprite(name)` は名前→アトラスの表で 1 つを返すので、`EnsureSpriteAtlasWithNameLoaded("IconsCompressed").GetSprite("icon_lock")` で 11x13 を引く。
  ほかの候補: `ui_reply_lock_red` / `_grn`（18x18、赤・緑の縁）・`comm-item_cell-dark-lock`（42x42 のセル全体）
- 絵を手元で見る: `UnityPy` で bundle の Sprite を名前で探す（アトラスに詰められた Sprite は `SpriteAtlas.m_RenderDataMap` からアトラスの絵を切り出す）。
  bundle の読み方の ⚠ は Craft Todo の調べの「UI の絵」
