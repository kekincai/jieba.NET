# 在应用项目和 GitHub Actions 中使用

普通分词引用 `Kekincai.JiebaNet.Segmenter`；需要 TF-IDF/TextRank 时引用 `Kekincai.JiebaNet.Analyser`，它会自动依赖 Segmenter。当前版本是 `1.0.0`，两个包均面向 .NET 10。

**当前可以复制 `.nupkg` 到应用仓库作为本地 NuGet 源，并通过 PackageReference 引用。** 不需要复制源码、解压包或手工引用 DLL。以后公开发布到 NuGet.org，就不必把包放进应用仓库。

## 当前可直接使用：仓库内本地源

以下操作都在“使用本库的应用仓库”里进行，不是在本库仓库中。

### 1. 复制生成的包

本库执行 `./scripts/verify.ps1` 后，在 `artifacts/packages` 中生成：

```text
Kekincai.JiebaNet.Segmenter.1.0.0.nupkg
Kekincai.JiebaNet.Analyser.1.0.0.nupkg
```

在应用仓库根目录建立 `vendor/nuget`，复制所需包，目录应类似：

```text
MyApp/
├── NuGet.Config
├── .gitignore
├── .github/workflows/ci.yml
├── vendor/nuget/
│   ├── Kekincai.JiebaNet.Segmenter.1.0.0.nupkg
│   └── Kekincai.JiebaNet.Analyser.1.0.0.nupkg
└── src/MyApp.Server/MyApp.Server.csproj
```

只做分词时仅需 Segmenter 包；使用 Analyser 时两个包都要放进本地源。不要把包放在 `bin`、`obj` 或被忽略的构建产物目录中。

PowerShell 示例，在应用仓库根目录执行，先修改第一行路径：

```powershell
$jiebaRoot = 'C:\path\to\jieba.NET'
New-Item -ItemType Directory -Force -Path vendor/nuget | Out-Null
Copy-Item -LiteralPath (Join-Path $jiebaRoot 'artifacts/packages/Kekincai.JiebaNet.Segmenter.1.0.0.nupkg') -Destination vendor/nuget/
# 需要分析器时，再复制 Kekincai.JiebaNet.Analyser.1.0.0.nupkg。
```

### 2. 配置仓库根目录的 NuGet.Config

新建以下文件；如果已经存在，合并源和映射，不要覆盖其他必要配置：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="repo-packages" value="vendor/nuget" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="repo-packages">
      <package pattern="Kekincai.JiebaNet.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

路径相对于这个配置文件解析，因而本地 Windows 和 GitHub runner 都能使用。专用前缀比 `*` 更具体，因此本库及其分析器依赖从仓库本地源取得，其他依赖从 NuGet.org 取得。已有私有源的项目需要把它们及对应映射一并保留。

不要把本地 `C:\...` 路径写成 CI 包源，也不要依赖当前电脑全局配置里的本地源。详见 [本地源](https://learn.microsoft.com/en-us/nuget/hosting-packages/local-feeds) 和 [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)。

### 3. 添加 PackageReference

在应用 `.csproj` 现有 ItemGroup 中添加，或新建一个 ItemGroup：

```xml
<ItemGroup>
  <PackageReference Include="Kekincai.JiebaNet.Segmenter" Version="1.0.0" />
</ItemGroup>
```

需要分析器时可改为引用 `Kekincai.JiebaNet.Analyser`，无需再显式引用 Segmenter；不过仓库内本地源仍需包含两个包。

如果项目启用了中央包版本管理，在 `Directory.Packages.props` 添加相应 `PackageVersion`，`.csproj` 的 PackageReference 不写 Version。

### 4. 把包和配置一起提交 Git

很多 .NET 项目的 `.gitignore` 默认包含 `*.nupkg`，否则会出现“本地能构建、Actions 找不到包”。在 `.gitignore` 末尾添加精确例外：

```gitignore
!/vendor/nuget/Kekincai.JiebaNet.*.nupkg
```

`vendor/nuget` 的父目录也不能被忽略。确认 Git 实际跟踪了这些文件：

```powershell
git add NuGet.Config .gitignore vendor/nuget src/MyApp.Server/MyApp.Server.csproj
git ls-files vendor/nuget
```

最后一个命令必须能列出 `.nupkg`。如仍列不出，用 `git check-ignore -v vendor/nuget/Kekincai.JiebaNet.Segmenter.1.0.0.nupkg` 找出命中的忽略规则。正常提交这些依赖与项目改动即可，不提交 NuGet 解压缓存。

### 5. 本地构建和发布

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --no-restore
dotnet publish src/MyApp.Server/MyApp.Server.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish
```

普通 `dotnet build` / `dotnet publish` 也会自动还原依赖。发布时 NuGet/MSBuild 会带上库程序集；默认模型已经内嵌，无需再复制 Resources 或手动拷贝 jieba DLL。自定义用户词典是应用自有数据，需要应用自行随发布复制或指定外部路径。

应用分发时保留本库的 MIT 许可和原版权说明。

## GitHub Actions 怎样配合

如果现有 workflow 已 checkout 应用仓库、安装 .NET 10 并运行普通 `dotnet build`，上述配置就足够：本地源随仓库一起 checkout，build 自动还原包。无需下载另一个仓库，无需 NuGet 登录凭据。

如希望单独显示还原阶段，可以在现有步骤中采用：

```yaml
- name: Restore
  run: dotnet restore
- name: Build
  run: dotnet build -c Release --no-restore
- name: Test
  run: dotnet test -c Release --no-build --no-restore
```

这些步骤在应用仓库根目录运行；不要再用 `--source` 只指定 NuGet.org，否则会绕过仓库本地源。应用自己的前端构建、原生依赖安装、发布、上传产物步骤继续按原工作流执行。

本库现有 Actions 负责验证和上传 `.nupkg` 构建产物，**没有自动发布到 NuGet.org**。代码推到 GitHub、上传 Release 附件或 Actions artifact，都不会自动成为可供 PackageReference 使用的 NuGet 源。

## 程序内怎样调用

ASP.NET Core 等服务建议复用一个实例：

```csharp
using JiebaNet.Segmenter;

builder.Services.AddSingleton<JiebaSegmenter>(_ =>
{
    var segmenter = new JiebaSegmenter();
    // 如有用户词典，在这里使用绝对路径加载一次。
    // segmenter.LoadUserDict(Path.Combine(builder.Environment.ContentRootPath, "Data", "user_dict.txt"));
    return segmenter;
});
```

在业务服务中通过构造函数注入 `JiebaSegmenter`，调用 `Cut(text)`。`Cut` 不会自动剥离文件扩展名、去除标点或停用词，这些由应用按用途处理。需要整词统计时用精确模式；搜索模式会额外输出长词内的短词，不适合直接当作去重后的文件计数。

## 更新库版本

1. 在本库的 `Directory.Build.props` 修改 Version，例如 `1.0.0` → `1.0.1`，同步更新文档中的版本示例。
2. 运行 `./scripts/verify.ps1`，生成并验证新版本包。
3. 将新包复制到应用的 `vendor/nuget`，将 PackageReference/中央版本改成新版本。
4. 提交包与项目版本更新，一起触发应用的 CI。
5. 仓库内没有其他项目引用旧版本时，可删除旧包后提交。

不要把修改过的 DLL 重新打成同一个已使用的版本号。NuGet 缓存按包名/版本复用，换内容不换版本会导致本地和 CI 使用不同内容。

只有确实需要排查缓存时，使用一次独立目录验证还原：

```powershell
dotnet restore --packages artifacts/nuget-clean-check
```

该目录是下载/解压缓存，不是要提交的包源。普通构建仍使用默认缓存。

## 长期公开分发：发布到 NuGet.org

多个项目或外部使用者开始使用后，推荐把本库正式发布到 NuGet.org，应用只保留 PackageReference，不再提交二进制包。发布本身需要先准备 NuGet.org 账号并确认包名可用；目前本库仅有本地产物，不能假定示例版本已能从 NuGet.org 安装。

本库维护者可按以下流程发布：

1. 修改并固定版本，运行本库验证，确认对应 commit 和两个 `.nupkg`。
2. 使用 NuGet.org 上传页面发布 Segmenter，再发布依赖它的 Analyser；或使用配置好的发布凭据运行 `dotnet nuget push`。
3. 等待包完成验证并可从 NuGet.org 还原，再更新应用。

凭据只配置在维护者环境或发布 workflow 中，不放进应用仓库。发布设置和账号准备见 [NuGet.org 官方发布说明](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package)。

应用迁移到 NuGet.org 后，删除 `repo-packages` 源、本库的本地源映射和不再使用的 vendored 包；如果其他依赖仍需要私有/本地源，保留它们。随后在全新缓存中运行 restore/build，确认本库确实可从远端源取得。

## 哪种引用方式适合当前情况

| 方式 | 应用 Actions 能否直接复现 | 用途 |
|---|---|---|
| 仓库内 `.nupkg` + 相对本地源 | 可以，包和配置必须提交 | 当前无需发布账号就能使用 |
| NuGet.org + 固定版本 PackageReference | 可以，版本必须已发布 | 长期公开分发推荐 |
| 相邻目录 ProjectReference | 只 checkout 应用仓库时不可以 | 两个仓库在本机共同开发 |
| 手工引用本地 DLL | 需要自行维护 DLL/依赖/版本 | 不作为推荐分发方式 |

已验证仓库内本地源方案可在独立 NuGet 缓存下还原、构建、发布，并运行分词、POS、TF-IDF/TextRank 和无参工厂创建。
