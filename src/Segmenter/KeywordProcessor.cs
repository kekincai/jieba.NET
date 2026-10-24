using System.Collections.Generic;
using System.Linq;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter
{
    public class KeywordProcessor
    {
        private readonly KeywordTrie KeywordTrie = new KeywordTrie();
        private static bool IsWordCharacter(char value) => char.IsAsciiLetterOrDigit(value) || value == '_';

        public bool CaseSensitive { get; }

        public KeywordProcessor(bool caseSensitive = false)
        {
            CaseSensitive = caseSensitive;
        }
        
        public void AddKeyword(string keyword, string cleanName = null)
        {
            SetItem(keyword, cleanName);
        }

        public void AddKeywords(IEnumerable<string> keywords)
        {
            foreach (var keyword in keywords)
            {
                AddKeyword(keyword);
            }
        }

        public void RemoveKeyword(string keyword)
        {
            if (!CaseSensitive)
            {
                keyword = keyword.ToLowerInvariant();
            }
            KeywordTrie.Remove(keyword);
        }
        
        public void RemoveKeywords(IEnumerable<string> keywords)
        {
            foreach (var keyword in keywords)
            {
                RemoveKeyword(keyword);
            }
        }

        public bool Contains(string word)
        {
            return GetItem(word).IsNotNull();
        }

        public IEnumerable<TextSpan> ExtractKeywordSpans(string sentence)
        {
            var spans = new List<TextSpan>();
            if (string.IsNullOrEmpty(sentence)) return spans;
            var normalized = CaseSensitive ? sentence : sentence.ToLowerInvariant();
            var start = 0;
            while (start < normalized.Length)
            {
                if (start > 0 && IsWordCharacter(normalized[start]) && IsWordCharacter(normalized[start - 1])) { start++; continue; }
                KeywordTrieNode state = KeywordTrie;
                string longest = null;
                var longestEnd = start;
                for (var index = start; index < normalized.Length; index++)
                {
                    state = state.GetChild(normalized[index]);
                    if (state == null) break;
                    var end = index + 1;
                    if (state.HasValue && (end == normalized.Length || !IsWordCharacter(normalized[end - 1]) || !IsWordCharacter(normalized[end])))
                    {
                        longest = state.Value;
                        longestEnd = end;
                    }
                }
                if (longest != null)
                {
                    spans.Add(new TextSpan(longest, start, longestEnd));
                    start = longestEnd;
                }
                else start++;
            }
            return spans;
        }

        public IEnumerable<string> ExtractKeywords(string sentence, bool raw = false)
        {
            if (raw)
            {
                return ExtractKeywordSpans(sentence).Select(span => sentence.Sub(span.Start, span.End));
            }
            
            return ExtractKeywordSpans(sentence).Select(span => span.Text);
        }

        #region Private methods

        private void SetItem(string keyword, string cleanName)
        {
            if (cleanName.IsEmpty() && keyword.IsNotEmpty())
            {
                cleanName = keyword;
            }

            if (keyword.IsNotEmpty() && cleanName.IsNotEmpty())
            {
                if (!CaseSensitive)
                {
                    keyword = keyword.ToLowerInvariant();
                }

                KeywordTrie[keyword] = cleanName;
            }
        }
        
        private string GetItem(string word)
        {
            if (!CaseSensitive)
            {
                word = word.ToLowerInvariant();
            }

            return KeywordTrie[word];
        }

        #endregion
    }
}