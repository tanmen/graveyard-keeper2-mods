using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using CraftTodo.Core;

namespace CraftTodo
{
    /// <summary>
    /// 製作メモを、ゲームのセーブの時点ごとに残す（0.6.1〜）。本人の言葉（2026-11-21）:「セーブデータから開始された時に、
    /// その時に保存された内容から復元されるのが好ましい」。セーブせずに終えた日に足したメモ（まだ建っていない制作機のメモなど）を、
    /// 次に始めたときに出さないため。
    ///
    /// - ゲームが保存に成功したとき（SaveSystem.Save。ふつうは寝て起きたとき）だけ、その時点のメモを
    ///   BepInEx/config/CraftTodo/saves/&lt;枠&gt;/&lt;保存した日時&gt;.txt に書く。遊んでいる間の足し引きはメモリの中だけ
    /// - セーブを読み込んだら（GameSave が替わったら）、その枠の、同じ保存の日時（SaveSlotData.saveDateTime）のメモに戻す。
    ///   無ければ空。ゲームのバックアップ（Steam_1_backup_2 など）は元の枠の保存の日時を持ったままなので、その時点のメモが戻る
    /// - 新しいゲーム（保存の日時が空）は空で始める。消したセーブの枠の名前がまた使われても、日時が違うので古いメモは出ない
    /// - 0.6.0 までの形式（todo-&lt;枠&gt;.txt。変わるたびに書いていた）は、その枠にまだ時点ごとのメモが無いときだけ読む
    /// BepInEx/plugins に書かないのは、ゲームが mod の更新・無効化のたびにそこを差し替える・消すため（README_MODDING.txt の 2）。
    /// </summary>
    internal sealed class TodoStorage
    {
        // 1 つの枠に残す時点の数。ゲームは本体＋バックアップ 3 つを残すので、それより多めに持つ
        private const int KeepPerSlot = 8;

        private const string SaveLinePrefix = "# save\t";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string directory;
        private readonly TodoList list;
        private readonly ManualLogSource log;

        // 遊んでいるセーブ（ロード・新しいゲームのたびに別のもの）。替わったらメモを読み替える
        private object session;

        public TodoStorage(string directory, TodoList list, ManualLogSource log)
        {
            this.directory = directory;
            this.list = list;
            this.log = log;
        }

        /// <summary>毎フレーム呼ぶ。遊んでいるセーブが替わったら、そのセーブを保存した時点のメモに戻す。</summary>
        public void Tick()
        {
            if (!GameBridge.TryGetSession(out GameSave save, out SaveSlotData slot) || ReferenceEquals(save, session))
            {
                return;
            }

            session = save;
            Restore(slot);
        }

        /// <summary>ゲームが保存に成功した（SaveSystem.Save の成功の呼び返し）。いま遊んでいるセーブなら、その時点のメモを書く。</summary>
        public void OnGameSaved(SaveSlotData slot, GameSave save)
        {
            if (slot == null || save == null || !GameBridge.IsCurrentSave(save))
            {
                // 枠の写し（取り込み）など、遊んでいるセーブ以外の保存
                return;
            }

            if (!ReferenceEquals(save, session))
            {
                log.LogWarning($"セーブ枠 {slot.slotName} の保存に、製作メモの読み替えが追いついていない。この時点のメモは残さない");
                return;
            }

            string root = RootOf(slot.slotName);
            string stamp = slot.saveDateTime;
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(stamp))
            {
                log.LogWarning($"保存した枠の名前か日時が空なので、製作メモを残せない（枠 {slot.slotName}・日時 {stamp}）");
                return;
            }

            try
            {
                string folder = SlotFolder(root);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, FileNameFor(stamp));
                string temp = path + ".tmp";
                string text = TodoFormat.Write(list.Entries) + SaveLinePrefix + stamp + "\n";
                File.WriteAllText(temp, text, Utf8NoBom);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                int pruned = Prune(folder);
                RetireLegacy(root);
                log.LogInfo($"ゲームの保存に合わせて製作メモを残した: セーブ枠 {slot.slotName}（{stamp}）・{list.Count} 件" + (pruned > 0 ? $"（古い時点 {pruned} 件を消した）" : string.Empty));
            }
            catch (Exception e)
            {
                log.LogError($"製作メモを保存できなかった（セーブ枠 {slot.slotName}）: {e.Message}");
            }
        }

        private void Restore(SaveSlotData slot)
        {
            string name = slot.slotName;
            string stamp = slot.saveDateTime;
            try
            {
                if (string.IsNullOrEmpty(stamp))
                {
                    list.ReplaceAll(null);
                    log.LogInfo($"新しいゲーム（セーブ枠 {name}）: 製作メモは空で始める");
                    return;
                }

                string root = RootOf(name);
                string folder = SlotFolder(root);
                string path = Path.Combine(folder, FileNameFor(stamp));
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    if (SavedStamp(text) == stamp)
                    {
                        list.ReplaceAll(TodoFormat.Read(text));
                        log.LogInfo($"セーブ枠 {name}（{stamp} に保存）の時点の製作メモに戻した: {list.Count} 件");
                        return;
                    }

                    log.LogWarning($"製作メモのファイル {path} の保存の日時が、セーブと合わない（{SavedStamp(text)} / {stamp}）。読まない");
                }

                string legacy = LegacyPath(name);
                if (!HasSnapshots(folder) && File.Exists(legacy))
                {
                    list.ReplaceAll(TodoFormat.Read(File.ReadAllText(legacy, Encoding.UTF8)));
                    log.LogInfo($"セーブ枠 {name} の製作メモを 0.6.0 までの形式（{Path.GetFileName(legacy)}）から読んだ: {list.Count} 件（次にゲームが保存したときから、保存の時点ごとに残す）");
                    return;
                }

                list.ReplaceAll(null);
                log.LogInfo($"セーブ枠 {name}（{stamp} に保存）の時点の製作メモは残っていないので、空で始める");
            }
            catch (Exception e)
            {
                log.LogError($"製作メモを読めなかった（空で始める）: {e.Message}");
                list.ReplaceAll(null);
            }
        }

        /// <summary>同じ枠の時点のうち、新しい <see cref="KeepPerSlot"/> 件だけ残す。消した数を返す。</summary>
        private static int Prune(string folder)
        {
            List<FileInfo> files = new DirectoryInfo(folder).GetFiles("*.txt").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            int removed = 0;
            for (int i = KeepPerSlot; i < files.Count; i++)
            {
                files[i].Delete();
                removed++;
            }

            return removed;
        }

        /// <summary>0.6.0 までの形式のファイルは、その枠の時点ごとのメモができたら .old に替える（もう読まない）。</summary>
        private void RetireLegacy(string root)
        {
            string legacy = LegacyPath(root);
            if (!File.Exists(legacy))
            {
                return;
            }

            string old = legacy + ".old";
            if (File.Exists(old))
            {
                File.Delete(old);
            }

            File.Move(legacy, old);
            log.LogInfo($"0.6.0 までの形式の製作メモ（{Path.GetFileName(legacy)}）を {Path.GetFileName(old)} に替えた（もう読まない）");
        }

        private static bool HasSnapshots(string folder) => Directory.Exists(folder) && Directory.GetFiles(folder, "*.txt").Length > 0;

        /// <summary>ファイルの中の保存の日時（ファイル名は日時を書き換えて作るので、読むときは中の行と突き合わせる）。</summary>
        private static string SavedStamp(string text)
        {
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith(SaveLinePrefix, StringComparison.Ordinal))
                {
                    return line.Substring(SaveLinePrefix.Length);
                }
            }

            return null;
        }

        /// <summary>ゲームのバックアップの枠（Steam_1_backup_2）は元の枠（Steam_1）にまとめる（ゲームの GetRootSlotName）。</summary>
        private static string RootOf(string slotName) => string.IsNullOrEmpty(slotName) ? slotName : SaveSystem.GetRootSlotName(slotName);

        private string SlotFolder(string root) => Path.Combine(directory, "saves", Safe(root, '-'));

        // 0.6.0 までと同じく、使えない字は _ に替える
        private string LegacyPath(string slotName) => Path.Combine(directory, "todo-" + Safe(slotName, '_') + ".txt");

        /// <summary>保存の日時（ゲームの言語の書式。日本語なら 2026/11/20 15:14:00）をファイル名にする。</summary>
        private static string FileNameFor(string stamp) => Safe(stamp.Replace(' ', '_'), '-') + ".txt";

        private static string Safe(string name, char replacement)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? replacement : c);
            }

            return sb.ToString();
        }
    }
}
