using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter.PosSeg
{
    public partial class PosSegmenter
    {
        [GeneratedRegex(@"([\u4E00-\u9FD5a-zA-Z0-9+#&\._]+)")]
        private static partial Regex CreateRegexChineseInternal();

        [GeneratedRegex(@"(\r\n|\s)")]
        private static partial Regex CreateRegexSkipInternal();

        [GeneratedRegex(@"([\u4E00-\u9FD5]+)")]
        private static partial Regex CreateRegexChineseDetail();

        [GeneratedRegex(@"([\.0-9]+|[a-zA-Z0-9]+)")]
        private static partial Regex CreateRegexSkipDetail();

        [GeneratedRegex(@"[a-zA-Z0-9]+")]
        private static partial Regex CreateRegexEnglishWords();

        [GeneratedRegex(@"[\.0-9]+")]
        private static partial Regex CreateRegexNumbers();

        [GeneratedRegex(@"^[a-zA-Z0-9]$")]
        private static partial Regex CreateRegexEnglishChar();

        private readonly WordDictionary WordDict;
        private static Viterbi PosSeg => Viterbi.Instance;

        #region Regular Expressions

        internal static readonly Regex RegexChineseInternal = CreateRegexChineseInternal();
        internal static readonly Regex RegexSkipInternal = CreateRegexSkipInternal();

        internal static readonly Regex RegexChineseDetail = CreateRegexChineseDetail();
        internal static readonly Regex RegexSkipDetail = CreateRegexSkipDetail();

        internal static readonly Regex RegexEnglishWords = CreateRegexEnglishWords();
        internal static readonly Regex RegexNumbers = CreateRegexNumbers();

        internal static readonly Regex RegexEnglishChar = CreateRegexEnglishChar();

        #endregion

        private readonly JiebaSegmenter _segmenter;

        public PosSegmenter() : this(new JiebaSegmenter()) { }

        public PosSegmenter(JiebaSegmenter segmenter)
        {
            _segmenter = segmenter ?? throw new ArgumentNullException(nameof(segmenter));
            WordDict = segmenter.Dictionary;
            WordDict.EnsureTagsLoaded();
        }

        public IEnumerable<Pair> Cut(string text, bool hmm = true)
        {
            return CutInternal(text, hmm);
        }
        
        public IEnumerable<IEnumerable<Pair>> CutInParallel(IEnumerable<string> texts, bool hmm = true)
        {
            return texts.AsParallel().AsOrdered().Select(text => CutInternal(text, hmm));
        }
        
        public IEnumerable<IEnumerable<Pair>> CutInParallel(string text, bool hmm = true)
        {
            var lines = text.SplitLines();
            return CutInParallel(lines, hmm);
        }

        #region Internal Cut Methods

        internal IEnumerable<Pair> CutInternal(string text, bool hmm = true)
        {
            ArgumentNullException.ThrowIfNull(text);
            using var scope = WordDict.Read();

            var blocks = RegexChineseInternal.Split(text);
            Func<string, IEnumerable<Pair>> cutMethod = null;
            if (hmm)
            {
                cutMethod = CutDag;
            }
            else
            {
                cutMethod = CutDagWithoutHmm;
            }

            var tokens = new List<Pair>();
            foreach (var blk in blocks)
            {
                if (RegexChineseInternal.IsMatch(blk))
                {
                    tokens.AddRange(cutMethod(blk));
                }
                else
                {
                    var tmp = RegexSkipInternal.Split(blk);
                    foreach (var x in tmp)
                    {
                        if (RegexSkipInternal.IsMatch(x))
                        {
                            tokens.Add(new Pair(x, "x"));
                        }
                        else
                        {
                            foreach (var xx in x.EnumerateRunes())
                            {
                                // TODO: each char?
                                var xxs = xx.ToString();
                                if (RegexNumbers.IsMatch(xxs))
                                {
                                    tokens.Add(new Pair(xxs, "m"));
                                }
                                else if (RegexEnglishWords.IsMatch(x))
                                {
                                    tokens.Add(new Pair(xxs, "eng"));
                                }
                                else
                                {
                                    tokens.Add(new Pair(xxs, "x"));
                                }
                            }
                        }
                    }
                }
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDag(string sentence)
        {
            var dag = _segmenter.GetDag(sentence);
            var route = _segmenter.Calc(sentence, dag);

            var tokens = new List<Pair>();

            var x = 0;
            var n = sentence.Length;
            var buf = string.Empty;
            while (x < n)
            {
                var y = route[x].Key + 1;
                var w = sentence.Substring(x, y - x);
                if (y - x == 1)
                {
                    buf += w;
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        AddBufferToWordList(tokens, buf);
                        buf = string.Empty;
                    }
                    tokens.Add(new Pair(w, WordDict.Tags.GetDefault(w, "x")));
                }
                x = y;
            }

            if (buf.Length > 0)
            {
                AddBufferToWordList(tokens, buf);
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDagWithoutHmm(string sentence)
        {
            var dag = _segmenter.GetDag(sentence);
            var route = _segmenter.Calc(sentence, dag);

            var tokens = new List<Pair>();

            var x = 0;
            var buf = string.Empty;
            var n = sentence.Length;

            var y = -1;
            while (x < n)
            {
                y = route[x].Key + 1;
                var w = sentence.Substring(x, y - x);
                // TODO: char or word?
                if (RegexEnglishChar.IsMatch(w))
                {
                    buf += w;
                    x = y;
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        tokens.Add(new Pair(buf, "eng"));
                        buf = string.Empty;
                    }
                    tokens.Add(new Pair(w, WordDict.Tags.GetDefault(w, "x")));
                    x = y;
                }
            }

            if (buf.Length > 0)
            {
                tokens.Add(new Pair(buf, "eng"));
            }

            return tokens;
        }

        internal IEnumerable<Pair> CutDetail(string text)
        {
            var tokens = new List<Pair>();
            var blocks = RegexChineseDetail.Split(text);
            foreach (var blk in blocks)
            {
                if (RegexChineseDetail.IsMatch(blk))
                {
                    tokens.AddRange(PosSeg.Cut(blk));
                }
                else
                {
                    var tmp = RegexSkipDetail.Split(blk);
                    foreach (var x in tmp)
                    {
                        if (!string.IsNullOrWhiteSpace(x))
                        {
                            if (RegexNumbers.Match(x) is { Success: true, Index: 0 })
                            {
                                tokens.Add(new Pair(x, "m"));
                            }
                            else if(RegexEnglishWords.IsMatch(x))
                            {
                                tokens.Add(new Pair(x, "eng"));
                            }
                            else
                            {
                                tokens.Add(new Pair(x, "x"));
                            }
                        }
                    }
                }
            }

            return tokens;
        }

        #endregion

        #region Private Helpers

        private void AddBufferToWordList(List<Pair> words, string buf)
        {
            if (buf.Length == 1)
            {
                words.Add(new Pair(buf, WordDict.Tags.GetDefault(buf, "x")));
            }
            else
            {
                if (!WordDict.ContainsWord(buf))
                {
                    var tokens = CutDetail(buf);
                    words.AddRange(tokens);
                }
                else
                {
                    words.AddRange(buf.Select(ch => new Pair(ch.ToString(), WordDict.Tags.GetDefault(ch.ToString(), "x"))));
                }
            }
        }

        #endregion
    }
}