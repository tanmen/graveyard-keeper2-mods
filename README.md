# graveyard-keeper2-mods

Graveyard Keeper 2 の自作 mod を置くリポジトリ。

| mod | 中身 |
| --- | --- |
| [Craft Todo](CraftTodo/README.md) | レシピの上でホイールを押し込むと、画面の左に「製作メモ」として残す。素材ごとに「いま使える数 / 要る数」を出し、そろったら緑になる。作ったら消える |

Mod の作り方（ゲームが公式に受け付ける形・置き場所・ビルド・ログ・Workshop への上げ方）は [docs/modding.md](docs/modding.md)。

## ビルド

```bash
dotnet test CraftTodo/CraftTodo.Core.Tests
```

ゲームに依存しない部分（`CraftTodo.Core`）はどこでもビルドとテストができる。mod 本体のビルドにはゲームの導入先
（`GraveyardKeeper2_Data/Managed` と同梱の `BepInEx/core`）が要る（手順は各 mod の README）。
