# 依赖与许可证审查

KTVS-053 以提交的 NuGet/NPM 锁文件、已还原包元数据和本机二进制构建信息为依据。可用 `scripts/audit-dependencies.ps1` 重建版本清单；版本变化后必须重做审查。

## 直接运行时依赖

| 组件 | 当前版本 | 许可证/结论 | 发布处理 |
|---|---:|---|---|
| .NET / ASP.NET Core / EF Core / Microsoft.Extensions | 10.0.11 | Microsoft 包元数据为 MIT | 保留版权与许可证通知 |
| QRCoder | 1.8.0 | MIT | 保留通知 |
| ToolGood.Words | 3.1.0.3 | Apache-2.0，包内含 LICENSE | 保留 NOTICE/许可证 |
| SQLitePCLRaw / e_sqlite3 | 2.1.12 | 包组合包含 Apache-2.0 与 SQLite public-domain blessing | 发布前复制对应包通知 |
| React / React DOM | 19.2.8 | MIT | Web 产物第三方通知 |
| React Router | 7.18.3 | MIT | Web 产物第三方通知 |
| Microsoft SignalR JS | 10.0.11 | MIT | Web 产物第三方通知 |
| Lucide React | 1.43.0 | ISC | Web 产物第三方通知 |

生产 NPM 传递依赖的已安装元数据显示 MIT、ISC、BSD-2-Clause、BSD-3-Clause、Apache-2.0、Unlicense、MIT-0、CC0-1.0、MPL-2.0 和 BlueOak-1.0.0；未发现 `UNKNOWN`。发布前仍须从锁定依赖图生成完整第三方清单并确认最终 bundle 实际包含内容。

测试/构建依赖包括 xUnit（Apache-2.0）、Microsoft.NET.Test.Sdk、coverlet、Vite、Vitest 和 TypeScript；它们不作为 Station 运行时 API，但源码发布和构建环境应保留其许可信息。

## mpv 与 FFmpeg 二进制边界

Station 通过 JSON IPC/子进程调用外部可执行文件，不链接其库。本机 `ffmpeg 8.1.2-full_build-www.gyan.dev` 参数包含 `--enable-gpl --enable-version3 --enable-static`，因此按 GPLv3 分发要求处理。mpv 官方说明默认完整构建为 GPLv2+，只有排除 GPL 文件及受影响链接库的特定构建才可能为 LGPL；当前 WinGet CI 二进制的版本输出不足以证明精确组合许可。

V1 发布包不得内置、复制或重新分发本机 mpv/FFmpeg。安装说明要求用户从受信任来源单独安装，Station 只定位外部程序。若未来制作一体化安装包，必须锁定二进制来源、版本、构建参数、对应源代码与完整许可证义务后另行批准。

核对来源：[mpv 官方 Copyright](https://github.com/mpv-player/mpv/blob/master/Copyright)、[FFmpeg 官方法律与许可说明](https://ffmpeg.org/legal.html)、[Gyan Windows 构建说明](https://www.gyan.dev/ffmpeg/builds/)。这些链接用于识别义务，不构成法律意见。

## 项目自身许可

仓库当前没有项目级 `LICENSE`。这不影响本地开发和私人使用，但任何外部公开发布前必须由所有者选择 Station 自身许可或明确保留所有权；本任务不替用户作该发布决定。
