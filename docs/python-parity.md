# 与官方 Python jieba 的对照

核查日期：2026-10-24。参考源为 [fxsjy/jieba](https://github.com/fxsjy/jieba/tree/67fa2e36e72f69d9134b8a1037b83fbb070b9775)，版本 `0.42.1`，固定 commit `67fa2e36e72f69d9134b8a1037b83fbb070b9775`。本分支原始 C# 版本号是 `0.42.2`，两个项目版本号不是同一套编号。

## 原来的测试能说明什么

原 C# 固定输入文件 `jieba_test.txt` 有 41 行，官方 `test/test.py` 有 89 个字面量 `cuttest` 调用，其中与 C# 输入完全相同的句子有 35 条。原来还有若干其他单元测试，但相当一部分只打印结果。不能把原有测试通过理解为“运行了完整官方 Python 测试集”，也不能用这些证据评价作者个人能力。

主词典 349,046 行、IDF 语料 270,132 行与官方文件逐行一致；两份普通 HMM JSON、四份 POS JSON（含字符状态表）转换成相同键结构后，与 Python 模块数据逐项一致。生成脚本验证这些关系并在参考文件元数据中记录 SHA-256。

## 新增固定参考

`Cases/python-parity.json` 由未修改的官方 Python 源码直接生成，不由 C# 的输出反推。包含：

- 六份官方测试脚本中的字面量测试输入，以及原 C# 固定输入。
- 媒体文件名、空串、空白、emoji、补充平面汉字、混合语言等边界样本。
- 固定随机种子 `20261024`：256 个词典词组合文件名、128 个罕见字压力样本。

去重后 499 条输入。每条比较 9 项：精确模式 HMM 开/关、全模式、搜索模式 HMM 开/关、POS HMM 开/关、TF-IDF 权重和 TextRank 权重，共 4,491 项操作。token 和顺序严格比较，不删除空词、不合并标点、不归一化结果；浮点权重仅允许相对误差 `1e-10` / 绝对误差 `1e-12`。

为比较算法，Python 与 C# 使用同一份 C# 内置停用词，TextRank 显式使用 `ns/n/vn/v`。本分支保留原 C# 的中文停用词与较宽的默认 TextRank 词性集合，**默认配置不保证与 Python 原生默认配置相同**。

## 对照结果

使用同一个导出程序，原 C# commit `c12ce441449350874a84d252db4e9b04ec5f3377` 出现 715 项差异；改造后的版本在这 4,491 项中没有差异。原版各模式差异数如下：

| 模式 | 原版与 Python 不同的输入数 | 改造后 |
|---|---:|---:|
| 全模式 | 302 | 0 |
| 精确模式 HMM 开 | 97 | 0 |
| 精确模式 HMM 关 | 2 | 0 |
| 搜索模式 HMM 开 | 97 | 0 |
| 搜索模式 HMM 关 | 2 | 0 |
| POS HMM 开 | 19 | 0 |
| POS HMM 关 | 2 | 0 |
| TF-IDF | 95 | 0 |
| TextRank | 99 | 0 |

715 不是独立 bug 数量，也不是实际中文语料的错误率。罕见字压力样本会反复触发同一个平局选择问题，搜索和 TF-IDF 也会继承普通分词差异。

确认并修复的原因包括：

- 普通 HMM 平局时按遍历顺序选择，而非 Python 的状态字典序；终止状态平局也选错。
- DAG 最佳路径在相同概率下没有按 Python 的规则选较长终点。
- 全模式沿用旧边界，丢弃标点、返回空片段，且英文连续段处理未跟上参考版本。
- 使用 UTF-16 `char` 逐个切开补充字符；TF-IDF 又把单个补充字符误认为两个字符的词。
- POS 对英文数字串使用非前缀正则匹配，部分已知字符的词性回退成 `x`；POS 状态比较用了文化排序。
- TextRank 迭代节点按当前文化排序，而 Python 按码点顺序排序，改变了迭代结果和权重。

旧的 `cut_all.txt` 仅由 Python 参考重新生成，以反映参考版本的正确全模式行为；其他旧期望结果没有为迁就新实现而放宽。新参考的每条结果都由 NUnit 自动断言。

## 重现

```powershell
git clone https://github.com/fxsjy/jieba.git artifacts/python-jieba
git -C artifacts/python-jieba checkout 67fa2e36e72f69d9134b8a1037b83fbb070b9775
python scripts/generate-python-parity.py artifacts/python-jieba
dotnet test test/Segmenter.Tests/Segmenter.Tests.csproj -c Release --filter FullyQualifiedName~PythonParityTests
```

生成脚本拒绝非指定 revision 或有修改的 Python checkout，并校验模型和词典。一般构建/CI 只使用已提交的固定参考，不需要联网或安装 Python。

需要导出结果时：

```powershell
dotnet run --project tools/Parity -c Release -- test/Segmenter.Tests/Cases/python-parity.json artifacts/python-parity-modern.json
python scripts/compare-python-parity.py artifacts/python-parity-modern.json
```

## 验证范围

这是一套跨实现一致性测试，不是语言学正确率评测。没有覆盖所有 Python 功能、自定义词典操作序列、Paddle 或所有 Unicode 序列。上游 issue 的自定义词典、删除词、并发和 KeywordProcessor 等场景另有具名测试。

`Tokenize` 的 C# 下标保持 UTF-16、EndIndex 为 exclusive，不能直接等同于 Python 的码点下标。核心 Cut 保留原 C# 对含 `·` / `-` 自定义词的扩展；POS 默认边界与 Python 参考一致。原 C# 的 `AddWord(freq <= 0)` 自动估算词频约定仍保留，显式删除请使用 `DeleteWord`。
