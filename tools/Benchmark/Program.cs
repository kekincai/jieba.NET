using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JiebaNet.Analyser;
using JiebaNet.Segmenter;
using JiebaNet.Segmenter.PosSeg;

// Deliberately single-threaded: file-name analysis should leave room for the host application.
var mode = args.Length > 0 ? args[0] : "cut";
var operations = args.Length > 1 ? int.Parse(args[1]) : 20_000;
var warmup = args.Length > 2 ? int.Parse(args[2]) : 50_000;
var settleMs = args.Length > 3 ? int.Parse(args[3]) : 1000;
if (operations <= 0) throw new ArgumentOutOfRangeException(nameof(operations));
string[] names = [
    "北京颐和园春游合影20260418.jpg", "京都和服樱花旅行记录.mp4",
    "上海外滩夜景人像摄影.jpg", "自然语言处理与机器学习课程.mp4",
    "IMG_20260418_123456.jpg", "DSC_012345.JPG", "family-beach-holiday-2026.mp4",
    "重庆火锅美食探店1080p.mp4", "成都大熊猫繁育研究基地.jpg",
    "东京旅行と桜の写真.jpg", "海边日落_朋友聚会_4k.mp4", "周末健身力量训练教学.mp4",
    "清华大学计算机科学与技术.jpg", "猫咪睡觉可爱宠物日常.mp4",
    "微信图片_20261024123000.jpg", "2026-10-24_旅行计划（最终版）.png"
];

var cold = Stopwatch.StartNew();
var run = Create(mode);
var firstCount = run(names[0]);
cold.Stop();
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
var managedAfterInit = GC.GetTotalMemory(false);
var workingSetAfterInit = Process.GetCurrentProcess().WorkingSet64;
for (var i = 0; i < warmup; i++) run(names[i % names.Length]);
// Let tiered JIT work triggered during warm-up settle before measuring sustained calls.
Thread.Sleep(settleMs);
var timings = new List<double>();
var cpuTimings = new List<double>();
var allocations = new List<double>();
using var process = Process.GetCurrentProcess();
long checksum = firstCount;
for (var round = 0; round < 5; round++)
{
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var cpuBefore = process.TotalProcessorTime;
    var watch = Stopwatch.StartNew();
    for (var i = 0; i < operations; i++) checksum += run(names[i % names.Length]);
    watch.Stop();
    timings.Add(watch.Elapsed.TotalMilliseconds);
    cpuTimings.Add((process.TotalProcessorTime - cpuBefore).TotalMilliseconds);
    allocations.Add((GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)operations);
}
timings.Sort(); cpuTimings.Sort(); allocations.Sort();
Console.WriteLine(JsonSerializer.Serialize(new
{
    Mode = mode, Runtime = Environment.Version.ToString(), CpuCount = Environment.ProcessorCount,
    OperationsPerRound = operations, Rounds = 5, WarmupCalls = warmup, SettleMs = settleMs,
    ColdFirstCallMs = Math.Round(cold.Elapsed.TotalMilliseconds, 2),
    ManagedAfterInitMiB = Math.Round(managedAfterInit / 1048576d, 2),
    WorkingSetAfterInitMiB = Math.Round(workingSetAfterInit / 1048576d, 2),
    MedianMs = Math.Round(timings[2], 2),
    MedianCpuMs = Math.Round(cpuTimings[2], 2),
    NamesPerSecond = Math.Round(operations * 1000d / timings[2]),
    AllocatedBytesPerName = Math.Round(allocations[2]), Checksum = checksum
}));

[MethodImpl(MethodImplOptions.NoInlining)]
static Func<string, int> Create(string mode)
{
    if (mode == "tfidf")
    {
        var extractor = new TfidfExtractor();
        return text => extractor.ExtractTags(text).Count();
    }
    if (mode == "textrank")
    {
        var extractor = new TextRankExtractor();
        return text => extractor.ExtractTags(text).Count();
    }
    if (mode == "pos")
    {
        var pos = new PosSegmenter();
        return text => pos.Cut(text).Count();
    }
    var segmenter = new JiebaSegmenter();
    return mode switch
    {
        "cut" => text => segmenter.Cut(text).Count(),
        "no-hmm" => text => segmenter.Cut(text, hmm: false).Count(),
        "search" => text => segmenter.CutForSearch(text).Count(),
        _ => throw new ArgumentException("Use cut, no-hmm, search, pos, tfidf or textrank.")
    };
}
