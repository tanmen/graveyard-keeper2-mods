# ゲーム側の調べ（Trade Finder。GK2 v1.012.9、2026-11-23）

`Assembly-CSharp.dll` と `LazyBearTechnology.dll` を手元で逆コンパイル（`ilspycmd`。結果はリポジトリに入れない）して読んだ要点と、
`resources.assets` の `GameBalance`・訳文を生のバイト列から読んだ結果。ツールチップへの足し方は
[Stock Finder の調べ](../../StockFinder/docs/game-api-notes.md)「ツールチップに足す所」と同じ（同じ 3 か所に差し込む）。

## 商人（`Vendor` / `VendorDef` / `VendorSystem`）

- 定義は `GameBalance.Me.vendorDefs`（`VendorDef`）: `townVendor`・`startTier`・`startMoney`・private `icon`（`WGODef` の id。
  `VendorDef.Icon` が `WGODef.Portrait` を返す）・`lockedByDefaultInOrdersWindow`・`tierDataList`（`VendorTierData`）
- 段階 `VendorTierData`: `vendorProducts`（`VendorProductData`: `itemId`・`priceMod`・`baseCount`）・`notBuying`・`notSelling`（どちらも
  `List<string>`）・`townVendorProductInfos`（売ると幸福が入る品。`itemId`・`itemCount`・`happinessCount`・`perOne`）・`newProducts`・`orders`
- セーブの商人は `MainGame.Instance.GameSave.vendorSystem`（`VendorSystem.GetVendor(id)`）。`Vendor.CurTier`（1 から）・`CurrentTierData`・
  `Inventory`（商人の在庫）・`CurMoney`。新しいゲームで定義の全部が作られる（`GameSave` の初期化と `VendorSystem.PrepareForGame`）
- **買える**（窓の右の在庫で押せる品）: `Vendor.CanSellItemToPlayer` = いまの段階の `vendorProducts` にあり `notSelling` に無い。
  窓は加えて、種（`IsSeed`）は在庫が 4 未満なら押せない（`Trading.VendorItemsAvailableCondition`）。いまの段階の品でない在庫は窓に出さない
- **売れる**（窓の左の所持品で押せる品）: `Vendor.CanBuyItemFromPlayer` = いまの段階の `vendorProducts` にあり `notBuying` に無い
- 窓は、次と次の次の段階で**新しく**扱う品（`newProducts` のうち在庫に無いもの）を「ステージ II」「ステージ III」の見出しの下に押せないセルで出す
  （`Trading.TryFormFakeInventoryForTier`）
- 値段（`Trading`）: 買う 1 個 = `Round(CurPrice(id, buy: true, 商人の在庫))`、売る 1 個 = `CurPrice(id, buy: false, 商人の在庫＋1)` を
  元値 `ItemDef.basePrice` で頭打ちにして丸める（窓の所持品のセルは `countModificator` 1、商人のセルは 0）。
  `CurPrice` = `basePrice＋全体の補正＋priceMod` × √(`baseCount` / 在庫) × (売るなら 0.75)。`isStaticCost` の品は元値のまま
- お金の書き方は `Trading.FormatMoney(値, printZero, 区切り)`（public static）: 1 金 = 100 銀 = 10000 銅。それぞれ文字の中のアイコン（`<sprite name=…>`）
- 幸福のアイコンは `"happiness".FontIcon()`（`<sprite name="happiness">`。ゲームの訳文 `tt_trade_hap_bar` もツールチップの中で使っている）
- 段階が上がるのは、町の店の建物を上の段階に建て替えたとき（`TownSystem.UpdateTownBuildingVendor` → `ForceLevelUpVendor`）と、
  スクリプトの `ForceLevelUpVendor` / `AddMoneyToVendor`
- 窓の見出しは `LLBase.L(VendorDef.id)`、段階の名前は訳文 `ui_vendor_tier_<N>`（日本語「ステージ I」〜「ステージ III」）

## 商人の一覧（`resources.assets` の `GameBalance` を生のバイト列で読んだ。2026-11-23）

型の木が省かれているので、`VendorDef` の並び（string `id`・bool `townVendor`（4 にそろえる）・int `startTier`・int `startMoney`・string `icon`・
bool `lockedByDefaultInOrdersWindow`・`List<VendorTierData>`）を候補の位置ごとに当てて、`npc_herm` を含む 21 人の並びが 1 か所だけ見つかった。
`VendorTierData` は int 2 つ・`LazyExpression`（string 2 つ・int・float・bool）・`List<string>` 2 つ・`List<VendorProductData>`・
`List<TownVendorProductInfo>`・`List<string>` 2 つ。

| id | 名前（ja） | 段階ごとの品の数 | 備考 |
| --- | --- | --- | --- |
| `npc_head_of_the_village` | ヤヌアリウス | 7 / 7 / 8 | |
| `npc_herm` | ヘルム | 6 / 21 / 28 | |
| `npc_warehouse_manager` | 商人頭 | 19 / 0 / 0 | 段階 2 で品が無くなる |
| `npc_astrologer` | ガンター | 6 / 6 / 6 | |
| `npc_workshop_foreman` | ジャック | 0 / 0 / 0 | 注文だけ（Trade Finder は出さない） |
| `npc_bishop` | 司祭 | 2 / 0 / 0 | |
| `npc_woodcarver` | ハロルド | 2 / 0 / 0 | |
| `t_b_apothecary` ほか 12 軒 | 町の店 | | `lockedByDefaultInOrdersWindow`。建てるまで閉じている |
| `test2` / `test_town_vendor` | （訳文なし） | | テスト用（出さない） |

- 扱う品は 279 種。1 つの品を売る商人は 1 人が 196 種・2 人 38・3 人 12・6 人 1（`bronze_scrap`）。買い取る商人もほぼ同じ分布
- 町の店（`t_b_*`）の id に訳文は無い。名前は町の建物の定義（`TownBuildingDef`）の id の訳文: `Pharmacy_t1` 薬屋・`Alchemy_t1` 錬金術師・
  `Vegetable_t1` 八百屋・`Tailor_t1` 仕立て屋・`Furniture_t1` 家具職人・`Bakery_t1` パン屋・`Fish_t1` 魚屋・`Jeweler_t1` 宝石商・
  `Builder_t1` 建築士・`Blacksmith_t1` 鍛冶場・`Shalman_t1` 酒場・`Brewery_t1` 醸造所（`_t2` / `_t3` は「薬屋 II」など）
- `TownBuildingDef`: `vendorId`（→ 商人）・`lvlUpId`（次の段階の建物）・`characterId`（店番の物の id。商人の `icon` と同じ `t_b_apothecary` など）

## 建てたか・居る場所

- 町の店を建てると `TownSystem.CreateTownBuildingOnWgo` が建物の物と店番の物（`characterId`）を世界に足し、
  `UpdateTownBuildingVendor` が `KnowledgeSystem.UnlockVendorForOrders(vendorId)` を呼ぶ（注文の窓に出す印）。
  `lockedByDefaultInOrdersWindow` でない商人は新しいゲームの時点でこの印が付く → **町の店（`TownBuildingDef.vendorId` に出てくる商人）は、
  この印が無ければ未建設**（`KnowledgeSystem.IsVendorForOrdersUnlocked`。ほかにこの印を付ける所は無い）
- 居る場所: `MainGame.WorldData.GetWgoDataList(商人の icon)` の物の居るゾーン（`id` → 訳文 `wz_<id>`。右上の地名の枠と同じ）。
  ゾーンの名前の例: `wz_herms_store` ヘルムのお店・`wz_warehouse` 町の倉庫・`wz_astrologer_tower` 占星術師の塔・`wz_workshop_conveyor` ジャックの工房・
  `wz_town` 町・`wz_church` 教会
- ⚠ **`WgoData.WorldZoneData` は入れ物型のゾーンにしか付かない**: ゲームが物をゾーンに結び付けるのは `WorldZoneData.TryAddWgoData` だけで、
  `IsContainer`（`worldZoneType == Default`）かつ `IsActive` のゾーンに限る。入れ物型でないゾーン（`SimpleNotContainer`）や町（`TownZone`。
  ゾーンのデータではなく場面の当たり判定）に居る物は空のまま。0.1.0 はこれだけを見ていて、18 人中 2 人（ヤヌアリウス → 村の酒場・ガンター →
  占星術師の塔）しか引けなかった
- 右上の地名の枠のプレイヤーのゾーンは、当たり判定（レイヤー 17）を真上から下へ撃って決める（`PlayerPhysicalBody.UpdateWorldZone`）:
  `displayType` が `Hidden` のゾーンは飛ばす → 入れ物型（中心の近い方）→ 入れ物型でないゾーン → 町（`TownZone`。訳文 `town_zone`）。
  0.1.1 はこれをデータで写す: `GetGameSceneDataById(WorldId).worldZones` のうち `wholeZoneRect` が物の位置（x, z）を含むもの →
  町の店なら `LinkedToTownBuildingUniqueId` の建物で同じこと → `TownZone.Collider.bounds.Contains(位置)` なら町（`wz_town`）
- 2026-11-22 14:21 のセーブ（`Steam_1.dat`。型名と項目名つきの UTF-16 BE の直列化。読むだけ）では: ヤヌアリウス・ガンター・ヘルム・商人頭・
  薬屋の店番（`t_b_apothecary`）の物は場面 `RuinedTemple` にあり、ヘルム・商人頭・薬屋の店番は `worldZoneDataId` が空。
  **司祭（`npc_bishop`）とハロルド（`npc_woodcarver`）の物は世界に無い**（商人 `vendorSystem` と注文の窓の印 `unlockedVendorsForOrders` にだけ出てくる）。
  どの条件で世界に出てくるかは調べていない（場面の中身は `StreamingAssets/aa` の Addressables で、`resources.assets` には GameBalance と訳文にしか出てこない）

## ツールチップの出し直し（キーを押している間だけ出す項目のため）

- `ShowAtTargetAndCalculateOffsets` → `ShowAtTarget` は、ツールチップが出ていない **か相手（target）が違うときだけ** `gui_hover_light` を鳴らす。
  → 同じ相手に同じ並びで呼び直すと、音を鳴らさずに部品を作り直し（`widgetContainer.ConstructWidgets`）、置き場所も測り直す
- `ShowAtPosition` は並びの末尾の区切りを取り除く（並びそのものを書き換える）
- ツールチップが消えると `gameObject` が非表示になる（`HideTooltip`。`Hide()` は DOTween の 0 秒の後に呼ぶ）
