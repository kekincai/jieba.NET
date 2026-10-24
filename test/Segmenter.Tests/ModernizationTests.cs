using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using JiebaNet.Analyser;
using JiebaNet.Segmenter.PosSeg;
using NUnit.Framework;

namespace JiebaNet.Segmenter.Tests
{
    [TestFixture]
    public class ModernizationTests
    {
        [Test]
        public void Deleting_a_word_also_prevents_hmm_from_recreating_it()
        {
            var segmenter = new JiebaSegmenter();
            var dictionary = WordDictionary.Instance;
            dictionary.Trie.TryGetValue("台中", out var previousFrequency);
            try
            {
                segmenter.AddWord("台中", 1000);
                Assert.That(segmenter.Cut("台中"), Is.EqualTo(new[] { "台中" }));
                segmenter.DeleteWord("台中");
                Assert.That(segmenter.Cut("台中"), Is.EqualTo(new[] { "台", "中" }));
            }
            finally { dictionary.AddWord("台中", previousFrequency); }
        }

        [Test]
        public void Empty_text_and_token_offsets_preserve_the_input()
        {
            var segmenter = new JiebaSegmenter();
            Assert.That(segmenter.Cut(""), Is.Empty);
            Assert.That(segmenter.CutForSearch(""), Is.Empty);
            Assert.That(new PosSegmenter(segmenter).Cut(""), Is.Empty);
            const string text = "北京旅行_樱花🌸2026.jpg";
            foreach (var mode in new[] { TokenizerMode.Default, TokenizerMode.Search })
            {
                var tokens = segmenter.Tokenize(text, mode).ToArray();
                foreach (var token in tokens)
                    Assert.That(text.Substring(token.StartIndex, token.EndIndex - token.StartIndex), Is.EqualTo(token.Word));
            }
            Assert.That(string.Concat(segmenter.Cut(text)), Is.EqualTo(text));
            Assert.Throws<ArgumentNullException>(() => segmenter.Cut(null));
        }

        [Test]
        public void Concurrent_readers_and_dictionary_updates_preserve_complete_results()
        {
            var segmenter = new JiebaSegmenter();
            var pos = new PosSegmenter(segmenter);
            const string word = "并发回归专用词";
            const string text = "北京旅行并发回归专用词2026";
            try
            {
                Parallel.For(0, 600, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    if (i % 6 == 0) segmenter.AddWord(word, 500, "nz");
                    else if (i % 6 == 1) segmenter.DeleteWord(word);
                    else if (i % 6 == 2) Assert.That(string.Concat(pos.Cut(text).Select(p => p.Word)), Is.EqualTo(text));
                    else Assert.That(string.Concat(segmenter.Cut(text)), Is.EqualTo(text));
                });
                segmenter.AddWord(word, 500, "nz");
                Assert.That(new PosSegmenter().Cut(word).Select(p => (p.Word, p.Flag)), Is.EqualTo(new[] { (word, "nz") }));
            }
            finally { segmenter.DeleteWord(word); }
        }

        [Test]
        public void Parallel_api_keeps_order_and_matches_sequential_results()
        {
            var segmenter = new JiebaSegmenter();
            var names = new[] { "北京旅行合影.jpg", "京都和服樱花.mp4", "IMG_20261024.jpg", "海边🌸日落.png" };
            Assert.That(segmenter.CutInParallel(names).Select(words => string.Join("|", words)),
                Is.EqualTo(names.Select(name => string.Join("|", segmenter.Cut(name)))));
            Assert.That(segmenter.CutForSearchInParallel(names).Select(words => string.Join("|", words)),
                Is.EqualTo(names.Select(name => string.Join("|", segmenter.CutForSearch(name)))));
        }

        [Test]
        public void A_missing_or_malformed_user_dictionary_fails_without_partial_updates()
        {
            var segmenter = new JiebaSegmenter();
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            const string word = "未提交回归专用词";
            Assert.Throws<FileNotFoundException>(() => segmenter.LoadUserDict(path));
            try
            {
                File.WriteAllText(path, word + " 500 nz\n错误词频 999999999999999999999999 nz");
                Assert.Throws<InvalidDataException>(() => segmenter.LoadUserDict(path));
                Assert.That(WordDictionary.Instance.ContainsWord(word), Is.False);
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Loading_the_same_user_dictionary_is_idempotent_and_shares_tags()
        {
            var segmenter = new JiebaSegmenter();
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
            const string word = "用户词典回归专用词";
            try
            {
                File.WriteAllText(path, word + " 500 nz\n");
                segmenter.LoadUserDict(path);
                var total = WordDictionary.Instance.Total;
                new JiebaSegmenter().LoadUserDict(path);
                Assert.That(WordDictionary.Instance.Total, Is.EqualTo(total));
                Assert.That(new PosSegmenter().Cut(word).Select(p => p.Flag), Is.EqualTo(new[] { "nz" }));
            }
            finally { File.Delete(path); segmenter.DeleteWord(word); }
        }

        [Test]
        public void Keyword_model_numbers_are_independent_of_the_process_culture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                const string text = "北京清华大学自然语言处理研究";
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var expected = new TfidfExtractor().ExtractTagsWithWeight(text).Select(p => (p.Word, p.Weight)).ToArray();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var actual = new TfidfExtractor().ExtractTagsWithWeight(text).Select(p => (p.Word, p.Weight)).ToArray();
                Assert.That(actual, Is.EqualTo(expected));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("{\"ab\":1}")]
        [TestCase("{\"\":1}")]
        public void Invalid_character_model_keys_are_rejected(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<char, double>>(json, ModelJson.Options));
        }

        [Test]
        public void Excessive_model_nesting_is_rejected()
        {
            var json = new string('[', 64) + "0" + new string(']', 64);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonElement>(json, ModelJson.Options));
        }

        [Test]
        public void Resource_configuration_cannot_change_after_initialization()
        {
            _ = new JiebaSegmenter().Cut("北京").ToArray();
            Assert.Throws<InvalidOperationException>(() => ConfigManager.ConfigFileBaseDir = Path.GetTempPath());
        }
    }
}
