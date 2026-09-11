# V1.0 候选版验证记录

候选版本：`0.1.0-rc.6`。本记录严格区分自动化软件证据与待实机验收。

## 自动化验证

| 项目 | 状态 | 证据 |
|---|---|---|
| 锁定依赖、格式、Release 构建 | 通过 | `scripts/run-uat-readiness.ps1`：0 warning / 0 error |
| Core/Desktop/Web 测试 | 通过 | Core 159/159、Desktop 14/14、Web 24/24 |
| Unicode 双音轨 MKV、字幕、mpv EOF | 通过 | 10.023 秒、4 轨、伴奏/原唱/字幕标题、`END_FILE=received` |
| win-x64 自包含 ZIP 与 SHA-256 | 通过 | 566 项；SHA-256 `11db6b4cf5b385402a4099922e1416ad22403752fa1ddb416bf65ea9431d6d5f` |
| 解压后 Desktop/SQLite/内嵌服务启动与退出 | 通过 | 随机临时目录与 loopback 端口；`PACKAGE_SMOKE=passed`；退出后 Station/mpv 进程和 5090 监听均为 0 |
| 运维文档完整性 | 通过 | 6 份文档、7 个章节、3 条安全警告 |

打包应用冒烟测试使用随机临时安装目录、随机 loopback 端口和 `AI_KTV_STATION_SETTINGS_ROOT` 隔离设置；只停止自身启动的进程并清理自身临时目录，不访问真实曲库或用户数据库。

首次 RC2 冒烟发现全新解压目录缺少 `data` 时桌面启动停在数据库初始化；RC3 修复数据目录初始化。RC6 进一步纳入本机 `common` 工具发现、Demo 视觉升级、两阶段扫描、手机控制和正常退出进程清理回归。RC2 至 RC5 均不作为当前候选发布物。

## 待实机验收

- 全新 Windows 11 x64 主机解压、首次启动、Windows 防火墙 LAN 范围配置和卸载。
- Android Chrome 与 iPhone Safari 扫码、搜索、并发点歌、锁屏和 Wi-Fi 短断线恢复。
- 用户明确选择的 5–20 首真实 115/CloudDrive 子目录：MKV/MPG、KSC、RAR、无 NFO、人工修正保留。
- 真实电视画面/字幕、功放音量与原唱/伴奏语义、50 首连续播放。
- CloudDrive 实际断挂/403 恢复、突然断电和正式数据库升级备份演练。

上述任一实机项未执行时，KTVS-058 只能标记“软件验证完成、实机阻塞”，不得宣称 V1.0 候选版整体通过。
