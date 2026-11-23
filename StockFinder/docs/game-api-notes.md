# ゲーム側の調べ（Stock Finder。GK2 v1.012.9、2026-11-22）

`Assembly-CSharp.dll` と `LazyBearTechnology.dll` を手元で逆コンパイル（`ilspycmd`。結果はリポジトリに入れない）して読んだ要点。
ツールチップの見た目・TextStyle・UI の倍率などの共通の話は [Craft Todo の調べ](../../CraftTodo/docs/game-api-notes.md)（「ゲームのツールチップ」「UI の絵」）。

## 宝箱と在庫

- **宝箱 = `WGODef.inventorySize != 0` かつ `WGODef.OpenInMultiInventory`**（設定の `open_in_multi_inventory`）。ゲームの
  `MultiInventory(WorldZoneData)`（作業台が素材を探す範囲・所持品の窓でまとめて開く範囲）と同じ選び方
- 世界の物の全部: `MainGame.WorldData.gameSceneDataList`（`GameSceneData`）の `wgoDataList`（`List<WgoData>`）。
  ゾーンの収納の一覧 `WorldZoneData.MultiInventoryWgoDatas` はゾーンの中の物だけ（`TryAddWgoData` が `wholeZoneRect` で振り分ける）
- 物の場所: `WgoData.WorldZoneData`（無ければ null）。名前は右上の地名の枠と同じ `LLBase.L("wz_" + zone.id)`（`WorldZoneWidget.Redraw`。
  町の中は `town_zone`＋小区画の名前を出しているが、ゾーンの id の訳文もある）
- 宝箱の名前: 所持品の窓の見出しは `Inventory.ViewId` の訳文。`WgoData` の在庫は `Inventory.Create(..., customViewId: id)` で作られるので、
  `LLBase.L(wgoData.id)`（定義の id。シンプルな宝箱 `chest_rough_place`・大きな宝箱 `chest_good_place`・道具棚 `tool_rack`・
  物資保管庫 `builder_conveyor_storage`・庭の保管スペース `garden_bags_storage_1〜3` など）
- ⚠ **`WgoData.Inventory` は、定義の `hasRefToOtherWgoInventory` なら別の物（`refToOtherWgoInventory`）の在庫を返す** → 同じ在庫を
  2 度数えないよう、`Inventory` の参照で重複を除く
- 数: `Inventory.Data`（`Item`。在庫そのものが入れ物の品）の `GetTotalCountInInventory(itemId)`。中の袋（`IsBag`）の中身も数える。
  品は `item.id == itemId` だけで見る（品質の星の違いは別の id）
- 所持品: `MainGame.PlayerData.Inventory`。訳文 `inventory` が「所持品」（所持品の窓の見出し）
- 訳文 `tt_study_faith` に「所持品と保管箱にある合計数。」があり、ゲームも「保管箱」と呼んでいる（Stock Finder の文言は「宝箱」の名前をそのまま出す）

## ツールチップに足す所

- 中身の部品の並び（`List<LazyWidgetDataBase>`）を出し方ごとに組み立て、最後に private の
  `ShowAtTargetAndCalculateOffsets(list, target, ForceCornerPosition, follow, appearOffset, TooltipPlacementPriority)` に渡す。
  public の `Show` も含め、**ツールチップを出す入口は全部ここを通る**（`ShowAtTarget` → `ShowAtPosition` → `widgetContainer.ConstructWidgets(list)`。
  部品はその場で作られる）
- 品の説明は private static `AddItemWidgets(list, ItemDef, addHeader, headerPrefix, notShowStudyWidget, nowShowCraftedAt, qualityVariants)`。
  呼ぶのは `ShowItemCell`（所持品・宝箱など `UIItemCell`）・`ShowCraftOutputCell`・`ShowLinkedEntity`（技術ツリー。この後に要る拡張・素材の並びが続く）・
  `ShowMultiAnswerIcon`・`ShowOrderWidget`
- 品の効果は private static `AddItemEffectWidgets(list, ItemDef, qualityVariants)`。`AddItemWidgets` の中と、`ShowCraftInfo`（制作の窓のレシピ・キュー。
  出来上がる品の効果）から呼ばれる。Craft Todo の素材のツールチップ（作り方の出る B）もこれを呼ぶ
- → Stock Finder は 2 つの Postfix で「この並びはこの品」と覚え（並びの参照で照らす。最初の品だけ）、`ShowAtTargetAndCalculateOffsets` の
  Prefix で同じ並びなら末尾に足す。技術ツリーでも素材の並びのあと、ツールチップの一番下に来る
- 文字の部品 `UITooltipTextWidget`: `label.alignment = data.TextAlignmentOptions` のあとに `TextStyleComponent.ApplyStyle()` を呼ぶので、
  揃え方は初めて当てたときの値（ゲームは全部 Center）に戻る（Craft Todo の調べの「TextStyle.ApplyStyle は揃え方を戻す」）。
  → 左揃え・列は文字の中の書式 `<align=left>` と `<pos=…>` で組む
- **ツールチップの並べ方**（MainScene の bundle。前のセッションの控え）: 根の `UITooltip` は縦並び（余白 14・間 2・中央寄せ・
  子の幅を制御・子の幅は広げない）＋ ContentSizeFitter（推奨の大きさ）。**部品はそれぞれ自分の推奨幅で、中央に置かれる**。
  文字の部品は TMP の大きさ 16（`TextStyle.ApplyStyle` は書体・材質・色・行間だけを替え、大きさは替えない）・余白 0・折り返し Normal
- ⚠ **窓には幅の上限がある**（2026-11-23 に bundle で読んだ。前の控えの「幅の上限は無い」は誤り）: 根の `UITooltip` と同じ物に
  `OverrideLayoutElement`（`LayoutElement` の子。`useMaxWidth` 1・`maxWidth` 200・最小の幅 60）が付いていて、窓の推奨幅を 200 で抑える。
  縦並びの余白 14 ずつを引いた**中身 172 を超える文字の部品は、172 に縮められて折り返す**（ゲームの長い説明が折り返すのもこれ）。
  前の控えの道具は `LayoutElement` の欄で見分けていて、子の型の `maxWidth` を出していなかった。
  Stock Finder の 1 行の表は、場所（執筆用の地下室）と宝箱（シンプルな宝箱）の名前が両方長いと 200 を超えて崩れた（水のツールチップ）
  → 0.2.1 で超えるときだけ場所を見出しの行にまとめ、0.2.2 からは本人の選択でいつもその形（中身の幅は実行中に窓の
  `OverrideLayoutElement` と `LayoutGroup.padding` から引き、それでも超えたら警告だけ出す）
- TMP の推奨幅（`CalculatePreferredValues`）は**空白でない字の右端までしか数えない**（空白・`<pos>` だけの行末は幅にならない）。
  見出しの行の幅を数の右端にそろえるには、色の書式の**内側**に `<alpha=#00>0` を置く（`<alpha>` は `m_htmlColor.a` だけを替え、
  頂点の色の不透明度は部品の色との小さい方になるので見えない。外側に置くと内側の 6 桁の色で戻る）
- `TextStyle.ApplyStyleToString(s)` は `<font="書体" material="材質"><color=#RRGGBB>s</color></font>`。色は 6 桁（不透明）なので、
  外側に `<alpha=#00>` を付けても中の色の書式で見えてしまう（透明な行で幅を取る手は使えない）
- → 表は、ゲームと同じ書体・大きさの TMP（自前。Canvas の外に置く）で `GetPreferredValues` を使って各列の幅を測り、
  宝箱の名前と数の位置を `<pos=…>`（単位なし = 文字の部品の座標の単位）で決める。数は「数の列の位置＋数の幅の最大−その数の幅」に置いて右揃え。
  初めて出したときに、ツールチップの実物の文字の部品で同じ文字を測り直してログに比べを出す
- 書体に無い字: 訳文に「×」「・」が 1 度も出てこない（ゲームの書体に字形が無い。Craft Todo の 0.7.0 で「・」が描かれなかった）。半角の `:` `,` は
  ゲームのコードが「作成場所: 」などに使っている
