# 队列与播放器编排

KTVS-030 由 `QueuePlaybackOrchestrator` 将持久化队列、`IPlayerAdapter` 和播放恢复策略连接起来；mpv IPC 仍只存在于适配器实现中。

- 启动后选择位置最前的等待项，创建播放历史并依次写入 `Preparing`、`Playing`。
- 应用重启先把遗留的 `Preparing`/`Playing`/`Paused` 项原子恢复为 `Waiting`，并以 `playback.interrupted_by_restart` 闭合旧历史，之后才允许重新编排。
- 匹配当前播放实例的完成事件将队列项和历史标为完成，然后自动加载下一首。
- 停止/替换事件标为跳过；失败事件先记录错误，再按 KTVS-025 策略重试、重启 mpv、跳过或停止整条播放链。
- mpv 重启重试会生成新的播放实例 ID，使旧进程迟到的结束事件无法跳过重试中的歌曲。
- 不可用媒体进入同一恢复流程；耗尽重试后保留索引、标记失败并继续下一首。无任何媒体的歌曲直接失败，不向播放器泄露空路径。
- 队列和历史的事实状态由 `IPlaybackQueueStore` 持久化；Application 层不依赖 EF、文件系统或 mpv。

自动测试覆盖两首自动连播、播放器进程重启重试、重试耗尽跳过、系统性故障停止和迟到事件忽略。真实电视、功放、CloudDrive 断挂与连续歌曲体验保持待实机验收。
