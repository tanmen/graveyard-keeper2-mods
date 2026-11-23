using UnityEngine;

namespace TradeFinder
{
    /// <summary>
    /// キー（既定 Shift）を押している間だけ出す項目のために、毎フレームキーを見て、出ているツールチップを出し直す。
    /// どの項目もキーを使わない設定なら何もしない。
    /// </summary>
    internal sealed class KeyWatcher : MonoBehaviour
    {
        /// <summary>設定のキーを、左右どちらかで押しているか。</summary>
        public static bool Held()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null)
            {
                return false;
            }

            switch (plugin.Key.Value)
            {
                case HoldKey.Ctrl:
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case HoldKey.Alt:
                    return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
                default:
                    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
        }

        private void Update()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null || !plugin.Options(false).UsesKey)
            {
                return;
            }

            bool held = Held();
            TooltipHook.Guard("キーでの出し直し", () => TooltipHook.ReshowIfKeyChanged(held));
        }
    }
}
