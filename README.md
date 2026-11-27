# graveyard-keeper2-mods

BepInEx 5 mods for **Graveyard Keeper 2** (Steam). Players install them from the Steam Workshop: subscribe and play, because the Steam
version of the game already includes BepInEx. Each mod's text follows the game language (Japanese or English).

| Mod | What it does | Steam Workshop |
| --- | --- | --- |
| [Mods Menu](ModsMenu/README.md) | Adds "Mods" to the pause menu: one tab per mod to change its settings in game. Other mods can add a tab ([For mod authors](ModsMenu/README.md#for-mod-authors)) | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576548) |
| [Craft Todo](CraftTodo/README.md) | Press the mouse wheel on a recipe to keep it in a to-do list on the left of the screen, with "available / needed" for every ingredient | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576662) |
| [Stock Finder](StockFinder/README.md) | Item tooltips show which chests hold the item and how many, plus the total | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576854) |
| [Quick Stash](QuickStash/README.md) | Press `G` to put items into the chests of your current place that already hold them. Lock items with the mouse wheel | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576760) |
| [Trade Finder](TradeFinder/README.md) | Item tooltips show which vendors sell and buy the item, with place and price | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576936) |

The READMEs and notes below are in Japanese. Licensed under the [MIT License](LICENSE).

---

Graveyard Keeper 2 の自作 mod を置くリポジトリ。

| mod | 中身 | Steam Workshop |
| --- | --- | --- |
| [Mods Menu](ModsMenu/README.md) | ESC の窓に「Mods」を足し、mod ごとの木のタブで設定を変える窓を開く。下の 4 つはここにタブを登録する（Mods Menu は任意の依存で、無くても動く）。ほかの人の mod もタブを足せる | [サブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576548) |
| [Craft Todo](CraftTodo/README.md) | レシピの上でホイールを押し込むと、画面の左に「製作メモ」として残す。素材ごとに「いま使える数 / 要る数」を出し、そろったら緑になる。作ったら消える | [サブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576662) |
| [Stock Finder](StockFinder/README.md) | アイテムのツールチップの一番下に、どこの宝箱に何個あるか（宝箱ごとの表）と、所持品と宝箱を合わせた合計を足す | [サブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576854) |
| [Quick Stash](QuickStash/README.md) | `G` を押すと、いまいる場所の宝箱に、すでに入っている品と同じ品を所持品からまとめてしまう（品のアイコンが体から宝箱へ飛ぶ）。所持品のセルの上でホイールを押し込むとロックでき、ロックした品は残る | [サブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576760) |
| [Trade Finder](TradeFinder/README.md) | アイテムのツールチップの一番下に、その品をどの商人から買えるか・どの商人に売れるか（居る場所と値段つき）を足す。未建設の店・先のステージは薄い字。項目ごとに「いつも / Shift の間 / 出さない」を選べる | [サブスクライブ](https://steamcommunity.com/sharedfiles/filedetails/?id=3846576936) |

Mod の作り方（ゲームが公式に受け付ける形・置き場所・ビルド・ログ・Workshop への上げ方）は [docs/modding.md](docs/modding.md)。

## ビルド

```bash
dotnet test ModsMenu/ModsMenu.Core.Tests
dotnet test CraftTodo/CraftTodo.Core.Tests
dotnet test StockFinder/StockFinder.Core.Tests
dotnet test QuickStash/QuickStash.Core.Tests
dotnet test TradeFinder/TradeFinder.Core.Tests
```

ゲームに依存しない部分（`ModsMenu.Core`・`CraftTodo.Core`・`StockFinder.Core`・`QuickStash.Core`・`TradeFinder.Core`）はどこでもビルドとテストができる。mod 本体のビルドにはゲームの導入先
（`GraveyardKeeper2_Data/Managed` と同梱の `BepInEx/core`）が要る。根の `Game.local.props.example` を写して `Game.local.props` を作り（gitignore 済み）、
`GameDir` をゲームの導入先にする（5 つの mod がどれもこの 1 つを読む。`-p:GameDir=...` でも渡せる）。
4 つの mod は Mods Menu を参照してビルドする（`ProjectReference`・`Private=false`）ので、どれをビルドしても Mods Menu も一緒にビルドされる
（配置は別。mod を `-p:DeployToGame=true` で配置しても Mods Menu は置かれないので、Mods Menu は `ModsMenu/ModsMenu` を直に配置する）。

Workshop に上げるフォルダは `scripts/stage-workshop.ps1` で組む（手順は [docs/modding.md](docs/modding.md) の 5）。

## ライセンス

[MIT](LICENSE)。
