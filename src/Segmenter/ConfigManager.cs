using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JiebaNet.Segmenter
{
    /// <summary>Built-in resources are embedded. Configure external resources before the first read.</summary>
    public class ConfigManager
    {
        private static readonly object Gate = new();
        private static string _externalDirectory;
        private static bool _initialized;
        private static readonly HashSet<string> BuiltInNames = new(StringComparer.Ordinal)
        {
            "dict.txt", "prob_trans.json", "prob_emit.json", "pos_prob_start.json", "pos_prob_trans.json",
            "pos_prob_emit.json", "char_state_tab.json", "idf.txt", "stopwords.txt"
        };

        public static string ConfigFileBaseDir
        {
            get { lock (Gate) return _externalDirectory ?? Path.Combine(AppContext.BaseDirectory, "Resources"); }
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                var directory = Path.GetFullPath(value, AppContext.BaseDirectory);
                lock (Gate)
                {
                    if (_initialized)
                        throw new InvalidOperationException("Configure the resource directory before using jieba.NET. Resources are shared for the process lifetime.");
                    _externalDirectory = directory;
                }
            }
        }

        public static string MainDictFile => Path.Combine(ConfigFileBaseDir, "dict.txt");
        public static string ProbTransFile => Path.Combine(ConfigFileBaseDir, "prob_trans.json");
        public static string ProbEmitFile => Path.Combine(ConfigFileBaseDir, "prob_emit.json");
        public static string PosProbStartFile => Path.Combine(ConfigFileBaseDir, "pos_prob_start.json");
        public static string PosProbTransFile => Path.Combine(ConfigFileBaseDir, "pos_prob_trans.json");
        public static string PosProbEmitFile => Path.Combine(ConfigFileBaseDir, "pos_prob_emit.json");
        public static string CharStateTabFile => Path.Combine(ConfigFileBaseDir, "char_state_tab.json");
        public static string IdfFile => Path.Combine(ConfigFileBaseDir, "idf.txt");
        public static string StopWordsFile => Path.Combine(ConfigFileBaseDir, "stopwords.txt");

        /// <summary>Opens a built-in resource or an explicitly supplied file. The caller owns the stream.</summary>
        public static Stream OpenRead(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            lock (Gate)
            {
                var defaultPath = Path.Combine(AppContext.BaseDirectory, "Resources", Path.GetFileName(fullPath));
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (_externalDirectory == null && BuiltInNames.Contains(Path.GetFileName(fullPath))
                    && string.Equals(fullPath, defaultPath, comparison))
                {
                    var resource = typeof(ConfigManager).Assembly.GetManifestResourceStream("JiebaNet.Resources." + Path.GetFileName(fullPath))
                        ?? throw new FileNotFoundException("The built-in jieba.NET resource is missing.", path);
                    _initialized = true;
                    return resource;
                }
                var stream = File.OpenRead(fullPath);
                _initialized = true;
                return stream;
            }
        }

        public static IEnumerable<string> ReadLines(string path)
        {
            using var reader = new StreamReader(OpenRead(path), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            while (reader.ReadLine() is { } line)
                yield return line;
        }
    }
}
