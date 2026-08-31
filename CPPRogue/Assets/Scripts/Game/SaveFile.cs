using System;
using System.IO;
using CPPRogue.Core.Codebase;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// 存档 IO（Game 层）：persistentDataPath/save.json。
    /// Core 的 SaveGame 负责序列化，这里只管文件；损坏/缺失返回 null（调用方用新档）。
    /// 保存时机：撤离入库、构建页任何改动（合成/装备/兑换）——都改完立即落盘。
    /// </summary>
    public static class SaveFile
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static PlayerProfile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return SaveGame.Parse(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"存档读取失败，使用新档：{ex.Message}");
            }
            return null;
        }

        public static void Save(PlayerProfile profile)
        {
            try
            {
                File.WriteAllText(FilePath, SaveGame.ToJson(profile));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"存档写入失败：{ex.Message}");
            }
        }
    }
}
