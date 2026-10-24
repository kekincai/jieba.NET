using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JiebaNet.Parity;
using NUnit.Framework;

namespace JiebaNet.Segmenter.Tests;

[TestFixture]
public class PythonParityTests
{
    private readonly ParityRunner _runner = new();

    public static IEnumerable<TestCaseData> ReferenceCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Cases", "python-parity.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var index = 0;
        foreach (var item in document.RootElement.GetProperty("Cases").EnumerateArray())
            yield return new TestCaseData(item.Deserialize<ParityCase>()).SetName($"PythonParity_{index++:D3}");
    }

    [TestCaseSource(nameof(ReferenceCases))]
    public void Matches_the_pinned_official_python_reference(ParityCase expected)
    {
        var actual = _runner.Evaluate(expected.Text);
        Assert.Multiple(() =>
        {
            Assert.That(actual.Cut, Is.EqualTo(expected.Cut), "Cut: " + expected.Text);
            Assert.That(actual.NoHmm, Is.EqualTo(expected.NoHmm), "NoHmm: " + expected.Text);
            Assert.That(actual.All, Is.EqualTo(expected.All), "All: " + expected.Text);
            Assert.That(actual.Search, Is.EqualTo(expected.Search), "Search: " + expected.Text);
            Assert.That(actual.SearchNoHmm, Is.EqualTo(expected.SearchNoHmm), "SearchNoHmm: " + expected.Text);
            Assert.That(actual.Pos, Is.EqualTo(expected.Pos), "Pos: " + expected.Text);
            Assert.That(actual.PosNoHmm, Is.EqualTo(expected.PosNoHmm), "PosNoHmm: " + expected.Text);
            AssertWeights(actual.Tfidf, expected.Tfidf, "TF-IDF: " + expected.Text);
            AssertWeights(actual.TextRank, expected.TextRank, "TextRank: " + expected.Text);
        });
    }

    private static void AssertWeights(WeightedWord[] actual, WeightedWord[] expected, string message)
    {
        Assert.That(actual.Select(pair => pair.Word), Is.EqualTo(expected.Select(pair => pair.Word)), message);
        if (actual.Length != expected.Length) return;
        for (var i = 0; i < expected.Length; i++)
            Assert.That(actual[i].Weight, Is.EqualTo(expected[i].Weight).Within(Math.Max(1e-12, Math.Abs(expected[i].Weight) * 1e-10)), message);
    }
}
