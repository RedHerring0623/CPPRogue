using System;
using System.Collections.Generic;
using System.IO;
using CPPRogue.Core.Enemies;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Loot
{
    /// <summary>
    /// 图鉴表的单元测试：内置表完整性（覆盖全部 EnemyKind）、字段合法域，
    /// 以及 JSON 资产（Assets/Resources/EnemyTable.json）与内置表的逐字段同步——
    /// 两边谁改了没同步，这里立刻红。
    /// </summary>
    [TestFixture]
    public class EnemyCodexTests
    {
        [Test]
        public void 内置表_覆盖全部EnemyKind且ID可反查()
        {
            List<CodexEntry> entries = EnemyCodex.Default();
            var byId = new Dictionary<string, CodexEntry>();
            foreach (CodexEntry e in entries)
                byId[e.Id] = e;

            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                string id = EnemyCodex.IdForKind(kind);
                Assert.IsTrue(byId.ContainsKey(id), $"内置表缺少 {kind}（id={id}）");
                Assert.AreEqual(kind, EnemyCodex.KindForId(id));
            }
            Assert.Throws<KeyNotFoundException>(() => EnemyCodex.KindForId("no_such_id"));
        }

        [Test]
        public void 内置表_字段合法域()
        {
            List<CodexEntry> entries = EnemyCodex.Default();
            Assert.AreEqual("bug", entries[0].Id, "首条约定为 bug，图鉴排序稳定");
            foreach (CodexEntry e in entries)
            {
                // hp/atk 是直值（≥0 可超 5），spd 仍是等级 1-5
                Assert.GreaterOrEqual(e.HpLv, 0);
                Assert.GreaterOrEqual(e.AtkLv, 0);
                Assert.GreaterOrEqual(e.SpdLv, 1); Assert.LessOrEqual(e.SpdLv, 5);
                Assert.GreaterOrEqual(e.SpawnScore, 0);
                Assert.IsFalse(string.IsNullOrEmpty(e.NameZh), $"{e.Id} 缺中文名");
                Assert.IsFalse(string.IsNullOrEmpty(e.NameEn), $"{e.Id} 缺英文名");
                Assert.IsFalse(string.IsNullOrEmpty(e.Desc), $"{e.Id} 缺描述");
                Assert.That(Enum.IsDefined(typeof(MaterialKind), e.MainDrop));
                Assert.IsFalse(string.IsNullOrEmpty(EnemyCodex.TierRule(e.Tier)), $"{e.Id} 档位缺规则文案");
            }
        }

        [Test]
        public void 解析器_条目字段映射与校验()
        {
            const string json = @"{
  ""version"": 1,
  ""materials"": { ""ram"": ""RAM"" },
  ""enemies"": [
    { ""id"": ""bug"", ""nameZh"": ""Bug"", ""nameEn"": ""Bug"", ""tier"": ""normal"",
      ""stats"": { ""hp"": 2, ""atk"": 1, ""spd"": 2 }, ""spawnScore"": 1,
      ""mainDrop"": ""driver"", ""desc"": ""d"", ""note"": ""n"",
      ""summons"": [""zombie""] }
  ]
}";
            CodexBook book = EnemyCodexJson.Parse(json);
            Assert.AreEqual(1, book.Version);
            Assert.AreEqual("RAM", book.MaterialNames["ram"]);
            Assert.AreEqual(1, book.Entries.Count);

            CodexEntry e = book.Entries[0];
            Assert.AreEqual("bug", e.Id);
            Assert.AreEqual(CodexTier.Normal, e.Tier);
            Assert.AreEqual(2, e.HpLv);
            Assert.AreEqual(1, e.AtkLv);
            Assert.AreEqual(2, e.SpdLv);
            Assert.AreEqual(1, e.SpawnScore);
            Assert.AreEqual(MaterialKind.Driver, e.MainDrop);
            Assert.AreEqual("n", e.Note);
            Assert.AreEqual(new[] { "zombie" }, e.Summons);
        }

        [Test]
        public void 解析器_非法输入_抛异常()
        {
            Assert.Throws<FormatException>(() => EnemyCodexJson.Parse("[]"), "根不是对象");
            Assert.Throws<FormatException>(() => EnemyCodexJson.Parse("{}"), "缺 enemies");
            Assert.Throws<FormatException>(() => EnemyCodexJson.Parse(
                "{\"enemies\":[{\"id\":\"x\",\"tier\":\"boss\"}]}"), "未知档位");
            Assert.Throws<FormatException>(() => EnemyCodexJson.Parse(
                "{\"enemies\":[{\"id\":\"x\",\"tier\":\"normal\",\"stats\":{\"hp\":1,\"atk\":1,\"spd\":9},\"spawnScore\":1,\"mainDrop\":\"driver\",\"desc\":\"d\",\"nameZh\":\"x\",\"nameEn\":\"X\"}]}"),
                "属性等级越界");
        }

        [Test]
        public void JSON资产_与内置表逐字段同步()
        {
            string path = FindEnemyTable();
            if (path == null)
            {
                Assert.Ignore("找不到 Assets/Resources/EnemyTable.json（在仓库外运行时跳过）");
                return;
            }

            CodexBook book;
            try
            {
                book = EnemyCodexJson.Parse(File.ReadAllText(path));
            }
            catch (FormatException ex)
            {
                Assert.Fail($"EnemyTable.json 解析失败：{ex.Message}");
                return;
            }

            List<CodexEntry> expected = EnemyCodex.Default();
            Assert.AreEqual(expected.Count, book.Entries.Count,
                $"条目数不一致：JSON {book.Entries.Count} vs 内置 {expected.Count}");

            for (int i = 0; i < expected.Count; i++)
            {
                CodexEntry a = expected[i];
                CodexEntry b = book.Entries[i];
                Assert.AreEqual(a.Id, b.Id, $"[{i}] id");
                Assert.AreEqual(a.NameZh, b.NameZh, $"[{a.Id}] nameZh");
                Assert.AreEqual(a.NameEn, b.NameEn, $"[{a.Id}] nameEn");
                Assert.AreEqual(a.Tier, b.Tier, $"[{a.Id}] tier");
                Assert.AreEqual(a.HpLv, b.HpLv, $"[{a.Id}] hp");
                Assert.AreEqual(a.AtkLv, b.AtkLv, $"[{a.Id}] atk");
                Assert.AreEqual(a.SpdLv, b.SpdLv, $"[{a.Id}] spd");
                Assert.AreEqual(a.SpawnScore, b.SpawnScore, $"[{a.Id}] spawnScore");
                Assert.AreEqual(a.MainDrop, b.MainDrop, $"[{a.Id}] mainDrop");
                Assert.AreEqual(a.Desc, b.Desc, $"[{a.Id}] desc");
                Assert.AreEqual(a.Note ?? "", b.Note ?? "", $"[{a.Id}] note");
                Assert.AreEqual(a.Summons ?? new string[0], b.Summons ?? new string[0], $"[{a.Id}] summons");
            }

            // materials 段与代码的材料定义对齐
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
            {
                string id = Materials.Id(kind);
                Assert.IsTrue(book.MaterialNames.ContainsKey(id), $"materials 缺 {id}");
                Assert.AreEqual(Materials.Name(kind), book.MaterialNames[id], $"materials[{id}]");
            }
        }

        /// <summary>
        /// 从测试程序集位置向上找数据表：dotnet 在 StandaloneTests/bin 下，
        /// Unity 编辑器在 Library/ScriptAssemblies 下，向上最多走 8 层都能命中。
        /// </summary>
        private static string FindEnemyTable()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "CPPRogue", "Assets", "Resources", "EnemyTable.json");
                if (File.Exists(candidate))
                    return candidate;
                candidate = Path.Combine(dir, "Assets", "Resources", "EnemyTable.json");
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
