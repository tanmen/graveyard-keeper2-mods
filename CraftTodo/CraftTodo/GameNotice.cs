using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CraftTodo
{
    /// <summary>
    /// 画面の右下に、ゲームの知らせ（UINotificator。アイテムを拾ったときや所持品がいっぱいのときと同じ部品）で 1 行の文を出す。
    /// 2026-11-21 に本人:「積み上げで数えるをメニュー名でお知らせするんじゃなく、右下に通知として出すのはだめそう？」（0.7.0 の F9）。
    ///
    /// ゲームの public の口は訳文の鍵しか受けない（ShowSimpleTextNotification(string locale) は LLBase.L(locale) を出す）ので、
    /// 文をそのまま受ける private の ShowSimpleTextNotification(string locale, string text) を反射で呼ぶ。
    /// ⚠ LazySingleton&lt;UINotificator&gt;.Instance は、見つからないと空の部品を新しく作ってしまうので使わない。シーンに置かれたものを探す。
    /// </summary>
    internal static class GameNotice
    {
        private static MethodInfo showText;
        private static bool resolved;
        private static UINotificator cached;
        private static bool loggedFirst;

        /// <summary>出せたら true。ゲームの部品が引けないとき（メニュー・ロード中・形が変わった）は false（呼んだ側で別の出し方にする）。</summary>
        public static bool TryShow(string key, string text)
        {
            try
            {
                UINotificator notificator = Find();
                if (notificator == null || !notificator.isActiveAndEnabled)
                {
                    return false;
                }

                if (!resolved)
                {
                    resolved = true;
                    showText = AccessTools.Method(typeof(UINotificator), "ShowSimpleTextNotification", new[] { typeof(string), typeof(string) });
                    if (showText == null)
                    {
                        Plugin.Log.LogWarning("右下の知らせ: UINotificator.ShowSimpleTextNotification(string, string) が見つからない（帯の文字で知らせる）");
                    }
                }

                if (showText == null)
                {
                    return false;
                }

                showText.Invoke(notificator, new object[] { key, text });
                if (!loggedFirst)
                {
                    loggedFirst = true;
                    Plugin.Log.LogInfo($"右下の知らせを初めて出した（{notificator.name}）");
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"右下の知らせを出せなかった（帯の文字で知らせる）: {e.GetBaseException().Message}");
                return false;
            }
        }

        private static UINotificator Find()
        {
            if (cached != null)
            {
                return cached;
            }

            foreach (UINotificator n in Resources.FindObjectsOfTypeAll<UINotificator>())
            {
                if (n != null && n.gameObject.scene.IsValid())
                {
                    cached = n;
                    break;
                }
            }

            return cached;
        }
    }
}
