# 元数据来源与优先级

歌曲初次入库时，应用层用可替换提供器取得文件名和可选 NFO 元数据，再按字段合并。最终优先级固定为：

1. 人工修正；
2. 同名 NFO；
3. 文件名解析结果。

优先级按字段应用，例如人工只修正标题时，歌手仍可来自 NFO。已有歌曲再次扫描时，扫描器不覆盖歌曲字段，因此人工编辑不会被文件名或 NFO 回写覆盖。未来的编辑 API 必须使用同一 `SongMetadataResolver` 规则。

## NFO 读取边界

- 只读取媒体同目录、同文件名的 `.nfo`，不搜索或修改其他文件。
- NFO 缺失是正常状态，直接使用文件名结果。
- 正式 115 曲库不要求上传 NFO；无 NFO 建库是强制验收场景，不是降级错误。
- XML 禁止 DTD 和外部实体，默认最大 1 MiB，防止实体展开和超大辅助文件占用资源。
- 支持常见的 `title`、一个或多个 `artist`（文本或嵌套 `name`）、`language`、`category`/`genre`、`year`、`quality`、`version`/`edition`。
- malformed、无权限或超限 NFO 转换为稳定 warning，歌曲仍按较低优先级来源入库。
- NFO 标题或歌手与文件名不一致时保留 `metadata.nfo_title_mismatch` / `metadata.nfo_artist_mismatch` warning；NFO 值仍按既定优先级采用，不阻止入库。

当前数据库尚不持久化来源 warning；解析结果会把 warning 返回给调用用例，后续扫描管理 API 可展示。真实 Builder 产出的脱敏 NFO 样本仍需后续补充，但不阻塞软件开发。

KSC 与 NFO 职责不同：KSC 是歌词伴随文件，只关联到同主干媒体，不作为歌曲元数据来源，也不作为可播放媒体。当前保存关联供后续歌词解析/显示使用。
