using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace JiebaNet.Segmenter
{
    /// <summary>A shared or explicitly independent dictionary. Concurrent cuts share read access; updates are serialized.</summary>
    public class WordDictionary
    {
        private static readonly Lazy<WordDictionary> Lazy = new(() => new WordDictionary());
        private readonly ReaderWriterLockSlim _gate = new(LockRecursionPolicy.SupportsRecursion);
        internal readonly Dictionary<string, int> Trie = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly IDictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly HashSet<string> ForceSplitWords = new(StringComparer.Ordinal);
        internal readonly ISet<string> LoadedPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly string _mainDictionaryPath;
        private double _total;
        private bool _tagsLoaded;

        public double Total
        {
            get { using var scope = Read(); return _total; }
            set
            {
                if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                using var scope = Write();
                _total = value;
            }
        }

        private WordDictionary(string mainDictionaryPath = null)
        {
            _mainDictionaryPath = mainDictionaryPath == null ? ConfigManager.MainDictFile : Path.GetFullPath(mainDictionaryPath);
            using var scope = Write();
            var lineNumber = 0;
            foreach (var line in ConfigManager.ReadLines(_mainDictionaryPath))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var frequency) || frequency <= 0)
                    throw new InvalidDataException($"Invalid main dictionary entry at line {lineNumber}.");
                // Match upstream initialization, which counts every dictionary row (including a duplicate).
                Trie.TryGetValue(parts[0], out var previous);
                SetWord(parts[0], frequency, null);
                _total += previous;
            }
            if (_total <= 0) throw new InvalidDataException("The main dictionary is empty.");
        }

        public static WordDictionary Instance => Lazy.Value;

        /// <summary>Creates an independent dictionary. Models are shared, but each dictionary owns its trie and updates.</summary>
        public static WordDictionary Create(string mainDictionaryPath = null) => new(mainDictionaryPath);

        internal LockScope Read()
        {
            if (_gate.IsReadLockHeld || _gate.IsWriteLockHeld) return default;
            _gate.EnterReadLock();
            return new LockScope(_gate, false);
        }

        internal LockScope Write()
        {
            if (_gate.IsWriteLockHeld) return default;
            _gate.EnterWriteLock();
            return new LockScope(_gate, true);
        }

        internal readonly struct LockScope : IDisposable
        {
            private readonly ReaderWriterLockSlim _gate;
            private readonly bool _write;
            internal LockScope(ReaderWriterLockSlim gate, bool write) { _gate = gate; _write = write; }
            public void Dispose()
            {
                if (_gate == null) return;
                if (_write) _gate.ExitWriteLock();
                else _gate.ExitReadLock();
            }
        }

        public bool ContainsWord(string word)
        {
            using var scope = Read();
            return Trie.TryGetValue(word, out var frequency) && frequency > 0;
        }

        public int GetFreqOrDefault(string key)
        {
            using var scope = Read();
            return Trie.TryGetValue(key, out var frequency) && frequency > 0 ? frequency : 1;
        }

        public void AddWord(string word, int freq, string tag = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(word);
            if (freq < 0) throw new ArgumentOutOfRangeException(nameof(freq));
            using var scope = Write();
            SetWord(word, freq, tag);
        }

        private void SetWord(string word, int freq, string tag)
        {
            Trie.TryGetValue(word, out var previous);
            var total = _total - previous + freq;
            if (total <= 0) throw new InvalidOperationException("The dictionary must retain a positive total frequency.");
            Trie[word] = freq;
            _total = total;
            if (freq == 0) ForceSplitWords.Add(word);
            else ForceSplitWords.Remove(word);
            if (!string.IsNullOrEmpty(tag)) Tags[word] = tag;
            for (var i = 1; i <= word.Length; i++)
            {
                var prefix = word.Substring(0, i);
                if (!Trie.ContainsKey(prefix)) Trie[prefix] = 0;
            }
        }

        internal void EnsureTagsLoaded()
        {
            using var scope = Write();
            if (_tagsLoaded) return;
            foreach (var line in ConfigManager.ReadLines(_mainDictionaryPath))
            {
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && !Tags.ContainsKey(parts[0])) Tags[parts[0]] = parts[2];
            }
            _tagsLoaded = true;
        }

        public void DeleteWord(string word)
        {
            AddWord(word, 0);
        }

        internal int SuggestFreq(string word, IEnumerable<string> segments)
        {
            using var scope = Read();
            double freq = 1;
            foreach (var segment in segments) freq *= GetFreqOrDefault(segment) / _total;
            return Math.Max((int)(freq * _total) + 1, GetFreqOrDefault(word));
        }
    }
}
