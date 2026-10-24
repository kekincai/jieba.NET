using System;
using System.Collections.Generic;
using System.Linq;
using JiebaNet.Analyser;
using JiebaNet.Segmenter;
using JiebaNet.Segmenter.PosSeg;

namespace JiebaNet.Parity;

public sealed record TaggedWord(string Word, string Flag);
public sealed record WeightedWord(string Word, double Weight);
public sealed record ParityCase(string Text, string[] Cut, string[] NoHmm, string[] All,
    string[] Search, string[] SearchNoHmm, TaggedWord[] Pos, TaggedWord[] PosNoHmm,
    WeightedWord[] Tfidf, WeightedWord[] TextRank);

public sealed class ParityRunner
{
    private readonly JiebaSegmenter _segmenter = new();
    private readonly PosSegmenter _pos;
    private readonly TfidfExtractor _tfidf;
    private readonly TextRankExtractor _textRank = new();
    private static readonly string[] StandardPos = { "ns", "n", "vn", "v" };

    public ParityRunner()
    {
        _pos = new PosSegmenter(_segmenter);
        _tfidf = new TfidfExtractor(_segmenter);
    }

    public ParityCase Evaluate(string text) => new(text,
        _segmenter.Cut(text).ToArray(), _segmenter.Cut(text, hmm: false).ToArray(),
        _segmenter.Cut(text, cutAll: true).ToArray(), _segmenter.CutForSearch(text).ToArray(),
        _segmenter.CutForSearch(text, hmm: false).ToArray(),
        _pos.Cut(text).Select(pair => new TaggedWord(pair.Word, pair.Flag)).ToArray(),
        _pos.Cut(text, hmm: false).Select(pair => new TaggedWord(pair.Word, pair.Flag)).ToArray(),
        _tfidf.ExtractTagsWithWeight(text).Select(pair => new WeightedWord(pair.Word, pair.Weight)).ToArray(),
        _textRank.ExtractTagsWithWeight(text, allowPos: StandardPos).Select(pair => new WeightedWord(pair.Word, pair.Weight)).ToArray());
}
