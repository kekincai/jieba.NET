using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JiebaNet.Analyser;
using JiebaNet.Segmenter.Common;
using JiebaNet.Segmenter.PosSeg;
using NUnit.Framework;

namespace JiebaNet.Segmenter.Tests;

[TestFixture]
public class UpstreamIssueTests
{
    private string _directory;
    private string _main;

    [SetUp]
    public void CreateFixture()
    {
        _directory = Path.Combine(Path.GetTempPath(), "jieba-issues-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _main = Path.Combine(_directory, "main.txt");
        File.WriteAllText(_main, "基础词 100000000 n\n北京 100 ns\n北京市 100 ns\n海淀区 100 ns\n");
    }

    [TearDown]
    public void RemoveFixture() => Directory.Delete(_directory, recursive: true);

    private JiebaSegmenter Isolated() => new(WordDictionary.Create(_main));

    [Test]
    public void Issue98_emoji_and_supplementary_characters_remain_complete()
    {
        var segmenter = Isolated();
        const string text = "🙂🚀𠮷";
        Assert.That(segmenter.Cut(text), Is.EqualTo(new[] { "🙂", "🚀", "𠮷" }));
        Assert.That(new PosSegmenter(segmenter).Cut(text).Select(pair => pair.Word), Is.EqualTo(new[] { "🙂", "🚀", "𠮷" }));
        Assert.That(segmenter.Tokenize(text).Select(token => (token.StartIndex, token.EndIndex)),
            Is.EqualTo(new[] { (0, 2), (2, 4), (4, 6) }));
    }

    [Test]
    public void Issues91_97_independent_dictionaries_do_not_accumulate_each_others_words()
    {
        var first = Isolated();
        var second = Isolated();
        first.AddWord("独立词典甲", 500, "nz");
        second.AddWord("独立词典乙", 500, "n");
        Assert.That(first.Dictionary.ContainsWord("独立词典乙"), Is.False);
        Assert.That(second.Dictionary.ContainsWord("独立词典甲"), Is.False);
        Assert.That(first.Cut("独立词典甲", hmm: false), Is.EqualTo(new[] { "独立词典甲" }));
        Assert.That(second.Cut("独立词典甲", hmm: false), Has.No.Member("独立词典甲"));
        Assert.That(new PosSegmenter(first).Cut("独立词典甲").Single().Flag, Is.EqualTo("nz"));
        var user = Path.Combine(_directory, "shared-user-file.txt");
        File.WriteAllText(user, "两实例加载同文件 500 nz\n");
        first.LoadUserDict(user);
        second.LoadUserDict(user);
        first.DeleteWord("两实例加载同文件");
        Assert.That(second.Cut("两实例加载同文件", hmm: false), Is.EqualTo(new[] { "两实例加载同文件" }));
    }

    [Test]
    public void Issue79_user_dictionary_is_registered_only_after_a_successful_load()
    {
        var segmenter = Isolated();
        var path = Path.Combine(_directory, "user.txt");
        File.WriteAllText(path, "用户词典测试 500 nz\n");
        segmenter.LoadUserDict(path);
        Assert.That(segmenter.Dictionary.LoadedPaths, Has.Count.EqualTo(1));
        File.Delete(path);
        Assert.DoesNotThrow(() => segmenter.LoadUserDict(path));
    }

    [Test]
    public void Issues96_55_77_user_dictionary_accepts_optional_frequency_tags_and_utf8_bom()
    {
        var segmenter = Isolated();
        var path = Path.Combine(_directory, "user.txt");
        File.WriteAllText(path, "创新办\n云计算 5\n凱特琳 nz\n二号谈话间 732 zg\n一号谈话间 10 nz\n", new UTF8Encoding(true));
        segmenter.LoadUserDict(path);
        foreach (var word in new[] { "创新办", "云计算", "凱特琳", "二号谈话间", "一号谈话间" })
        {
            Assert.That(segmenter.Dictionary.ContainsWord(word), Is.True);
            Assert.That(segmenter.Cut(word, hmm: false), Is.EqualTo(new[] { word }));
        }
        Assert.That(new PosSegmenter(segmenter).Cut("二号谈话间").Single().Flag, Is.EqualTo("zg"));
        var invalid = Path.Combine(_directory, "invalid-encoding.txt");
        File.WriteAllBytes(invalid, new byte[] { 0xd6, 0xd0, 0xce, 0xc4 });
        Assert.Throws<DecoderFallbackException>(() => segmenter.LoadUserDict(invalid));
    }

    [Test]
    public void Issues52_57_85_custom_tags_are_visible_through_both_dictionary_apis()
    {
        var segmenter = Isolated();
        var pos = new PosSegmenter(segmenter);
        segmenter.Dictionary.AddWord("北京市", 6000, "city");
        segmenter.Dictionary.AddWord("海淀区", 6000, "town");
        Assert.That(pos.Cut("北京市海淀区").Select(pair => (pair.Word, pair.Flag)),
            Is.EqualTo(new[] { ("北京市", "city"), ("海淀区", "town") }));
        var path = Path.Combine(_directory, "tags.txt");
        File.WriteAllText(path, "筒形毂 3 n\n");
        segmenter.LoadUserDict(path);
        Assert.That(new PosSegmenter(segmenter).Cut("筒形毂").Select(pair => (pair.Word, pair.Flag)),
            Is.EqualTo(new[] { ("筒形毂", "n") }));
    }

    [Test]
    public void Issue46_deleting_a_word_prevents_hmm_from_putting_it_back()
    {
        var segmenter = Isolated();
        segmenter.AddWord("天半", 500);
        segmenter.DeleteWord("天半");
        Assert.That(segmenter.Cut("2天半"), Is.EqualTo(new[] { "2", "天", "半" }));
    }

    [TestCase("永和服装饰品   有限公司")]
    [TestCase("Issues help repositories track bugs, enhancements, and ideas.")]
    [TestCase("北京🙂  合影.jpg")]
    public void Issues32_69_offsets_are_utf16_and_end_is_exclusive(string text)
    {
        var segmenter = new JiebaSegmenter();
        foreach (var mode in new[] { TokenizerMode.Default, TokenizerMode.Search })
            foreach (var token in segmenter.Tokenize(text, mode))
                Assert.That(text.Substring(token.StartIndex, token.EndIndex - token.StartIndex), Is.EqualTo(token.Word));
    }

    [Test]
    public void Issue87_adjacent_chinese_keywords_are_not_skipped()
    {
        var processor = new KeywordProcessor();
        processor.AddKeywords(new[] { "前", "从", "从前", "从前山", "有座" });
        Assert.That(processor.ExtractKeywords("从前有座从前山"), Is.EqualTo(new[] { "从前", "有座", "从前山" }));
        Assert.That(processor.ExtractKeywordSpans("从前有座从前山"), Is.EqualTo(new[]
        {
            new TextSpan("从前", 0, 2), new TextSpan("有座", 2, 4), new TextSpan("从前山", 4, 7)
        }));
    }

    [Test]
    public void Keyword_boundaries_include_digits_and_matching_is_culture_independent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var processor = new KeywordProcessor();
            processor.AddKeywords(new[] { "FILE", "Java", "JavaScript" });
            Assert.That(processor.ExtractKeywords("file Java2 JavaScript Java"), Is.EqualTo(new[] { "FILE", "JavaScript", "Java" }));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        var mixed = new KeywordProcessor();
        mixed.AddKeywords(new[] { "Java", "中国", "JavaScript" });
        Assert.That(mixed.ExtractKeywords("Java中国JavaScript"), Is.EqualTo(new[] { "Java", "中国", "JavaScript" }));
        var trie = new KeywordTrie();
        trie["a"] = "a";
        trie["ab"] = "ab";
        trie.Remove("a");
        trie.Remove("missing");
        Assert.That(trie.Count, Is.EqualTo(1));
        trie["a"] = "again";
        Assert.That(trie.Count, Is.EqualTo(2));
    }

    [Test]
    public void Issue30_textrank_applies_the_requested_part_of_speech_filter()
    {
        var segmenter = Isolated();
        segmenter.AddWord("回归名词甲", 500, "n"); segmenter.AddWord("回归名词乙", 500, "n");
        segmenter.AddWord("回归动词甲", 500, "v"); segmenter.AddWord("回归动词乙", 500, "v");
        const string text = "回归名词甲回归动词甲回归名词乙回归动词乙";
        var rank = new TextRankExtractor(segmenter);
        Assert.That(rank.ExtractTags(text, allowPos: new[] { "n" }), Is.EquivalentTo(new[] { "回归名词甲", "回归名词乙" }));
        Assert.That(rank.ExtractTags(text, allowPos: new[] { "v" }), Is.EquivalentTo(new[] { "回归动词甲", "回归动词乙" }));
    }

    [Test]
    public void A_failed_idf_update_does_not_replace_the_working_model()
    {
        var extractor = new TfidfExtractor();
        const string text = "北京清华大学研究";
        var before = extractor.ExtractTagsWithWeight(text).Select(pair => (pair.Word, pair.Weight)).ToArray();
        var path = Path.Combine(_directory, "bad-idf.txt");
        File.WriteAllText(path, "北京 NaN\n");
        Assert.Throws<InvalidDataException>(() => extractor.SetIdfPath(path));
        Assert.That(extractor.ExtractTagsWithWeight(text).Select(pair => (pair.Word, pair.Weight)), Is.EqualTo(before));
        File.WriteAllText(path, "北京 2.5\n");
        Assert.DoesNotThrow(() => extractor.SetIdfPath(path));
    }
}
