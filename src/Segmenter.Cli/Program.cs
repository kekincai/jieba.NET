using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JiebaNet.Segmenter.PosSeg;

namespace JiebaNet.Segmenter.Cli
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                string file = null, delimiter = "/ ", userDictionary = null, resources = null;
                bool cutAll = false, noHmm = false, pos = false;
                for (var i = 0; i < args.Length; i++)
                {
                    var argument = args[i];
                    var equals = argument.StartsWith("--", StringComparison.Ordinal) ? argument.IndexOf('=') : -1;
                    var option = equals >= 0 ? argument.Substring(0, equals) : argument;
                    string Value()
                    {
                        if (equals >= 0) return argument.Substring(equals + 1);
                        if (++i >= args.Length) throw new ArgumentException($"Missing value for {option}.");
                        return args[i];
                    }
                    if (equals >= 0 && option is not ("--file" or "--delimiter" or "--user-dict" or "--resources"))
                        throw new ArgumentException($"Unknown option: {argument}");
                    switch (option)
                    {
                        case "-h": case "--help": Console.WriteLine(Usage); return 0;
                        case "-v": case "--version":
                            var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                            Console.WriteLine($"jieba.NET {version}"); return 0;
                        case "-f": case "--file": file = Value(); break;
                        case "-d": case "--delimiter": delimiter = Value(); break;
                        case "-u": case "--user-dict": userDictionary = Value(); break;
                        case "--resources": resources = Value(); break;
                        case "-a": case "--cut-all": cutAll = true; break;
                        case "-n": case "--no-hmm": noHmm = true; break;
                        case "-p": case "--pos": pos = true; break;
                        default: throw new ArgumentException($"Unknown option: {argument}");
                    }
                }
                if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Specify an input file with --file.");
                if (resources != null) ConfigManager.ConfigFileBaseDir = resources;
                var segmenter = new JiebaSegmenter();
                if (userDictionary != null) segmenter.LoadUserDict(userDictionary);
                var posSegmenter = pos ? new PosSegmenter(segmenter) : null;
                foreach (var line in File.ReadLines(file, Encoding.UTF8))
                {
                    var words = pos
                        ? posSegmenter.Cut(line, hmm: !noHmm).Select(token => $"{token.Word}/{token.Flag}")
                        : segmenter.Cut(line, cutAll: cutAll, hmm: !noHmm);
                    Console.WriteLine(string.Join(delimiter, words));
                }
                return 0;
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or System.Text.Json.JsonException or TypeInitializationException or InvalidOperationException)
            {
                Console.Error.WriteLine(exception.GetBaseException().Message);
                return 1;
            }
        }

        private const string Usage = """
            jieba.NET — Chinese text segmentation for .NET 10
            -f, --file PATH        UTF-8 input file (required)
            -d, --delimiter TEXT   Token separator (default: / )
            -a, --cut-all          Full segmentation mode
            -n, --no-hmm           Disable the hidden Markov model
            -p, --pos              Emit word/part-of-speech pairs
            -u, --user-dict PATH   Load a user dictionary
                --resources DIR   Use an external resource directory
            -v, --version         Show version
            -h, --help            Show help
            """;
    }
}
