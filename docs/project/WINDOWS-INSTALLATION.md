# Windows 发布包与安装

## 系统要求

- Windows 11 x64。
- 发布包自带 `tools/mpv`、`tools/ffmpeg` 及 `tools/licenses`，无需用户另行安装 mpv、FFmpeg 或配置 `PATH`。
- 115 内容应通过 CloudDrive/CloudDrive2 挂载为本机可读目录。Station 只读扫描媒体。

## 安装与首次启动

1. 核对 ZIP 旁 `.sha256` 文件与本机 `Get-FileHash <zip> -Algorithm SHA256` 一致。
2. 将 ZIP 解压到用户可写的固定目录，例如 `%LOCALAPPDATA%\Programs\AI-KTV-Station`。不要从 ZIP 内直接运行。
3. 当前 Server/桌面候选包启动 `Station.Tray.exe`。它驻留通知区、启动本机 Server 并打开 Edge App 窗口。退出托盘菜单会仅停止本程序所创建的 mpv 实例，不会结束其他 mpv 进程。首次启动会在程序目录的 `data` 子目录创建 SQLite 数据库，并在 `%LOCALAPPDATA%\AI-KTV Station` 保存设置、日志和脱敏诊断。不要将 `Station.Server` 的开发命令当作日常入口：它没有托盘，也不会自行创建房间。
4. 如需手机访问，在设置中选择明确的局域网 IP；服务默认监听 `0.0.0.0:5090`，房间二维码使用可达的局域网地址。不得映射公网端口。
5. 先添加一个小批测试目录，确认后手动扫描；添加来源不会自动扫描，更不得直接选择庞大的挂载根目录。

升级时不得删除 `station.db`。Station 仅在存在待应用迁移时创建一致性备份，迁移失败会恢复原数据库并停止启动。升级正式库前仍应把程序目录与 `%LOCALAPPDATA%\AI-KTV Station` 复制到独立备份位置并执行小库演练。

## 构建发布包

从干净源码使用 PowerShell 7：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/publish-windows.ps1 -Version 0.1.0-rc.12
```

若要将已验收的歌曲索引、收藏和人工修正作为新安装的初始数据一并发布，关闭正在使用该数据库的 Station 实例后显式传入数据库快照；路径不会写入仓库或安装包配置：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/publish-windows.ps1 -Version 0.1.0-rc.19 -SeedDatabasePath 'D:\Applications\AI-KTV-Station\data\station.db'
```

发布脚本只接受名为 `station.db` 的非空文件。它通过 SQLite 只读备份 API 生成一致快照，因此即使源库带有 `-wal`/`-shm` 旁写日志也不会漏掉已提交数据；随后只对发布暂存副本应用待迁移并重建搜索索引，源数据库不会被修改。安装包中的初始库位于 `data/station.db`；升级已有安装时，不会覆盖目标目录中已有的数据库。

输出位于 `artifacts`，包含 ZIP 和 SHA-256 文件。脚本以锁定依赖构建 Web 与 .NET 自包含 `win-x64` 包，复制 `common/` 中经版本核对的运行工具及许可证说明，生成逐文件清单，并拒绝数据库或用户设置进入包。若工具或许可证说明缺失，发布脚本会失败，不生成不完整包。
