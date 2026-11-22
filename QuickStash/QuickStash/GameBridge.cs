using System;
using System.Collections.Generic;
using LazyBearTechnology;
using UnityEngine;

namespace QuickStash
{
    /// <summary>
    /// ゲームの型との境目（セーブ・所持品・場所・名前・音）。ゲームの更新で形が変わったら、直すのはここと Stasher・LockMarks・Patches。
    /// 調べた根拠は QuickStash/docs/game-api-notes.md。
    /// </summary>
    internal static class GameBridge
    {
        /// <summary>
        /// 遊んでいるセーブ（GameSave。ロード・新しいゲームのたびに別のもの）と、その枠の元の名前（Steam_1。バックアップの枠も元の枠にまとめる）。
        /// メニューやロード中は false。
        /// </summary>
        public static bool TryGetSession(out GameSave save, out string slotRoot)
        {
            save = null;
            slotRoot = null;
            try
            {
                MainGame game = MainGame.Instance;
                if (game == null || MainGame.PlayerData == null || game.GameSave == null || game.SaveSlotData == null)
                {
                    return false;
                }

                string name = game.SaveSlotData.slotName;
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }

                save = game.GameSave;
                slotRoot = SaveSystem.GetRootSlotName(name);
                return !string.IsNullOrEmpty(slotRoot);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static PlayerData Player
        {
            get
            {
                try
                {
                    return MainGame.Instance == null ? null : MainGame.PlayerData;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static PlayerController Controller
        {
            get
            {
                try
                {
                    return MainGame.Instance == null ? null : MainGame.PlayerController;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>その在庫が所持品か（窓の部品は同じ在庫を別の Inventory で包むことがあるので、中身の Item で照らす）。</summary>
        public static bool IsPlayerInventory(Inventory inventory)
        {
            Item own = Player?.Inventory?.Data;
            return inventory?.Data != null && own != null && ReferenceEquals(inventory.Data, own);
        }

        /// <summary>ホットバー（1〜4）に置いた品の id。</summary>
        public static HashSet<string> PinnedItems(PlayerData player)
        {
            var pinned = new HashSet<string>(StringComparer.Ordinal);
            if (player?.pinnedItems == null)
            {
                return pinned;
            }

            foreach (string id in player.pinnedItems)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    pinned.Add(id);
                }
            }

            return pinned;
        }

        public static bool Japanese
        {
            get
            {
                try
                {
                    return LLBase.CurrentLang == "ja";
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>場所（ゾーン）の名前。右上の地名の枠と同じ訳文 wz_&lt;id&gt;。</summary>
        public static string ZoneName(WorldZoneData zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.id))
            {
                return null;
            }

            return L("wz_" + zone.id, zone.id);
        }

        /// <summary>品の名前（ログ用。ゲームの ItemDef.GetHeader）。</summary>
        public static string ItemName(Item item)
        {
            try
            {
                return item?.Definition?.GetHeader() ?? item?.id;
            }
            catch (Exception)
            {
                return item?.id;
            }
        }

        /// <summary>ゲームの UI の根（倍率と表示・非表示がゲームの UI と揃う）。Craft Todo・ゲームの Workshop Creator の窓と同じ取り方。</summary>
        public static Transform FindUiRoot()
        {
            try
            {
                GUIElements gui = GUIElements.Instance;
                if (gui == null)
                {
                    return null;
                }

                Component fitter = HarmonyLib.Traverse.Create(gui).Field("uiFitter").GetValue<Component>();
                return fitter != null ? fitter.transform : gui.Root;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void PlaySound(string id)
        {
            try
            {
                LazyAudio.PlayAndForget(id);
            }
            catch (Exception)
            {
                // 音が鳴らなくても続ける
            }
        }

        private static string L(string id, string fallback)
        {
            try
            {
                return LLBase.HasL(id) ? LLBase.L(id) : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
