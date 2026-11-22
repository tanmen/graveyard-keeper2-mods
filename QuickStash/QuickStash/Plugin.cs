using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace QuickStash
{
    /// <summary>
    /// しまうキー（既定 G）を押すと、いまいる場所（右上の地名の場所）の宝箱に、すでに入っている品と同じ品を所持品からしまう。
    /// 所持品のセルの上でホイールを押し込むと、その品をロックする（もう一度押すと外す）。ロックした品は、しまうキーを押しても残る。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, BuildInfo.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tanmen.gk2.quickstash";
        public const string PluginName = "Quick Stash";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<KeyboardShortcut> StashKey { get; private set; }

        internal ConfigEntry<KeyboardShortcut> LockKey { get; private set; }

        internal ConfigEntry<bool> KeepHotBarItems { get; private set; }

        internal ConfigEntry<bool> Notify { get; private set; }

        internal ConfigEntry<bool> Animation { get; private set; }

        internal LockStorage Locks { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            Locks = new LockStorage(Path.Combine(Paths.ConfigPath, "QuickStash"), Log);
            ApplyPatches();
            Log.LogInfo($"{PluginName} {BuildInfo.Version} を読み込んだ（しまうキー {StashKey.Value}・ロックのキー {LockKey.Value}）");
        }

        private void BindConfig()
        {
            StashKey = Config.Bind("Keys", "StashKey", new KeyboardShortcut(KeyCode.G),
                "いまいる場所の宝箱に、すでに入っている品と同じ品を所持品からしまう（移動キーを押したままでも効く）");
            LockKey = Config.Bind("Keys", "LockKey", new KeyboardShortcut(KeyCode.Mouse2),
                "所持品のセルの上で押すと、その品をロックする（もう一度押すと外す）。ロックした品は、しまうキーを押しても残る");
            KeepHotBarItems = Config.Bind("Stash", "KeepHotBarItems", true,
                "ホットバー（1〜4）に置いた品は、ロックしていなくても残す");
            Notify = Config.Bind("Stash", "Notify", true,
                "しまったときに、右下の知らせに何をいくつしまったかを出す");
            Animation = Config.Bind("Stash", "Animation", true,
                "しまったときに、品のアイコンを自分の体から宝箱へ飛ばす");
        }

        private void Update()
        {
            try
            {
                Locks.Tick();

                if (KeyInput.WasPressed(LockKey.Value))
                {
                    LockPicker.TryToggleUnderPointer(Locks);
                }

                if (KeyInput.WasPressed(StashKey.Value) && InputGuard.CanStash())
                {
                    Stasher.Run(Locks.Current, KeepHotBarItems.Value, Notify.Value, Animation.Value);
                }
            }
            catch (Exception e)
            {
                Hooks.LogError("毎フレームの処理", e);
            }
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
