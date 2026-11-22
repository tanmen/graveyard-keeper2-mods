using System;
using System.IO;
using System.Text;
using BepInEx.Logging;
using QuickStash.Core;

namespace QuickStash
{
    /// <summary>
    /// ロックした品を、セーブ枠ごとに BepInEx/config/QuickStash/locks-&lt;枠&gt;.txt に残す（1 行に 1 つの id）。
    /// ロックは遊び方の好みなので、ゲームの保存の時点とは結び付けない: ロック・解除したその場で書き、セーブを読み込んだら
    /// その枠のファイルを読む。ゲームのバックアップの枠（Steam_1_backup_2）は元の枠（Steam_1）と同じファイルを使う。
    /// BepInEx/plugins に書かないのは、ゲームが mod の更新・無効化のたびにそこを差し替える・消すため（README_MODDING.txt の 2）。
    /// </summary>
    internal sealed class LockStorage
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string directory;
        private readonly ManualLogSource log;

        // 遊んでいるセーブ（ロード・新しいゲームのたびに別のもの）。替わったら読み直す
        private object session;
        private string slotRoot;

        public LockStorage(string directory, ManualLogSource log)
        {
            this.directory = directory;
            this.log = log;
        }

        /// <summary>いま遊んでいる枠のロック。メニューにいる間は空。</summary>
        public LockSet Current { get; } = new LockSet();

        /// <summary>いま遊んでいる枠の名前（メニューにいる間は null）。</summary>
        public string SlotRoot => slotRoot;

        /// <summary>毎フレーム呼ぶ。遊んでいるセーブが替わったら、その枠のロックを読む。</summary>
        public void Tick()
        {
            if (!GameBridge.TryGetSession(out GameSave save, out string root))
            {
                if (session != null)
                {
                    session = null;
                    slotRoot = null;
                    Current.Clear();
                }

                return;
            }

            if (ReferenceEquals(save, session))
            {
                return;
            }

            session = save;
            slotRoot = root;
            Load(root);
        }

        /// <summary>品のロックを切り替えて、その場でファイルに書く。ロックした状態になったら true。</summary>
        public bool Toggle(string itemId)
        {
            bool locked = Current.Toggle(itemId);
            Save();
            return locked;
        }

        private void Load(string root)
        {
            string path = PathOf(root);
            try
            {
                LockSet loaded = File.Exists(path) ? LockSet.Parse(File.ReadAllText(path, Utf8NoBom)) : new LockSet();
                Current.ReplaceWith(loaded);
                log.LogInfo($"セーブ枠 {root} のロックを読んだ: {Current.Count} 種" + (Current.Count > 0 ? "（" + string.Join(", ", Current.Ids) + "）" : string.Empty));
            }
            catch (Exception e)
            {
                Current.Clear();
                log.LogError($"ロックを読めなかった（{path}）。ロック無しで続ける: {e.Message}");
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(slotRoot))
            {
                log.LogWarning("遊んでいるセーブ枠が分からないので、ロックをファイルに残せない（このロックはゲームを終えると消える）");
                return;
            }

            string path = PathOf(slotRoot);
            try
            {
                Directory.CreateDirectory(directory);
                string temp = path + ".tmp";
                File.WriteAllText(temp, Current.Serialize($"Quick Stash のロック（セーブ枠 {slotRoot}）。1 行に 1 つのアイテムの id"), Utf8NoBom);
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception e)
            {
                log.LogError($"ロックを書けなかった（{path}）: {e.Message}");
            }
        }

        private string PathOf(string root) => Path.Combine(directory, "locks-" + Safe(root) + ".txt");

        private static string Safe(string name)
        {
            var text = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                text.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            }

            return text.ToString();
        }
    }
}
