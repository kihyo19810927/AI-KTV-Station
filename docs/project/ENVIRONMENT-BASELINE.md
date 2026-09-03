# KTVS-005 实际环境基线

日期：2026-09-03  
平台：Windows 11（本地开发主机）

## 自动验证

执行：

```powershell
pwsh.exe -ExecutionPolicy Bypass -File .\scripts\verify-environment.ps1
```

脚本验证 .NET、Node/npm、FFmpeg/ffprobe、mpv 可执行文件；重复生成约 10 秒 Unicode 路径 MKV；用 ffprobe 断言一路视频、标题为“伴奏/原唱”的两路音频和一路字幕；随后运行 C# named-pipe mpv Spike。

2026-09-03 结果：.NET `10.0.400`、Node `v24.18.0`、npm `11.16.0`、FFmpeg/ffprobe `8.1.2`、mpv `v0.41.0-dev-g41f6a6450`；夹具时长 `10.023s`；`ENVIRONMENT_BASELINE=passed`。

## 待实机验收

以下项目不能由生成夹具代替，但不阻塞非硬件软件任务：

- [ ] 真实双音轨 MKV 的标题、声道和“伴奏/原唱”语义正确
- [ ] mpv 在电视/投影仪上全屏画面、分辨率和刷新率正常
- [ ] 功放/HDMI/声卡输出设备选择、音量和静音正常
- [ ] 外挂/内封字幕的字体、中文/日文和时序可接受
- [ ] CloudDrive/CloudDrive2 挂载路径可读，首播与切歌延迟可接受
- [ ] 115 临时离线/403 后索引保留，队列可继续
- [ ] Android Chrome 与 iPhone Safari 能通过家庭局域网扫码加入

验收时只记录脱敏结果、耗时和错误分类；不得提交真实挂载路径、文件名、凭据或媒体。
