# 上游 issue 对照

检查日期：2026-10-24。来源：[anderscui/jieba.NET issues](https://github.com/anderscui/jieba.NET/issues)。读取 90 条 issue（含已关闭历史问题），并检查关键 issue 的讨论及 #87 的截图。仅在本分支修复/验证，没有向上游发评论或关闭 issue。

本表区分：本次确认修复、原版已有修复、现有用法/设计边界、功能请求、缺少复现信息。并非把每条 issue 都当作 bug。

独立词典是显式选择，默认构造函数仍共享词典。独立模式会多占一份词典内存，详见 README。#87 的复现输入来自截图，不是根据标题猜测。

| Issue | 原标题 | 本分支处理 | 证据/边界 |
|---|---|---|---|
| [#98](https://github.com/anderscui/jieba.NET/issues/98) | 带有表情的字符被分词后，表情的unicode字符对被拆分开了，导致无法完整的统计一个表情，怎么样能让表情不会被拆分开呢？ | 已修复 | Rune 保留代理对；Issue98 + PythonParity |
| [#97](https://github.com/anderscui/jieba.NET/issues/97) | 在net core上使用，多个词典加载后累加的问题。 | 已提供独立模式 | WordDictionary.Create；Issues91_97；默认仍共享，见 README 内存说明 |
| [#96](https://github.com/anderscui/jieba.NET/issues/96) | WordDictionary LoadDict Invalid line | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#95](https://github.com/anderscui/jieba.NET/issues/95) | System.Text.Json 代替 Newtonsoft.Json | 已实现 | 使用 System.Text.Json，内置 char 键转换器和深度限制 |
| [#94](https://github.com/anderscui/jieba.NET/issues/94) | JiebaNet.Segmenter是不是没用到停用词 | 用法说明 | Cut 不移除停用词，提取器/应用层负责；与 Python 一致 |
| [#93](https://github.com/anderscui/jieba.NET/issues/93) | 有Net Core版本的使用示例吗 | 文档更新 | README 提供 .NET 10 项目引用和调用示例 |
| [#91](https://github.com/anderscui/jieba.NET/issues/91) | 不同分词实例加载不同字典怎么实现 | 已提供独立模式 | 同 #97，用户词、标签和文件加载记录均隔离 |
| [#89](https://github.com/anderscui/jieba.NET/issues/89) | 同义支持 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#88](https://github.com/anderscui/jieba.NET/issues/88) | highlighter返回结果问题 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#87](https://github.com/anderscui/jieba.NET/issues/87) | KeywordProcessor提词不准确 | 已复现并修复 | 按 issue 截图：从前有座从前山；原版漏有座；最长匹配和相邻词回归 |
| [#86](https://github.com/anderscui/jieba.NET/issues/86) | 想邀请您的项目在 Gitee 同步代码并加入 dotNET China 组织（非微软官方） | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#85](https://github.com/anderscui/jieba.NET/issues/85) | 词性分析时调用自定义词典的方法 | 已修复并说明 | PosSegmenter(segmenter)；共享同一词典标签，Issues52_57_85 |
| [#84](https://github.com/anderscui/jieba.NET/issues/84) | AddWord | 用法说明 | AddWord 只改内存，不修改词典文件；标点不属于任意可识别词 |
| [#83](https://github.com/anderscui/jieba.NET/issues/83) | 建议支持日期时间格式，不必拆分成为几个独立的数字，建议日期时间为完整的词 | 功能请求，未纳入 | 日期整体识别属于业务分词规则，不改变 Python 默认行为 |
| [#82](https://github.com/anderscui/jieba.NET/issues/82) | 今天4:50某某某领了一只记号笔中的4:50为什么不能识别成时间？？？ | 模型/功能请求 | 时间实体识别不在本次范围 |
| [#81](https://github.com/anderscui/jieba.NET/issues/81) | newtonsoft.json.12.0.3 版本能不能降低成4.几？谢谢 | 已移除依赖 | 不再依赖 Newtonsoft，无需降级 |
| [#80](https://github.com/anderscui/jieba.NET/issues/80) | 请问支不支持VS2013 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#79](https://github.com/anderscui/jieba.NET/issues/79) | 自定义字典问题 | 已修复 | 加载成功后登记 LoadedPaths，重复调用跳过；Issue79 |
| [#78](https://github.com/anderscui/jieba.NET/issues/78) | How to config the ConfigFileBaseDir in Abp framework | 部署方式改进 | 默认内嵌资源；显式目录仍支持，包和单文件烟测 |
| [#77](https://github.com/anderscui/jieba.NET/issues/77) | 自定义字典中读取中文乱码导致不能匹配 | 编码验证增强/说明 | UTF-8/BOM 回归，错误编码明确失败；相对用户词典路径仍相对 CWD |
| [#76](https://github.com/anderscui/jieba.NET/issues/76) | .Net Core 3.1  LoadUserDict 下，會出現 Unhandled exception. System.UnauthorizedAccessException: Access to the path  | 路径用法 | LoadUserDict 参数应是文件，不是文件夹；保留真实读取错误 |
| [#75](https://github.com/anderscui/jieba.NET/issues/75) | 请问自定义词库的方法可以多次调用么，还是说只能调用一次，多个自定义词库文件该如何处理呢？ | 已覆盖 | 多个文件可累加，同一词典对象每个路径只加载一次 |
| [#74](https://github.com/anderscui/jieba.NET/issues/74) | 如何启用 停用词库，感觉默认的好像没有去除停用词 | 用法说明 | 与 #94 相同；README 说明停用词用途 |
| [#73](https://github.com/anderscui/jieba.NET/issues/73) | 请问一下，是不是理论上来说，我有不同语言的字典就可以直接支持相关语言呢？ | 功能边界 | 不是添加日/韩词典即可获得对应形态分析模型 |
| [#71](https://github.com/anderscui/jieba.NET/issues/71) | 签名的程序集中调用提示需要强名称程序集 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#70](https://github.com/anderscui/jieba.NET/issues/70) | 如果可以，我想加入开发组 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#69](https://github.com/anderscui/jieba.NET/issues/69) | BUG:Tokenize方法返回的EndIndex比实际下标多1 | 不是下标 bug | EndIndex 是 exclusive；Issues32_69 验证 Substring(start,end-start) |
| [#68](https://github.com/anderscui/jieba.NET/issues/68) | JiebaSegmenter.Tokenize方法能不能增加词性返回 | 功能请求，未纳入 | 暂未为 Token 增加词性字段 |
| [#67](https://github.com/anderscui/jieba.NET/issues/67) | 封装与MATLAB使用 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#66](https://github.com/anderscui/jieba.NET/issues/66) | 自定义分词问题 | 模式/频率选择 | 精确模式选择最佳路径，不保证输出全部前缀；全模式用于枚举候选 |
| [#65](https://github.com/anderscui/jieba.NET/issues/65) | 在UWP中由于无法使用windows文件路径造成无法使用 | 当前目标之外 | 内嵌资源减少文件路径依赖，但未声称 UWP 支持 |
| [#64](https://github.com/anderscui/jieba.NET/issues/64) | 如何拆分数字，例如将53拆分为5,3,53三个词 | 功能请求，未纳入 | 数字分字与 n-gram 不是默认模式 |
| [#63](https://github.com/anderscui/jieba.NET/issues/63) | 是否支持同义词替换 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#62](https://github.com/anderscui/jieba.NET/issues/62) | 这个分词等的来源都来自文件,我可否添加数据库支持 | 功能请求，未纳入 | 未引入数据库/ORM 依赖 |
| [#61](https://github.com/anderscui/jieba.NET/issues/61) | 如何支持品牌名称？ | 用法说明 | 品牌通过用户词典/AddWord 添加，不改变词典默认内容 |
| [#60](https://github.com/anderscui/jieba.NET/issues/60) | 小白求问：WordWeightPair如何转换为String | 用法说明 | 显式选择 Word/Weight，README 示例 |
| [#59](https://github.com/anderscui/jieba.NET/issues/59) | 词库里加入英文分词后，在使用分词的时候，能不能忽略大小写 | 功能请求，局部覆盖 | KeywordProcessor 支持 invariant 忽略大小写；核心词典保持大小写敏感 |
| [#58](https://github.com/anderscui/jieba.NET/issues/58) | 关于词典路径在 .netcore 环境的问题 | 部署方式改进 | 内嵌资源，不再依赖 app.config；外部目录必须在初始化前设置 |
| [#57](https://github.com/anderscui/jieba.NET/issues/57) | 自定义词库的问题 | 已覆盖 | 自定义标签经 PosSegmenter(segmenter) 生效；Issues52_57_85 |
| [#56](https://github.com/anderscui/jieba.NET/issues/56) | 是否可以多线程调用? | 并发增强 | 读写锁保护词典，单次分词一致；Modernization 并发测试 |
| [#55](https://github.com/anderscui/jieba.NET/issues/55) | 自定义词典路径的正确设置方法是什么？ | 已覆盖/编码增强 | 按原文二号谈话间/一号谈话间验证 UTF-8/BOM |
| [#54](https://github.com/anderscui/jieba.NET/issues/54) | 关于SharpDevelop调用jieba的DLL库问题 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#53](https://github.com/anderscui/jieba.NET/issues/53) | 关于Jieba.net引用MVC框架时候出错的问题 | 依赖冲突来源已移除 | 现代包不再加载 Newtonsoft；旧 MVC 运行环境未验证 |
| [#52](https://github.com/anderscui/jieba.NET/issues/52) | 自定义词典下的posSeg.Cut("北京市海淀区",true);结果不正确 | 已修复 | WordDictionary.AddWord 的标签进入同一词典；city/town 具名测试 |
| [#51](https://github.com/anderscui/jieba.NET/issues/51) | 无法使用Counter来统计词频率 | 用法说明 | Counter 位于 JiebaNet.Segmenter.Common |
| [#50](https://github.com/anderscui/jieba.NET/issues/50) | 初次接触，配置有点问题 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#49](https://github.com/anderscui/jieba.NET/issues/49) | 分词功能.Cut()出错 | 原版已修复，回归保留 | TestIssue49 + PythonParity |
| [#46](https://github.com/anderscui/jieba.NET/issues/46) | 关于分词 我在词典中删除了 “天半” 但是分词还是在一起 | 已修复 | 显式删除词阻止 HMM 再合成；Issue46 测 2天半 |
| [#45](https://github.com/anderscui/jieba.NET/issues/45) | 源码生成报错 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#44](https://github.com/anderscui/jieba.NET/issues/44) | PosSegmenter如何添加自定义词典 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#43](https://github.com/anderscui/jieba.NET/issues/43) | 对类似“艾尔肯·吐尼亚孜”切词，即使添加了自定义词典，也无法把这个人名切出来  | 原版已修复，保留扩展 | 核心 Cut 支持自定义含 · 人名；TestChineseDot |
| [#42](https://github.com/anderscui/jieba.NET/issues/42) | 无法识别混合有符号、字母、汉字的词？ | 原版已修复，保留扩展 | 核心 Cut 支持短P-R间期；TestChineseDot |
| [#41](https://github.com/anderscui/jieba.NET/issues/41) | 大神，JiebaNet.Segmenter.JiebaSegmenter的类型初始值错误，到底是哪方面的错误。 | 默认部署风险减少 | 内嵌模型；缺失/损坏外部模型失败烟测，不能回溯确认旧环境 |
| [#40](https://github.com/anderscui/jieba.NET/issues/40) | 自定义词不起作用? | 用户词典回归覆盖 | 词频影响最佳路径；仅声明当前规范词典可用，不回溯断言原报告原因 |
| [#39](https://github.com/anderscui/jieba.NET/issues/39) | 文件下载，显示不可预料的文件末端，文件不能解压！已经下载3次了！ | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#38](https://github.com/anderscui/jieba.NET/issues/38) | 衷心建议：说明能否写得更明确点，太多的坑啊，大神！ | 文档/部署改进 | 明确 .NET 10、内嵌资源与外部配置步骤 |
| [#37](https://github.com/anderscui/jieba.NET/issues/37) | vs2013、4.5下报错，已经调试2天，无结果！跪求！ | 旧环境未复现 | 新包不再需要复制 Resources/匹配 Newtonsoft；不验证 VS2013 |
| [#36](https://github.com/anderscui/jieba.NET/issues/36) | 我同时想要词性标注和自定义词典有没办法做到的？ | 已覆盖 | 给 PosSegmenter 传入分词器；标签在对应词典统一维护 |
| [#35](https://github.com/anderscui/jieba.NET/issues/35) | 为什么.Net版本的结巴需要4.5的FrameWork这个高版本啊，4.0不好吗？客户端使用4.5太高了 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#34](https://github.com/anderscui/jieba.NET/issues/34) | 关于去除停用词后进行词频统计 | 算法说明 | TF-IDF 权重与原始词频不同，并非排序错误 |
| [#33](https://github.com/anderscui/jieba.NET/issues/33) | Resource path 配置如果加一个code API就好了。 | 已实现/保留 | 显式配置与内嵌资源；初始化后禁止切换以免混用 |
| [#32](https://github.com/anderscui/jieba.NET/issues/32) | Tokenize：返回词语在原文的起止位置有错误 | 原版已修复，扩大回归 | 多空格/英文/emoji 的两种 Tokenize 模式均验证 UTF-16 片段 |
| [#31](https://github.com/anderscui/jieba.NET/issues/31) | 请问关键词提取如何限定仅提取某些词 | 功能边界 | IDF 调权重不是白名单；已知词提取可用 KeywordProcessor |
| [#30](https://github.com/anderscui/jieba.NET/issues/30) | textrank allowpos 的功能 | 原版已有实现，扩大回归 | Issue30 证明 n/v 过滤结果不同 |
| [#29](https://github.com/anderscui/jieba.NET/issues/29) | 请问有没有计算词频的功能？怎么调用 | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#27](https://github.com/anderscui/jieba.NET/issues/27) | Nuget下载安装后，Tfidf使用报错 | 部署方式改进 | 包消费测试覆盖 TF-IDF；旧 ASP.NET 4.5 环境未验证 |
| [#26](https://github.com/anderscui/jieba.NET/issues/26) | Please specify fileaccess.read when load dict | 原版已有修复，保留 | File.OpenRead/只读 StreamReader；不写词典文件 |
| [#25](https://github.com/anderscui/jieba.NET/issues/25) | 问题 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#24](https://github.com/anderscui/jieba.NET/issues/24) | “当药”在词性中被分割成“当”和“药”，而在精确模式下是“当药” | 证据不足 | 无完整句子；词性路径现共用词典，但未声称重现此历史问题 |
| [#23](https://github.com/anderscui/jieba.NET/issues/23) | 哈工大同义词词典有使用上吗？ | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#22](https://github.com/anderscui/jieba.NET/issues/22) | 搜索引擎模式無法使用 | 词典/语言差异 | 繁体和简体词频不同，不能要求输出相同；未更换默认词典 |
| [#21](https://github.com/anderscui/jieba.NET/issues/21) | 載入自訂義辭典? | 用法/错误处理改进 | 用户词典绝对路径示例，文件错误明确抛出 |
| [#20](https://github.com/anderscui/jieba.NET/issues/20) | 采用.NET 4.0的项目如何用最新版本jieba.NET？ | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#19](https://github.com/anderscui/jieba.NET/issues/19) | 调用DeleteWord和AddWord发现只在程序执行期间有效！！！ | 预期行为 | 内存增删不写文件，与 Python 一致 |
| [#17](https://github.com/anderscui/jieba.NET/issues/17) | uap10的支持问题 | 旧平台/环境，未纳入 | 目标为 .NET 10，不验证历史平台/打包方式 |
| [#16](https://github.com/anderscui/jieba.NET/issues/16) | TfidfExtractor.SetStopWords似乎沒有作用 | 用法说明 | SetStopWords 接受路径；AddStopWord 接受单个词 |
| [#15](https://github.com/anderscui/jieba.NET/issues/15) | TfidfExtractor和TextRankExtractor创建报错。 | 部署方式改进 | 内嵌资源与提取器包消费烟测；旧环境未验证 |
| [#14](https://github.com/anderscui/jieba.NET/issues/14) | “JiebaNet.Segmenter.JiebaSegmenter”的类型初始值设定项引发异常。 | 部署方式改进 | 内嵌资源/显式目录，旧 web.config 配置已移除 |
| [#13](https://github.com/anderscui/jieba.NET/issues/13) | Error in ASP.NET | 旧二进制混用 | 新包明确版本与依赖；未声称旧 Framework DLL 可兼容 |
| [#12](https://github.com/anderscui/jieba.NET/issues/12) | 增加读取摘要的函数吗？ | 功能/集成请求，未纳入 | 不改变默认算法，不扩大到外部搜索引擎、同义词或其他项目 |
| [#10](https://github.com/anderscui/jieba.NET/issues/10) | 我想咨询一下 中文分词有关问题  | 业务搜索策略 | 模式、领域词典和查询字段策略；非通用算法 bug |
| [#9](https://github.com/anderscui/jieba.NET/issues/9) | “JiebaNet.Segmenter.JiebaSegmenter”的类型初始值设定项引发异常 | 部署方式改进 | 默认内嵌资源和烟测，不能回溯确认旧环境 |
| [#8](https://github.com/anderscui/jieba.NET/issues/8) | 词典路径的进一步改进 | 已实现/保留 | 显式目录配置和内嵌资源 |
| [#7](https://github.com/anderscui/jieba.NET/issues/7) | 安装失败 | 依赖来源已移除 | 核心不依赖 Newtonsoft |
| [#6](https://github.com/anderscui/jieba.NET/issues/6) | 给一个案例，会抛出给定关键字不在字典中 | 原版已修复 | 保留 TestNewords；未把历史修复冒充此次修复 |
| [#5](https://github.com/anderscui/jieba.NET/issues/5) | 关于词典路径在asp.net环境的问题 | 部署方式改进 | 内嵌资源避免依赖工作目录 |
| [#4](https://github.com/anderscui/jieba.NET/issues/4) | 包含空格的自定义词分词时会被分割问题 | 算法边界/替代 API | Cut 保持 Python 空格边界，含空格短语用 KeywordProcessor |
| [#3](https://github.com/anderscui/jieba.NET/issues/3) | System.TypeInitializationException  | 部署方式改进 | 默认内嵌资源；旧异常缺完整根因，未声称重现 |
| [#2](https://github.com/anderscui/jieba.NET/issues/2) | 需要拷贝 Resources 到 ./bin/Debug 而非仅仅到项目目录 | 已改善 | 无需复制 Resources；真实包单文件烟测 |
| [#1](https://github.com/anderscui/jieba.NET/issues/1) | will update jieba 0.38? | 版本记录 | 分支以 Python 0.42.1 作为固定对照 |
