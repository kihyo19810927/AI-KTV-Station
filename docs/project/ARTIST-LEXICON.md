# 歌手字典同步

`scripts/sync-artist-lexicon.ps1` 将旧版姓名键对象、V2 对象或 V2 数组统一生成为版本化歌手字典。输出字段包括稳定 `id`、显示名、别名、国家或地区、浏览分组、热度、头像、来源和来源歌手 ID。

```powershell
pwsh -File scripts/sync-artist-lexicon.ps1 -Source <上游歌手数据.json>
```

V2 输入推荐为 `{"schemaVersion":2,"artists":[...]}`。同名歌手必须使用不同稳定 ID；有上游身份时使用 `source` 与 `sourceArtistId`，不得只以姓名区分。`scripts/artist-lexicon-overrides.json` 保存经人工确认的纠错记录，默认在同步时合并；`replaceByName` 仅用于替换旧字典中确认错误的单一姓名记录。

歌曲语种只作为同名歌手消歧线索：韩语优先韩国歌手，日语优先日本歌手。它不等同于歌手国籍；无可靠身份时不得虚构头像或来源 ID。

旧版字典仍可由运行时读取，便于发布包平滑升级。新脚本输出 Schema V2 数组，因此可以同时保留不同国家或地区的同名歌手。
