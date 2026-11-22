using BepInEx.Configuration;
using UnityEngine;

namespace QuickStash
{
    /// <summary>
    /// 設定のキーが押されたか。BepInEx の KeyboardShortcut.IsDown() は、ショートカットに無いキーが 1 つでも押されていると false を返す
    /// （BepInEx 5.4.23.5 の ModifierKeyTest。Craft Todo の調べ）ので、移動キー（WASD）を押したまま歩きながら押すと効かない。
    /// ここでは「主のキーがこのフレームで押され、修飾キーが全部押されている」だけを見る。
    /// </summary>
    internal static class KeyInput
    {
        public static bool WasPressed(KeyboardShortcut shortcut)
        {
            KeyCode main = shortcut.MainKey;
            if (main == KeyCode.None || !Input.GetKeyDown(main))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
