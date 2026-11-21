using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CraftTodo.Core;
using HarmonyLib;
using UnityEngine;

namespace CraftTodo
{
    /// <summary>
    /// レシピを画面の左に「製作メモ」として残す。
    /// レシピの上で登録キー（既定はホイールの押し込み）を押すたびに回数を足し、減らすキー（既定は左 Shift＋ホイール）で減らす。
    /// 作った（製作を始めた・設置した）回数だけ減り、0 になったら消える。
    /// 設定 AddOnBlockedClick を入れると、素材の足りない行・灰色の「作る」を押したときにも足す。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, BuildInfo.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tanmen.gk2.crafttodo";
        public const string PluginName = "Craft Todo";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> Visible { get; private set; }

        internal ConfigEntry<KeyboardShortcut> ToggleKey { get; private set; }

        internal ConfigEntry<KeyboardShortcut> ClearKey { get; private set; }

        internal ConfigEntry<int> MaxEntries { get; private set; }

        internal ConfigEntry<bool> GameFrame { get; private set; }

        internal ConfigEntry<KeyboardShortcut> RegisterKey { get; private set; }

        internal ConfigEntry<KeyboardShortcut> RemoveKey { get; private set; }

        internal ConfigEntry<bool> AddOnBlockedClick { get; private set; }

        internal ConfigEntry<bool> AddWhenCraftWindowOpens { get; private set; }

        internal ConfigEntry<bool> AutoPosition { get; private set; }

        internal ConfigEntry<float> OffsetX { get; private set; }

        internal ConfigEntry<float> OffsetY { get; private set; }

        internal TodoList Todos { get; } = new TodoList();

        private TodoStorage storage;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();

            Todos.MaxEntries = MaxEntries.Value;
            MaxEntries.SettingChanged += (_, __) => Todos.MaxEntries = MaxEntries.Value;
            storage = new TodoStorage(Path.Combine(Paths.ConfigPath, "CraftTodo"), Todos, Log);

            ApplyPatches();
            gameObject.AddComponent<TodoOverlay>();
            Log.LogInfo($"{PluginName} {BuildInfo.Version} を読み込んだ");
        }

        private void Update()
        {
            storage.Tick();
        }

        /// <summary>ゲームが保存に成功した（SaveSystem.Save。ふつうは寝て起きたとき）。その時点のメモを残す。</summary>
        internal void OnGameSaved(SaveSlotData slot, GameSave save) => storage.OnGameSaved(slot, save);

        /// <summary>
        /// 作れなかったときに自動で足す（設定 AddOnBlockedClick / AddWhenCraftWindowOpens）。
        /// 何度呼ばれても回数は増え続けず、少なくともこの回数を残す。
        /// </summary>
        internal void AddTodo(TodoEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            bool existed = Todos.Contains(entry.Key);
            TodoEntry result = Todos.EnsureCount(entry);
            Log.LogInfo($"製作メモに{(existed ? "更新" : "追加")}: {entry.Key} ×{result.Multiplier}（素材 {entry.Materials.Count} 種）");
        }

        /// <summary>登録キーで押されたレシピ。その回数分を足す。隠していても、足したら表示に戻す。</summary>
        internal void RegisterTodo(TodoEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Visible.Value = true;
            TodoEntry result = Todos.AddCount(entry);
            Log.LogInfo($"登録キーで製作メモに +{entry.Multiplier}: {entry.Key} → ×{result.Multiplier}（素材 {entry.Materials.Count} 種）");
        }

        /// <summary>減らすキーで押されたレシピ。その回数分を減らす。メモに無ければ何もせず false。</summary>
        internal bool UnregisterTodo(TodoEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            int remaining = Todos.SubtractCount(entry.Key, entry.Multiplier);
            if (remaining < 0)
            {
                return false;
            }

            Log.LogInfo($"減らすキーで製作メモを -{entry.Multiplier}: {entry.Key} → {(remaining == 0 ? "消した" : "×" + remaining)}");
            return true;
        }

        /// <summary>作った（製作を始めた・建物を設置した）回数だけ減らす。</summary>
        internal void CompleteTodo(string key, int count)
        {
            int remaining = Todos.SubtractCount(key, count);
            if (remaining >= 0)
            {
                Log.LogInfo($"{count} 回作ったので製作メモを減らした: {key} → {(remaining == 0 ? "消した" : "×" + remaining)}");
            }
        }

        internal void RemoveTodo(string key)
        {
            if (Todos.Remove(key))
            {
                Log.LogInfo($"製作メモから手で外した: {key}");
            }
        }

        private void BindConfig()
        {
            Visible = Config.Bind("Display", "Visible", true,
                "製作メモを表示する（ToggleKey でも切り替わる）");
            ToggleKey = Config.Bind("Display", "ToggleKey", new KeyboardShortcut(KeyCode.F8),
                "製作メモの表示・非表示を切り替えるキー");
            ClearKey = Config.Bind("Display", "ClearKey", new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "製作メモを全部消すキー");
            MaxEntries = Config.Bind("Display", "MaxEntries", 8,
                new ConfigDescription("残すメモの数の上限（超えたら古いものから消える）", new AcceptableValueRange<int>(1, 30)));
            GameFrame = Config.Bind("Display", "GameFrame", true,
                "背景をゲームの UI の枠の絵にする。false なら単色の半透明の板（0.2.0 までの見た目）");
            AutoPosition = Config.Bind("Position", "AutoPosition", true,
                "左上の HUD（顔とエネルギーの枠）のすぐ下に置く。false なら画面の上から 17% の高さに置く");
            OffsetX = Config.Bind("Position", "OffsetX", 4f,
                "置き場所からの横のずらし（UI の単位。右が正）");
            OffsetY = Config.Bind("Position", "OffsetY", 4f,
                "置き場所からの縦のずらし（UI の単位。下が正）");
            RegisterKey = Config.Bind("Adding", "RegisterKey", new KeyboardShortcut(KeyCode.Mouse2),
                "マウスの下のレシピを製作メモに足すキー。押すたびに回数が増える（既定はホイールの押し込み。建物の行・作業台のレシピ・キュー・回数を選ぶ窓で効く）");
            RemoveKey = Config.Bind("Adding", "RemoveKey", new KeyboardShortcut(KeyCode.Mouse2, KeyCode.LeftShift),
                "マウスの下のレシピの回数を製作メモから減らすキー（既定は左 Shift＋ホイールの押し込み。0 になったら消える）");
            AddOnBlockedClick = Config.Bind("Adding", "AddOnBlockedClick", false,
                "素材の足りない建物の行・灰色の「作る」（ゲームパッドの作るキーも）を押したときにも足す");
            AddWhenCraftWindowOpens = Config.Bind("Adding", "AddWhenCraftWindowOpens", false,
                "作業台でレシピを開いた時点で、作れなければメモに足す");
        }

        private static void ApplyPatches()
        {
            var harmony = new Harmony(PluginGuid);
            int ok = 0;
            int failed = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (!type.GetCustomAttributes(typeof(HarmonyPatch), false).Any())
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    Log.LogError($"パッチを当てられなかった（ゲームの更新で形が変わった可能性）: {type.Name}: {e.Message}");
                }
            }

            Log.LogInfo($"パッチ: 成功 {ok} / 失敗 {failed}（差し替えたメソッド {harmony.GetPatchedMethods().Count()} 個）");
        }
    }
}
