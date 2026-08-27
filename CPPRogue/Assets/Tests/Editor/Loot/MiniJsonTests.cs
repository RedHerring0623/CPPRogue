using System;
using System.Collections.Generic;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Loot
{
    /// <summary>MiniJson 解析器的单元测试：本项目数据表用到的全部语法 + 非法输入。</summary>
    [TestFixture]
    public class MiniJsonTests
    {
        [Test]
        public void Parse_对象数组字符串数字布尔null()
        {
            // 注意：逐字字符串里 "" = 一个引号；本夹具的 en 字段 JSON 原文是 "Null Pointer\""
            const string json = @"{
  ""name"": ""空指针"",
  ""en"": ""Null Pointer\"""",
  ""hp"": 2,
  ""ratio"": -2.5e1,
  ""ok"": true,
  ""none"": null,
  ""list"": [1, -2.5, ""三"", false, null],
  ""nested"": { ""a"": [ { ""b"": false } ] }
}";
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            Assert.AreEqual("空指针", root["name"]);
            Assert.AreEqual("Null Pointer\"", root["en"]);
            Assert.AreEqual(2d, root["hp"]);
            Assert.AreEqual(-25d, root["ratio"]);
            Assert.AreEqual(true, root["ok"]);
            Assert.IsNull(root["none"]);

            var list = (List<object>)root["list"];
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(1d, list[0]);
            Assert.AreEqual(-2.5d, list[1]);
            Assert.AreEqual("三", list[2]);
            Assert.AreEqual(false, list[3]);
            Assert.IsNull(list[4]);

            var nested = (Dictionary<string, object>)root["nested"];
            var inner = (List<object>)nested["a"];
            var deepest = (Dictionary<string, object>)inner[0];
            Assert.AreEqual(false, deepest["b"]);
        }

        [Test]
        public void Parse_空对象与空数组()
        {
            var obj = (Dictionary<string, object>)MiniJson.Parse("  { }  ");
            var arr = (List<object>)MiniJson.Parse("\t[\n]");
            Assert.AreEqual(0, obj.Count);
            Assert.AreEqual(0, arr.Count);
        }

        [Test]
        public void Parse_转义()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(@"{""s"":""\u4e2d\\n\t""}");
            Assert.AreEqual("中\\n\t", root["s"]);
        }

        [Test]
        public void Parse_根之后有多余内容_抛异常()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{} {}"));
        }

        [Test]
        public void Parse_常见残缺_抛异常()
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": 1,}"), "尾逗号");
            Assert.Throws<FormatException>(() => MiniJson.Parse("[1, 2,"), "数组意外结束");
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\" 1}"), "缺冒号");
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": \"未闭合"), "字符串未闭合");
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\": tru}"), "残缺字面量");
            Assert.Throws<FormatException>(() => MiniJson.Parse("{a: 1}"), "键不是字符串");
            Assert.Throws<FormatException>(() => MiniJson.Parse(""));
            Assert.Throws<ArgumentNullException>(() => MiniJson.Parse(null));
        }
    }
}
