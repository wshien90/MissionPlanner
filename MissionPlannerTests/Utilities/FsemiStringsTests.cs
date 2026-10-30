using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class FsemiStringsTests
    {
        static readonly string[] Cultures = { "vi-VN" };

        /// <summary>The repository folder: the first parent of the test folder with FsemiStrings.resx.</summary>
        static string Root()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FsemiStrings.resx")))
                dir = dir.Parent;
            if (dir == null)
                Assert.Inconclusive("FsemiStrings.resx not found above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir.FullName;
        }

        static Dictionary<string, string> Load(string culture)
        {
            var name = culture == null ? "FsemiStrings.resx" : "FsemiStrings." + culture + ".resx";
            return XDocument.Load(Path.Combine(Root(), name)).Root.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));
        }

        static string Placeholders(string text)
        {
            return string.Join(",", Regex.Matches(text, @"\{(\d+)").Cast<Match>().Select(m => m.Groups[1].Value)
                .Distinct().OrderBy(x => x));
        }

        [TestMethod]
        public void EveryLanguageHasEveryString()
        {
            var english = Load(null);
            foreach (var culture in Cultures)
            {
                var translated = Load(culture);
                CollectionAssert.AreEquivalent(english.Keys.ToList(), translated.Keys.ToList(), culture);
                foreach (var key in english.Keys)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(translated[key]), culture + " " + key);
            }
        }

        [TestMethod]
        public void TranslationsKeepThePlaceholders()
        {
            var english = Load(null);
            var args = Enumerable.Range(0, 20).Select(i => (object)1.5).ToArray();
            foreach (var culture in new string[] { null }.Concat(Cultures))
            {
                foreach (var pair in Load(culture))
                {
                    Assert.AreEqual(Placeholders(english[pair.Key]), Placeholders(pair.Value), culture + " " + pair.Key);
                    // a stray brace would throw when the text is shown; dates are formatted the same way
                    if (!pair.Value.Contains("HH:mm"))
                        string.Format(pair.Value, args);
                }
            }
        }

        [TestMethod]
        public void EveryKeyInTheCodeExists()
        {
            var root = Root();
            var english = Load(null);
            var files = new[] { "Controls", "GCSViews", "Utilities" }
                .SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories));
            var used = 0;
            foreach (var file in files)
            {
                // every literal shaped like a key, so keys picked with ?: count too
                foreach (Match m in Regex.Matches(File.ReadAllText(file),
                             @"""((?:Delivery|Common|Drop|Plan|Voice)_[A-Za-z]+)"""))
                {
                    used++;
                    Assert.IsTrue(english.ContainsKey(m.Groups[1].Value), Path.GetFileName(file) + ": " + m.Groups[1].Value);
                }
            }

            Assert.IsTrue(used > 40, "found " + used + " uses");
        }
    }
}
