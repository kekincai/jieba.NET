# 发布到 NuGet.org

包由 NuGet.org 账号 `kekincai` 发布。发布流程位于 `.github/workflows/publish-nuget.yml`，使用 GitHub OIDC 换取短期凭据，无需保存长期 API Key。

## 首次设置

登录 [NuGet.org Trusted Publishing](https://www.nuget.org/account/trustedpublishing)，创建 GitHub 可信发布策略：

| 字段 | 值 |
|---|---|
| Policy owner | kekincai |
| Repository owner | kekincai |
| Repository | jieba.NET |
| Workflow file | publish-nuget.yml |
| Environment | 留空 |
| Package pattern | Kekincai.JiebaNet.* |

范围需允许创建新包及发布新版本；首次发布时两个包尚不存在。Workflow file 只填文件名，不填目录。账号需要符合 NuGet.org 的发布要求。策略创建成功后再推送版本标签。

## 发布版本

1. 在 `Directory.Build.props` 设置新版本，并更新文档版本示例。
2. 提交代码并推送分支，确认 Windows/Linux CI 通过。
3. 在准备发布的同一 commit 创建并推送标签，例如 `v1.0.0`。标签必须精确匹配 `Version`。
4. `Publish NuGet` Actions 执行完整构建、测试、依赖审计和实际包消费验证，全部成功后才申请发布凭据，依次推送 Segmenter 和 Analyser。
5. 等待 NuGet.org 完成验证和索引，用全新缓存还原公开版本。GitHub workflow 成功不代表索引已经完成。

推送普通分支不会发布。版本标签会实际发布公开包，因此只对准备好的版本推送标签。已发布的版本不能用新内容覆盖。

若第二个包失败，先检查 NuGet.org 是否已有成功发布的包，再重新运行同一个 workflow；`--skip-duplicate` 允许略过已发布版本。不要修改该标签的内容。

## 应用使用

公开版本可还原后，应用使用固定版本的 `PackageReference`：

```xml
<PackageReference Include="Kekincai.JiebaNet.Segmenter" Version="1.0.0" />
```

需要 TF-IDF/TextRank 时引用 `Kekincai.JiebaNet.Analyser`，会自动还原 Segmenter。应用无需保存 `.nupkg`，现有 GitHub Actions 使用 nuget.org 即可还原。完整迁移步骤见 [应用接入说明](consuming-packages.md)。

官方机制说明：[NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)。
