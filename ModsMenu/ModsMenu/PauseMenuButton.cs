using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace ModsMenu
{
    /// <summary>
    /// ESC の窓（ポーズ）の「Mods」のボタン。「操作方法」のボタンを写し、そのすぐ下に置く
    /// （Craft Todo 0.7.0 の「制作メニュー」のボタンと同じ所。2026-11-23 に本人の求めで名前を「Mods」にし、Mods Menu へ移した）。
    /// ゲームの UIGamePauseWindow.CreateBugReportButton と同じ写し方。登録されたタブが 1 つも無ければ出さない。
    /// </summary>
    internal static class PauseMenuButton
    {
        private const string ButtonName = "ModsMenuButton";

        public static void Ensure(UIGamePauseWindow window, LazyButton controls)
        {
            if (window == null || controls == null)
            {
                return;
            }

            Transform parent = controls.transform.parent;
            Transform existing = parent.Find(ButtonName);
            if (existing != null)
            {
                // 言語を変えたあとも文言を合わせる。タブが無くなったら隠す
                SetLabel(existing.gameObject);
                existing.gameObject.SetActive(Registry.Count > 0);
                return;
            }

            if (Registry.Count == 0)
            {
                return;
            }

            GameObject go = Object.Instantiate(controls.gameObject, parent, false);
            go.name = ButtonName;
            go.SetActive(true);
            go.transform.SetSiblingIndex(controls.transform.GetSiblingIndex() + 1);
            foreach (LocalizedLabel label in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                label.IgnoreLocalize = true;
            }

            SetLabel(go);
            LazyButton button = go.GetComponent<LazyButton>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Hooks.Guard("ESC の窓の「Mods」", () =>
            {
                window.Close();
                MenuWindow.OpenWindow(() => window.Open(null));
            }));
            button.SetCallbacksIntoGamepadNavigationItem();
            Plugin.Log.LogInfo($"ESC の窓に「Mods」のボタンを足した（タブ {Registry.Count}）");
        }

        private static void SetLabel(GameObject go)
        {
            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = Strings.PauseButton;
            }
        }
    }

    /// <summary>
    /// ESC の窓を開く前に「Mods」のボタンを足す（1 度だけ）。Init ではなく Open に差すのは、窓の Init が mod より先に済んでいても足せるように。
    /// 押すと Mods の窓を開き、閉じると ESC の窓に戻る（ゲームの「設定」と同じ流れ）。
    /// </summary>
    [HarmonyPatch(typeof(UIGamePauseWindow), nameof(UIGamePauseWindow.Open), new[] { typeof(LazyWidgetDataBase) })]
    internal static class PauseWindowOpenPatch
    {
        private static void Prefix(UIGamePauseWindow __instance, LazyButton ___controlsBtn)
        {
            Hooks.Guard("ESC の窓のボタン", () => PauseMenuButton.Ensure(__instance, ___controlsBtn));
        }
    }

    /// <summary>差し込みの本体を包む。1 つの処理が落ちてもゲームを止めず、同じ所のエラーは数回だけログに出す。</summary>
    internal static class Hooks
    {
        private const int MaxErrorLogs = 8;

        private static int errorLogs;

        public static void Guard(string what, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                if (errorLogs >= MaxErrorLogs)
                {
                    return;
                }

                errorLogs++;
                Plugin.Log.LogError($"{what}で失敗した: {e}" + (errorLogs == MaxErrorLogs ? "（以後は出さない）" : string.Empty));
            }
        }
    }
}
