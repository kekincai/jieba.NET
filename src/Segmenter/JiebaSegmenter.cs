using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JiebaNet.Segmenter.Common;
using JiebaNet.Segmenter.FinalSeg;

namespace JiebaNet.Segmenter
{
    public partial class JiebaSegmenter
    {
        [GeneratedRegex(@"([\u4E00-\u9FD5a-zA-Z0-9+#&\._%·\-]+)")]
        private static partial Regex CreateRegexChineseDefault();

        [GeneratedRegex(@"(\r\n|\s)")]
        private static partial Regex CreateRegexSkipDefault();

        [GeneratedRegex(@"([\u4E00-\u9FD5]+)")]
        private static partial Regex CreateRegexChineseCutAll();

        [GeneratedRegex(@"[^a-zA-Z0-9+#\n]")]
        private static partial Regex CreateRegexSkipCutAll();

        [GeneratedRegex(@"[a-zA-Z0-9]")]
        private static partial Regex CreateRegexEnglishChars();

        [GeneratedRegex("^(?<word>.+?)(?<freq> [0-9]+)?(?<tag> [a-z]+)?$")]
        private static partial Regex CreateRegexUserDict();

        private readonly WordDictionary WordDict;
        internal WordDictionary Dictionary => WordDict;
        private static Viterbi FinalSeg => Viterbi.Instance;

        #region Regular Expressions

        internal static readonly Regex RegexChineseDefault = CreateRegexChineseDefault();

        internal static readonly Regex RegexSkipDefault = CreateRegexSkipDefault();

        internal static readonly Regex RegexChineseCutAll = CreateRegexChineseCutAll();
        internal static readonly Regex RegexSkipCutAll = CreateRegexSkipCutAll();

        internal static readonly Regex RegexEnglishChars = CreateRegexEnglishChars();

        internal static readonly Regex RegexUserDict = CreateRegexUserDict();

        #endregion

        public JiebaSegmenter() : this(WordDictionary.Instance) { }

        public JiebaSegmenter(WordDictionary dictionary)
        {
            WordDict = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        /// <summary>
        /// The main function that segments an entire sentence that contains 
        /// Chinese characters into seperated words.
        /// </summary>
        /// <param name="text">The string to be segmented.</param>
        /// <param name="cutAll">Specify segmentation pattern. True for full pattern, False for accurate pattern.</param>
        /// <param name="hmm">Whether to use the Hidden Markov Model.</param>
        /// <returns></returns>
        public IEnumerable<string> Cut(string text, bool cutAll = false, bool hmm = true)
        {
            ArgumentNullException.ThrowIfNull(text);
            using var scope = WordDict.Read();
            var reHan = RegexChineseDefault;
            var reSkip = RegexSkipDefault;
            var cutMethod = cutAll ? CutAll : hmm ? CutDag : (Func<string, IEnumerable<string>>)CutDagWithoutHmm;
            return CutIt(text, cutMethod, reHan, reSkip, cutAll);
        }
        
        public IEnumerable<IEnumerable<string>> CutInParallel(IEnumerable<string> texts, bool cutAll = false, bool hmm = true)
        {
            ArgumentNullException.ThrowIfNull(texts);
            return texts.AsParallel().AsOrdered().Select(text => Cut(text, cutAll, hmm));
        }
        
        public IEnumerable<string> CutInParallel(string text, bool cutAll = false, bool hmm = true)
        {
            var lines = text.SplitLines();
            return CutInParallel(lines, cutAll, hmm).SelectMany(words => words);
        }

        public IEnumerable<string> CutForSearch(string text, bool hmm = true)
        {
            ArgumentNullException.ThrowIfNull(text);
            using var scope = WordDict.Read();
            var result = new List<string>();

            var words = Cut(text, hmm: hmm);
            foreach (var w in words)
            {
                if (w.Length > 2)
                {
                    foreach (var i in Enumerable.Range(0, w.Length - 1))
                    {
                        var gram2 = w.Substring(i, 2);
                        if (WordDict.ContainsWord(gram2))
                        {
                            result.Add(gram2);
                        }
                    }
                }

                if (w.Length > 3)
                {
                    foreach (var i in Enumerable.Range(0, w.Length - 2))
                    {
                        var gram3 = w.Substring(i, 3);
                        if (WordDict.ContainsWord(gram3))
                        {
                            result.Add(gram3);
                        }
                    }
                }

                result.Add(w);
            }

            return result;
        }
        
        public IEnumerable<IEnumerable<string>> CutForSearchInParallel(IEnumerable<string> texts, bool hmm = true)
        {
            return texts.AsParallel().AsOrdered().Select(line => CutForSearch(line, hmm));
        }
        
        public IEnumerable<string> CutForSearchInParallel(string text, bool hmm = true)
        {
            var lines = text.SplitLines();
            return CutForSearchInParallel(lines, hmm).SelectMany(words => words);
        }

        public IEnumerable<Token> Tokenize(string text, TokenizerMode mode = TokenizerMode.Default, bool hmm = true)
        {
            ArgumentNullException.ThrowIfNull(text);
            using var scope = WordDict.Read();
            var result = new List<Token>();

            var start = 0;
            if (mode == TokenizerMode.Default)
            {
                foreach (var w in Cut(text, hmm: hmm))
                {
                    var width = w.Length;
                    result.Add(new Token(w, start, start + width));
                    start += width;
                }
            }
            else
            {
                foreach (var w in Cut(text, hmm: hmm))
                {
                    var width = w.Length;
                    if (width > 2)
                    {
                        for (var i = 0; i < width - 1; i++)
                        {
                            var gram2 = w.Substring(i, 2);
                            if (WordDict.ContainsWord(gram2))
                            {
                                result.Add(new Token(gram2, start + i, start + i + 2));
                            }
                        }
                    }
                    if (width > 3)
                    {
                        for (var i = 0; i < width - 2; i++)
                        {
                            var gram3 = w.Substring(i, 3);
                            if (WordDict.ContainsWord(gram3))
                            {
                                result.Add(new Token(gram3, start + i, start + i + 3));
                            }
                        }
                    }

                    result.Add(new Token(w, start, start + width));
                    start += width;
                }
            }

            return result;
        }

        #region Internal Cut Methods

        internal IDictionary<int, List<int>> GetDag(string sentence)
        {
            var dag = new Dictionary<int, List<int>>(sentence.Length);
            var trie = WordDict.Trie.GetAlternateLookup<ReadOnlySpan<char>>();
            for (var k = 0; k < sentence.Length; k++)
            {
                var ends = new List<int>(2);
                for (var end = k; end < sentence.Length; end++)
                {
                    if (!trie.TryGetValue(sentence.AsSpan(k, end - k + 1), out var frequency)) break;
                    if (frequency > 0) ends.Add(end);
                }
                if (ends.Count == 0) ends.Add(k);
                dag[k] = ends;
            }

            return dag;
        }

        internal IDictionary<int, Pair<int>> Calc(string sentence, IDictionary<int, List<int>> dag)
        {
            var n = sentence.Length;
            var route = new Dictionary<int, Pair<int>>(n + 1);
            var trie = WordDict.Trie.GetAlternateLookup<ReadOnlySpan<char>>();
            route[n] = new Pair<int>(0, 0.0);

            var logtotal = Math.Log(WordDict.Total);
            for (var i = n - 1; i > -1; i--)
            {
                var candidate = new Pair<int>(-1, double.MinValue);
                foreach (int x in dag[i])
                {
                    trie.TryGetValue(sentence.AsSpan(i, x - i + 1), out var frequency);
                    var freq = Math.Log(frequency > 0 ? frequency : 1) - logtotal + route[x + 1].Freq;
                    if (candidate.Freq < freq || (candidate.Freq == freq && candidate.Key < x))
                    {
                        candidate.Freq = freq;
                        candidate.Key = x;
                    }
                }
                route[i] = candidate;
            }
            return route;
        }

        internal IEnumerable<string> CutAll(string sentence)
        {
            var dag = GetDag(sentence);

            var words = new List<string>();
            var lastPos = -1;
            var scanningEnglish = false;
            var english = new StringBuilder();
            foreach (var pair in dag)
            {
                var k = pair.Key;
                var nexts = pair.Value;
                if (scanningEnglish && !char.IsAsciiLetterOrDigit(sentence[k]))
                {
                    scanningEnglish = false;
                    words.Add(english.ToString());
                }
                if (nexts.Count == 1 && k > lastPos)
                {
                    var word = sentence.Substring(k, nexts[0] + 1 - k);
                    if (char.IsAsciiLetterOrDigit(word[0]))
                    {
                        if (!scanningEnglish) { scanningEnglish = true; english.Clear(); }
                        english.Append(word);
                    }
                    if (!scanningEnglish) words.Add(word);
                    lastPos = nexts[0];
                }
                else
                {
                    foreach (var j in nexts)
                    {
                        if (j > k)
                        {
                            words.Add(sentence.Substring(k, j + 1 - k));
                            lastPos = j;
                        }
                    }
                }
            }
            if (scanningEnglish) words.Add(english.ToString());

            return words;
        }

        internal IEnumerable<string> CutDag(string sentence)
        {
            var dag = GetDag(sentence);
            var route = Calc(sentence, dag);

            var tokens = new List<string>();

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
                    tokens.Add(w);
                }
                x = y;
            }

            if (buf.Length > 0)
            {
                AddBufferToWordList(tokens, buf);
            }

            return tokens;
        }

        internal IEnumerable<string> CutDagWithoutHmm(string sentence)
        {
            var dag = GetDag(sentence);
            var route = Calc(sentence, dag);

            var words = new List<string>();

            var x = 0;
            string buf = string.Empty;
            var N = sentence.Length;

            var y = -1;
            while (x < N)
            {
                y = route[x].Key + 1;
                var l_word = sentence.Substring(x, y - x);
                if (RegexEnglishChars.IsMatch(l_word) && l_word.Length == 1)
                {
                    buf += l_word;
                    x = y;
                }
                else
                {
                    if (buf.Length > 0)
                    {
                        words.Add(buf);
                        buf = string.Empty;
                    }
                    words.Add(l_word);
                    x = y;
                }
            }

            if (buf.Length > 0)
            {
                words.Add(buf);
            }

            return words;
        }

        internal IEnumerable<string> CutIt(string text, Func<string, IEnumerable<string>> cutMethod,
                                           Regex reHan, Regex reSkip, bool cutAll)
        {
            using var scope = WordDict.Read();
            var result = new List<string>();
            var blocks = reHan.Split(text);
            foreach (var blk in blocks)
            {
                if (string.IsNullOrEmpty(blk))
                {
                    continue;
                }

                if (reHan.IsMatch(blk))
                {
                    foreach (var word in cutMethod(blk))
                    {
                        result.Add(word);
                    }
                }
                else
                {
                    var tmp = reSkip.Split(blk);
                    foreach (var x in tmp)
                    {
                        if (reSkip.IsMatch(x))
                        {
                            result.Add(x);
                        }
                        else if (!cutAll)
                        {
                            foreach (var ch in x.EnumerateRunes())
                            {
                                result.Add(ch.ToString());
                            }
                        }
                        else
                        {
                            result.Add(x);
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Extend Main Dict

        /// <summary>
        /// Loads user dictionaries.
        /// </summary>
        /// <param name="userDictFile"></param>
        public void LoadUserDict(string userDictFile)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userDictFile);
            var fullPath = Path.GetFullPath(userDictFile);
            using (WordDict.Read())
                if (WordDict.LoadedPaths.Contains(fullPath)) return;
            // Parse the whole file first: a malformed entry must not leave a partially applied dictionary.
            var entries = new List<(string Word, int Frequency, string Tag)>();
            var lineNumber = 0;
            foreach (var line in File.ReadLines(fullPath, new UTF8Encoding(false, true)))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var match = RegexUserDict.Match(line.Trim());
                if (!match.Success) throw new InvalidDataException($"Invalid user dictionary entry at line {lineNumber}.");
                var word = match.Groups["word"].Value.Trim();
                var frequencyText = match.Groups["freq"].Value.Trim();
                var frequency = 0;
                if (word.Length == 0 || (frequencyText.Length > 0 && !int.TryParse(frequencyText, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out frequency)))
                    throw new InvalidDataException($"Invalid user dictionary frequency at line {lineNumber}.");
                entries.Add((word, frequency, match.Groups["tag"].Value.Trim()));
            }
            using var scope = WordDict.Write();
            if (WordDict.LoadedPaths.Contains(fullPath)) return;
            foreach (var entry in entries) AddWord(entry.Word, entry.Frequency, entry.Tag);
            WordDict.LoadedPaths.Add(fullPath);
        }

        public void AddWord(string word, int freq = 0, string tag = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(word);
            using var scope = WordDict.Write();
            if (freq <= 0) freq = WordDict.SuggestFreq(word, Cut(word, hmm: false));
            WordDict.AddWord(word, freq, tag);
        }

        public void DeleteWord(string word)
        {
            WordDict.DeleteWord(word);
        }

        #endregion

        #region Private Helpers

        private void AddBufferToWordList(List<string> words, string buf)
        {
            if (buf.Length == 1)
            {
                words.Add(buf);
            }
            else
            {
                if (!WordDict.ContainsWord(buf))
                {
                    var tokens = FinalSeg.Cut(buf, WordDict);
                    words.AddRange(tokens);
                }
                else
                {
                    words.AddRange(buf.Select(ch => ch.ToString()));
                }
            }
        }

        #endregion
    }

    public enum TokenizerMode
    {
        Default,
        Search
    }
}
