# Windows 发布包与安装

## 系统要求

- Windows 11 x64。
- mpv 由用户单独安装：`winget install --exact --id mpv-player.mpv-CI.MSVC --source winget`。
- FFmpeg/ffprobe 由用户从可信来源单独安装，并加入 `PATH`；发布包不包含这些程序。
- 115 内容应通过 CloudDrive/CloudDrive2 挂载为本机可读目录。Station 只读扫描媒体。

## 安装与首次启动

1. 核对 ZIP 旁 `.sha256` 文件与本机 `Get-FileHash <zip> -Algorithm SHA256` 一致。
2. 将 ZIP 解压到用户可写的固定目录，例如 `%LOCALAPPDATA%\Programs\AI-KTV-Station`。不要从 ZIP 内直接运行。
3. 启动 `Station.Desktop.exe`。首次启动会在程序目录的 `data` 子目录创建 SQLite 数据库，并在 `%LOCALAPPDATA%\AI-KTV Station` 保存设置、日志和脱敏诊断。
4. 如需手机访问，在设置中选择明确的局域网 IP；默认 `127.0.0.1:5090` 仅本机可访问。不得映射公网端口。
5. 先添加一个小批测试目录，确认后手动扫描；添加来源不会自动扫描，更不得直接选择庞大的挂载根目录。

升级时不得删除 `station.db`。Station 仅在存在待应用迁移时创建一致性备份，迁移失败会恢复原数据库并停止启动。升级正式库前仍应把程序目录与 `%LOCALAPPDATA%\AI-KTV Station` 复制到独立备份位置并执行小库演练。

## 构建发布包

从干净源码使用 PowerShell 7：

```powershell
pwsh.exe -ExecutionPolicy Bypass -File scripts/publish-windows.ps1 -Version 0.1.0-rc.1
```

输出位于 `artifacts`，包含 ZIP 和 SHA-256 文件。脚本以锁定依赖构建 Web 与 .NET 自包含 `win-x64` 包，生成逐文件清单，并拒绝 mpv、FFmpeg、数据库或用户设置进入包。
