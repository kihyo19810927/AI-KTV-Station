# V1.0 候选版验证记录

候选版本：`0.1.0-rc.3`。本记录严格区分自动化软件证据与待实机验收。

## 自动化验证

| 项目 | 状态 | 证据 |
|---|---|---|
| 锁定依赖、格式、Release 构建 | 通过 | `scripts/run-uat-readiness.ps1`：0 warning / 0 error |
| Core/Desktop/Web 测试 | 通过 | Core 153/153、Desktop 12/12、Web 24/24 |
| Unicode 双音轨 MKV、字幕、mpv EOF | 通过 | 10.023 秒、4 轨、伴奏/原唱/字幕标题、`END_FILE=received` |
| win-x64 自包含 ZIP 与 SHA-256 | 通过 | 566 项；SHA-256 `6d5d3a363d1318dbce5715cf60fd295a5231f15eb45cbc86dde61e3d3e94f1df` |
| 解压后 Desktop/SQLite/内嵌服务启动 | 通过 | 随机临时目录与 loopback 端口；`PACKAGE_SMOKE=passed` |
| 运维文档完整性 | 通过 | 6 份文档、7 个章节、3 条安全警告 |

打包应用冒烟测试使用随机临时安装目录、随机 loopback 端口和 `AI_KTV_STATION_SETTINGS_ROOT` 隔离设置；只停止自身启动的进程并清理自身临时目录，不访问真实曲库或用户数据库。

首次 RC2 冒烟发现全新解压目录缺少 `data` 时桌面启动停在数据库初始化；RC3 在迁移前创建配置的数据目录并完成回归。RC2 不作为候选发布物。

## 待实机验收

- 全新 Windows 11 x64 主机解压、首次启动、Windows 防火墙 LAN 范围配置和卸载。
- Android Chrome 与 iPhone Safari 扫码、搜索、并发点歌、锁屏和 Wi-Fi 短断线恢复。
- 用户明确选择的 5–20 首真实 115/CloudDrive 子目录：MKV/MPG、KSC、RAR、无 NFO、人工修正保留。
- 真实电视画面/字幕、功放音量与原唱/伴奏语义、50 首连续播放。
- CloudDrive 实际断挂/403 恢复、突然断电和正式数据库升级备份演练。

上述任一实机项未执行时，KTVS-058 只能标记“软件验证完成、实机阻塞”，不得宣称 V1.0 候选版整体通过。
