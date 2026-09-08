# mpv 正式适配器技术报告

KTVS-021 将 KTVS-003 Spike 收敛为 Infrastructure 层的 `MpvPlayerAdapter`。每个适配器进程使用含进程号和随机 GUID 的独立 Windows named pipe；所有 JSON 命令使用单调递增 `request_id`，读取循环可在同一管道上交错处理响应和 mpv 事件，写入由异步锁串行化。

## 进程与协议边界

- mpv 以 `--idle=yes`、无终端、无强制窗口模式启动，参数通过 `ArgumentList` 传递以保留 Unicode 路径。
- 启动连接和每个命令均受配置的命令超时限制；调用方取消不会转换为播放器故障。
- 非 `success` 响应、畸形/中断协议、加载失败和进程意外退出被映射为稳定 Application 错误或 `PlayerFailure`，原始 JSON、pipe 名和媒体路径不会离开 Infrastructure。
- 主动停止只清理适配器持有的子进程；超时后也只按已捕获 PID 终止该进程树，不扫描或终止其他 mpv 实例。
- `file-loaded`、`end-file` 和进程退出由持续读取循环接收。替换媒体时旧 `PlaybackId` 只产生一次 `Replaced`，自然 EOF 映射为 `Completed`。

## 自动验证

`scripts/test-mpv-adapter.ps1` 每次用 FFmpeg 重建约 10 秒的 Unicode 路径 MKV，然后运行两项外部集成测试：完整播放覆盖状态、四轨枚举、原唱/伴奏切换、字幕切换和 EOF；异常测试终止当前适配器明确持有的 mpv PID，并确认收到可重试 `ProcessExited` 故障。测试夹具目录被 Git 忽略，不提交 MKV。

真实歌曲、电视 HDMI、功放声道和 CloudDrive/115 挂载仍为待实机验收，不影响后续软件任务。
