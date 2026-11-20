using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Logging;
using CraftTodo.Core;

namespace CraftTodo
{
    /// <summary>
    /// 製作メモをセーブ枠ごとのファイル（BepInEx/config/CraftTodo/todo-&lt;枠の名前&gt;.txt）に置く。
    /// 一覧が変わるたびに書く（ゲームの終了を待たない。落ちても直前までのメモが残る）。
    /// BepInEx/plugins に書かないのは、ゲームが mod の更新・無効化のたびにそこを差し替える・消すため（README_MODDING.txt の 2）。
    /// </summary>
    internal sealed class TodoStorage
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string directory;
        private readonly TodoList list;
        private readonly ManualLogSource log;
        private string slot;
        private bool loading;

        public TodoStorage(string directory, TodoList list, ManualLogSource log)
        {
            this.directory = directory;
            this.list = list;
            this.log = log;
            list.Changed += OnChanged;
        }

        /// <summary>毎フレーム呼ぶ。遊んでいるセーブ枠が変わったら、その枠のメモに読み替える。</summary>
        public void Tick(string currentSlot)
        {
            if (currentSlot == null || currentSlot == slot)
            {
                return;
            }

            slot = currentSlot;
            Load();
        }

        private void Load()
        {
            loading = true;
            try
            {
                string path = PathFor(slot);
                List<TodoEntry> entries = File.Exists(path) ? TodoFormat.Read(File.ReadAllText(path, Encoding.UTF8)) : new List<TodoEntry>();
                list.ReplaceAll(entries);
                log.LogInfo($"セーブ枠 {slot} の製作メモを読んだ: {list.Count} 件");
            }
            catch (Exception e)
            {
                log.LogError($"製作メモを読めなかった（空で始める）: {e.Message}");
                list.ReplaceAll(null);
            }
            finally
            {
                loading = false;
            }
        }

        private void OnChanged()
        {
            if (loading || slot == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(directory);
                string path = PathFor(slot);
                string temp = path + ".tmp";
                File.WriteAllText(temp, TodoFormat.Write(list.Entries), Utf8NoBom);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
            }
            catch (Exception e)
            {
                log.LogError($"製作メモを保存できなかった: {e.Message}");
            }
        }

        private string PathFor(string slotName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(slotName.Length);
            foreach (char c in slotName)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return Path.Combine(directory, "todo-" + sb + ".txt");
        }
    }
}
