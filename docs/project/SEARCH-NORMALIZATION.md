# 搜索文本规范化

KTVS-017 在媒体入库时预计算搜索键，查询阶段只访问 SQLite，不读取媒体目录。技术选择见 [ADR-0007](../adr/ADR-0007-search-text-normalization.md)。

每个标题保存以下字段：

| 字段 | 规则 |
|---|---|
| `NormalizedTitle` | Unicode NFKC、Invariant 小写、连续空白折叠 |
| `SimplifiedTitle` | 规范化后的简体兼容键 |
| `TraditionalTitle` | 规范化后的繁体兼容键 |
| `TitlePinyin` | 简体键的无音调全拼，仅保留 Unicode 字母和数字 |
| `TitleInitials` | 拼音首字母紧凑键 |
| `CompactTitle` | 原文规范化键移除标点和空白后的字母数字形式 |

歌手独立保存同类的 `NormalizedName`、`SimplifiedName`、`TraditionalName`、`Pinyin`、`Initials` 和 `CompactName`，因此多歌手可在 KTVS-018 分别进入 FTS 索引。

扫描新文件时一次性生成这些字段；已有数据库记录若字段为空，会在下一次增量扫描中原地补齐并保存，不接触媒体内容。`ISearchTextNormalizer` 位于 Application，ToolGood 实现只存在于 Infrastructure；组合根必须注入该实现。应用层提供的 invariant fallback 用于无中文转换组件的受控场景，不伪造拼音结果。

当前 Unicode 回归样本覆盖全角拉丁字母/数字、空白、emoji、简体、繁体、原文、拼音和简拼。多音字结果依赖锁定词典版本；显示文本始终使用人工/NFO/文件名元数据，不用搜索键反向覆盖。
