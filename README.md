# graveyard-keeper2-mods

Graveyard Keeper 2 の自作 mod を置くリポジトリ。

| mod | 中身 |
| --- | --- |
| [Mods Menu](ModsMenu/README.md) | ESC の窓に「Mods」を足し、mod ごとの木のタブで設定を変える窓を開く。下の 3 つはここにタブを登録する（Mods Menu は任意の依存で、無くても動く）。ほかの人の mod もタブを足せる |
| [Craft Todo](CraftTodo/README.md) | レシピの上でホイールを押し込むと、画面の左に「製作メモ」として残す。素材ごとに「いま使える数 / 要る数」を出し、そろったら緑になる。作ったら消える |
| [Stock Finder](StockFinder/README.md) | アイテムのツールチップの一番下に、どこの宝箱に何個あるか（宝箱ごとの表）と、所持品と宝箱を合わせた合計を足す |
| [Quick Stash](QuickStash/README.md) | `G` を押すと、いまいる場所の宝箱に、すでに入っている品と同じ品を所持品からまとめてしまう（品のアイコンが体から宝箱へ飛ぶ）。所持品のセルの上でホイールを押し込むとロックでき、ロックした品は残る |

Mod の作り方（ゲームが公式に受け付ける形・置き場所・ビルド・ログ・Workshop への上げ方）は [docs/modding.md](docs/modding.md)。

## ビルド

```bash
dotnet test ModsMenu/ModsMenu.Core.Tests
dotnet test CraftTodo/CraftTodo.Core.Tests
dotnet test StockFinder/StockFinder.Core.Tests
dotnet test QuickStash/QuickStash.Core.Tests
```

ゲームに依存しない部分（`ModsMenu.Core`・`CraftTodo.Core`・`StockFinder.Core`・`QuickStash.Core`）はどこでもビルドとテストができる。mod 本体のビルドにはゲームの導入先
（`GraveyardKeeper2_Data/Managed` と同梱の `BepInEx/core`）が要る（手順は各 mod の README）。
3 つの mod は Mods Menu を参照してビルドする（`ProjectReference`・`Private=false`）ので、どれをビルドしても Mods Menu も一緒にビルドされる
（配置は別。mod を `-p:DeployToGame=true` で配置しても Mods Menu は置かれないので、Mods Menu は `ModsMenu/ModsMenu` を直に配置する）。
