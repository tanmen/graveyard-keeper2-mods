namespace ModsMenu.Core
{
    /// <summary>
    /// 2 度押しのボタン（Craft Todo の「メモを全部消す」など）。1 度目で構え、<see cref="Seconds"/> 秒のうちにもう一度押すと実行する。
    /// 時刻は呼ぶ側が渡す（ゲームでは Time.unscaledTime）。
    /// </summary>
    internal sealed class ConfirmLatch
    {
        public ConfirmLatch(float seconds)
        {
            Seconds = seconds > 0f ? seconds : 3f;
        }

        public float Seconds { get; }

        public bool Armed { get; private set; }

        private float armedUntil;

        /// <summary>押した。実行するときだけ true（そのとき構えは解ける）。</summary>
        public bool Press(float now)
        {
            if (Armed && now < armedUntil)
            {
                Armed = false;
                return true;
            }

            Armed = true;
            armedUntil = now + Seconds;
            return false;
        }

        /// <summary>時間切れなら構えを解く。解いたときだけ true（見た目を戻す合図）。</summary>
        public bool Tick(float now)
        {
            if (Armed && now >= armedUntil)
            {
                Armed = false;
                return true;
            }

            return false;
        }

        public void Reset() => Armed = false;
    }
}
