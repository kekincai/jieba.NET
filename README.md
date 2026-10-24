# jieba.NET for .NET 10

这是 [anderscui/jieba.NET](https://github.com/anderscui/jieba.NET) 的现代化分支。目标框架为 `net10.0`，保留 `JiebaNet.Segmenter` / `JiebaNet.Analyser` 命名空间和主要 API。

- 核心库与分析库没有第三方运行时包依赖；模型使用内置 `System.Text.Json`。
- 默认词典和模型内嵌到程序集，无需复制 Resources，也无需 app.config/web.config。
- 默认单线程分词；词典、HMM 模型按需初始化，支持共享实例并发读取。
- 提供显式独立词典，避免不同任务的用户词典互相污染。
- 使用固定版本官方 Python jieba 生成的参考结果持续验证算法。

## 构建与验证

安装 .NET 10 SDK，在仓库目录运行：

```powershell
dotnet build jieba.NET.sln -c Release
dotnet test jieba.NET.sln -c Release
./scripts/verify.ps1
```

`verify.ps1` 还会审计传递依赖、生成 NuGet 包，并用真正安装的本地包验证单文件发布、内嵌资源、外部资源、损坏/缺失模型和命令行工具。测试中的两项旧手工性能演示保留为忽略状态；实际性能测试见下文。

包名为 `Kekincai.JiebaNet.Segmenter` 和 `Kekincai.JiebaNet.Analyser`，当前版本 `1.0.0`。生成结果位于 `artifacts/packages`，尚未发布到 NuGet.org。项目引用也可直接使用：

```xml
<ProjectReference Include="PATH/TO/jieba.NET/src/Segmenter/Segmenter.csproj" />
```

需要 TF-IDF/TextRank 时再引用 Analyser 项目或包。

应用接入、仓库内本地包源、GitHub Actions 和以后发布到 NuGet.org 的完整步骤见 [应用与 CI 接入说明](docs/consuming-packages.md)。

## 分词

```csharp
using JiebaNet.Segmenter;

var segmenter = new JiebaSegmenter();
var words = segmenter.Cut("我来到北京清华大学");
// 我 / 来到 / 北京 / 清华大学
var searchWords = segmenter.CutForSearch("清华大学");
var allWords = segmenter.Cut("北京清华大学", cutAll: true);
var withoutHmm = segmenter.Cut("北京旅行合影.jpg", hmm: false);
```

精确模式适合词云、推荐和词语统计；搜索模式会额外输出词典中存在的短词，适合提升搜索召回率。`Cut` 保留空格和标点，不自动移除停用词；停用词属于关键词提取和应用业务过滤。

`Tokenize` 返回的 `StartIndex` / `EndIndex` 是 **UTF-16 下标、左闭右开区间**，可以直接用于 C# 的 `text.Substring(start, end - start)`。emoji 和补充平面字符保持完整。Python 的下标以 Unicode 码点计数，含补充字符时数字不能直接混用。

## 用户词典、词性与实例隔离

```csharp
segmenter.LoadUserDict(Path.Combine(AppContext.BaseDirectory, "user_dict.txt"));
segmenter.AddWord("机器学习", tag: "n");
segmenter.DeleteWord("台中"); // 也阻止普通分词的 HMM 再次把它合并

var pos = new JiebaNet.Segmenter.PosSeg.PosSegmenter(segmenter);
var tagged = pos.Cut("机器学习");
```

用户词典支持 UTF-8（含或不含 BOM），一行一个词，格式为 `词语 [词频] [词性]`，例如 `机器学习 100 n`、`云计算`、`凱特琳 nz`。无效 UTF-8、溢出的词频和文件读取失败会抛出异常。主词典与用户词典不同：主词典必须提供正整数词频。

相对用户词典路径相对于当前工作目录解析；`ConfigFileBaseDir` 不改变这个语义，建议总是显式组合绝对路径。同一个词典对象对同一文件只加载一次；修正一次加载失败的文件后可以重试。`AddWord`、`DeleteWord` 只修改内存，不回写文件。

默认的 `new JiebaSegmenter()` 延续原版的进程级共享词典。不同词典需要显式创建：

```csharp
var first = new JiebaSegmenter(WordDictionary.Create());
var second = new JiebaSegmenter(WordDictionary.Create());
first.LoadUserDict("first.txt");
second.LoadUserDict("second.txt");
// 两者增删词、词性与用户词典加载记录相互隔离。
// 也可 WordDictionary.Create("/absolute/path/to/main-dict.txt") 指定独立主词典。
```

独立词典拥有自己的 Trie，因而需要额外词典内存；HMM 模型仍共享。**服务中建议注册并复用一个分词器实例**，在启动时加载用户词典，避免为每个文件或请求创建独立词典。`CutInParallel` 等并行 API 是显式选择；普通 `Cut` 不启动并行任务。增删词与读取使用读写锁，单次分词不会读到一半更新的词典。

包含空格的短语或忽略大小写的关键词匹配可用 `KeywordProcessor`，不改变中文分词算法的默认边界规则：

```csharp
var keywords = new KeywordProcessor();
keywords.AddKeywords(new[] { ".NET Core", "Mac OS X", "从前", "有座", "从前山" });
var matches = keywords.ExtractKeywords("从前有座从前山");
// 从前 / 有座 / 从前山；采用最长、非重叠匹配。
```

## 关键词提取

```csharp
using JiebaNet.Analyser;
var tfidf = new TfidfExtractor(segmenter);
var tags = tfidf.ExtractTagsWithWeight("北京清华大学自然语言处理研究");
var rank = new TextRankExtractor(segmenter);
var nouns = rank.ExtractTags("北京清华大学自然语言处理研究", allowPos: new[] { "n", "ns" });
```

`SetStopWords(path)` 替换停用词集合，`AddStopWord(word)` 添加单个停用词。`SetIdfPath(path)` 替换 IDF 数据。配置更新失败会保留先前有效的数据；建议在开始提供服务之前完成这些配置。

本分支保留原 C# 版的中文停用词，以及较宽的 TextRank 默认词性集合；与 Python 对照时显式统一停用词和词性过滤条件。TF-IDF 权重不是单纯的词频，不能把其排序当作 Counter 的词频排序。

## 外部默认资源

通常不需要设置。如果确实要替换默认模型，在任何资源首次读取之前设置：

```csharp
ConfigManager.ConfigFileBaseDir = Path.Combine(AppContext.BaseDirectory, "jieba-data");
```

显式目录必须包含所用功能需要的资源，缺失时明确失败；不会悄悄退回内嵌模型。初始化后更改目录会抛出异常，防止词典和模型混用。原来的 app.config/web.config 配置项已移除。路径属性默认表示资源的逻辑名称；读取内嵌资源使用 `ConfigManager.OpenRead` 或 `ReadLines`，不能假定磁盘上存在同名文件。

## 命令行与性能测量

```powershell
dotnet run --project src/Segmenter.Cli -c Release -- --file input.txt
dotnet run --project src/Segmenter.Cli -c Release -- --file input.txt --no-hmm --delimiter '|'
./scripts/benchmark.ps1
```

命令行逐行读取，正常退出后不等待回车；错误输出到 stderr，参数错误返回 2，运行/文件错误返回 1。`--help` 显示全部选项。

性能工具使用固定的 16 个媒体文件名，默认每轮 20,000 次单线程调用，5 轮取中位数，每种模式默认启动 3 个独立进程；先预热 50,000 次并等待 1 秒。性能报告使用每轮 100,000 次的长采样。冷调用、初始化后托管内存、工作集、吞吐和每文件分配量分别记录。见 [迁移与验证记录](docs/modernization.md)、[Python 对照](docs/python-parity.md) 和 [上游 issue 对照](docs/upstream-issues.md)。

本分支不支持旧 .NET Framework/.NET Core 2.x，也不声称支持 Native AOT、裁剪、日语/韩语形态分析或 Paddle 模式。默认模型内嵌已验证适用于普通和非裁剪单文件发布。

## 致谢与授权

原 C# 实现来自 anderscui/jieba.NET，算法和词典来自 fxsjy/jieba。保留原 MIT LICENSE；本次改造不改变其授权要求。
