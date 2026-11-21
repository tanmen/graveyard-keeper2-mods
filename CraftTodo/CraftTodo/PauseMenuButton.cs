using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace CraftTodo
{
    /// <summary>
    /// ESC の窓（ポーズ）の「制作メニュー」のボタン（0.7.0〜）。「操作方法」のボタンを写し、そのすぐ下に置く
    /// （2026-11-21 に本人が図で決めた。ゲームの UIGamePauseWindow.CreateBugReportButton と同じ写し方）。
    /// </summary>
    internal static class PauseMenuButton
    {
        private const string ButtonName = "CraftTodoButton";

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
                // 言語を変えたあとも文言を合わせる
                SetLabel(existing.gameObject);
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
            button.onClick.AddListener(() => Hooks.Guard("ESC の窓の「制作メニュー」", () =>
            {
                window.Close();
                SettingsWindow.OpenWindow(() => window.Open(null));
            }));
            button.SetCallbacksIntoGamepadNavigationItem();
            Plugin.Log.LogInfo("ESC の窓に「制作メニュー」のボタンを足した");
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
}
