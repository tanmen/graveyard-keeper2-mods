using System;

namespace QuickStash
{
    /// <summary>差し込みの本体を包む。1 つの処理が落ちてもゲームを止めず、同じ所のエラーは数回だけログに出す。</summary>
    internal static class Hooks
    {
        private const int MaxErrorLogs = 5;

        private static int errorLogs;

        public static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                LogError(what, e);
            }
        }

        public static void LogError(string what, Exception e)
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
