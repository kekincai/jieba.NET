using System.Collections.Generic;
using System.Globalization;
using JiebaNet.Segmenter;
using System.IO;
using System.Linq;
using System.Text;

namespace JiebaNet.Analyser
{
    public class IdfLoader
    {
        internal string IdfFilePath { get; set; }
        internal IDictionary<string, double> IdfFreq { get; set; }
        internal double MedianIdf { get; set; }

        public IdfLoader(string idfPath = null)
        {
            IdfFilePath = string.Empty;
            IdfFreq = new Dictionary<string, double>();
            MedianIdf = 0.0;
            if (!string.IsNullOrWhiteSpace(idfPath))
            {
                SetNewPath(idfPath);
            }
        }

        public void SetNewPath(string newIdfPath)
        {
            var idfPath = Path.GetFullPath(newIdfPath);
            if (IdfFilePath != idfPath)
            {
                var frequencies = new Dictionary<string, double>(System.StringComparer.Ordinal);
                var lineNumber = 0;
                foreach (var line in ConfigManager.ReadLines(idfPath))
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var frequency)
                        || !double.IsFinite(frequency) || frequency < 0)
                        throw new InvalidDataException($"Invalid IDF entry at line {lineNumber}.");
                    frequencies[parts[0]] = frequency;
                }
                if (frequencies.Count == 0) throw new InvalidDataException("The IDF dictionary is empty.");
                var median = frequencies.Values.OrderBy(value => value).ElementAt(frequencies.Count / 2);
                IdfFreq = frequencies;
                MedianIdf = median;
                IdfFilePath = idfPath;
            }
        }
    }
}