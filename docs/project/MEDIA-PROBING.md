# 媒体探测

KTVS-015 使用 `ffprobe` 子进程读取媒体元数据。扫描器仅向探测器传入媒体完整路径；探测过程不写入、不移动也不删除媒体。

## 数据流

1. 增量扫描发现新文件，或检测到文件大小、修改时间、可用状态发生变化。
2. `IMediaProbe` 启动 `ffprobe`，通过参数数组传递路径并读取 UTF-8 JSON。
3. 解析器保存时长，以及视频、音频、字幕流的流 ID、类型、编码、语言和标题。
4. 成功结果写入 `MediaFile.DurationSeconds` 和 `MediaTrack`；失败只把文件标记为 `Unreadable` 并记录稳定错误码，歌曲和文件索引仍保留。

未变化文件不会重复探测。已有文件重新探测失败时保留上一次成功的时长和轨道，便于远端挂载恢复后再次扫描。

## 错误分类

| 错误码 | 含义 |
|---|---|
| `media_probe.executable_missing` | 配置的 ffprobe 不存在 |
| `media_probe.start_failed` | 子进程未能创建 |
| `media_probe.timeout` | 在配置时间内未退出，进程树会被终止 |
| `media_probe.process_failed` | ffprobe 非零退出 |
| `media_probe.invalid_json` | 输出不是预期 JSON 结构 |
| `media_probe.invalid_duration` | 缺少合法非负时长 |

## 验证

默认测试使用固定 JSON 和替身探测器，不依赖本机媒体。外部集成测试运行 `scripts/test-media-probe.ps1`：脚本先在 `tests/fixtures/Unicode 测试/` 生成约 10 秒 MKV，再用真实 ffprobe 验证 4 条轨道和 Unicode 元数据。夹具目录被 Git 忽略，二进制不会提交。

真实 CloudDrive/CloudDrive2 挂载和私人 MKV 的时延、损坏文件行为仍为待实机验收，不影响后续软件任务。
