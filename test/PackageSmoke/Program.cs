using System.Text;
using System.Text.Json;
using JiebaNet.Analyser;
using JiebaNet.Segmenter;
using JiebaNet.Segmenter.PosSeg;

var mode = args.Length == 0 ? "embedded" : args[0];
var temporary = Path.Combine(Path.GetTempPath(), "jieba-smoke-" + Guid.NewGuid().ToString("N"));
try
{
    if (mode is "external" or "missing" or "corrupt")
    {
        Directory.CreateDirectory(temporary);
        if (mode != "missing")
        {
            var assembly = typeof(ConfigManager).Assembly;
            const string prefix = "JiebaNet.Resources.";
            foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)))
            {
                using var input = assembly.GetManifestResourceStream(name)!;
                using var output = File.Create(Path.Combine(temporary, name.Substring(prefix.Length)));
                input.CopyTo(output);
            }
        }
        if (mode == "external")
            File.AppendAllText(Path.Combine(temporary, "dict.txt"), "\n外部词典回归专用词 1000000 nz\n", Encoding.UTF8);
        if (mode == "corrupt")
            File.WriteAllText(Path.Combine(temporary, "prob_emit.json"), "{ invalid json }");
        ConfigManager.ConfigFileBaseDir = temporary;
    }
    else if (mode != "embedded") throw new ArgumentException("Unknown smoke-test mode.");

    if (mode == "missing")
    {
        try { _ = new JiebaSegmenter().Cut("北京").ToArray(); }
        catch (Exception exception) when (exception.GetBaseException() is FileNotFoundException)
        { Console.WriteLine("PASS missing resource fails explicitly"); return 0; }
        throw new Exception("Missing resources did not fail.");
    }
    if (mode == "corrupt")
    {
        try { _ = JiebaNet.Segmenter.FinalSeg.Viterbi.Instance; }
        catch (Exception exception) when (exception.GetBaseException() is JsonException)
        { Console.WriteLine("PASS malformed model fails explicitly"); return 0; }
        throw new Exception("Malformed models did not fail.");
    }

    var segmenter = new JiebaSegmenter();
    Assert(segmenter.Cut("我来到北京清华大学").SequenceEqual(new[] { "我", "来到", "北京", "清华大学" }), "accurate segmentation");
    Assert(segmenter.CutForSearch("清华大学").Contains("大学"), "search segmentation");
    var pos = new PosSegmenter(segmenter);
    Assert(pos.Cut("北京").Any(pair => pair.Word == "北京" && pair.Flag == "ns"), "part-of-speech model");
    Assert(new TfidfExtractor().ExtractTags("北京清华大学自然语言处理研究").Any(), "TF-IDF model");
    Assert(new TextRankExtractor().ExtractTags("北京清华大学自然语言处理研究").Any(), "TextRank model");
    if (mode == "external")
        Assert(segmenter.Cut("外部词典回归专用词").SequenceEqual(new[] { "外部词典回归专用词" }), "external dictionary override");
    Console.WriteLine($"PASS {mode}: segmentation, search, POS, TF-IDF and TextRank");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
}

static void Assert(bool condition, string operation)
{
    if (!condition) throw new Exception("Failed: " + operation);
}
