# AI-KTV Station

Windows 11 家庭 KTV 点歌主机：Windows 主机负责曲库索引、房间、队列和播放；同一局域网内的手机浏览器负责点歌。

## 当前状态

项目处于 Phase 0（仓库基线与产品边界定义）。权威计划见 [AI-KTV-Station 完整开发计划与 Codex 提示词](AI-KTV-Station_完整开发计划与Codex提示词.md)，持续状态见 [docs/project/STATUS.md](docs/project/STATUS.md)。

## 边界

Station 与 AI-KTV-Builder 独立：Builder 制作成品 MKV，Station 只读索引、点歌、排队和播放。Station 不上传或转发视频，不管理 115/CloudDrive 文件，也不删除、移动、重命名真实媒体。

## 开发

目标技术栈为 .NET 10、WPF/MVVM、ASP.NET Core Minimal API、SignalR、React/TypeScript/Vite、EF Core/SQLite、mpv JSON IPC 和 ffprobe。当前机器缺少 .NET SDK 与 mpv，具体验证状态记录在 `docs/project/STATUS.md`。

## 安全提醒

不要把账号密码、Token、Cookie、真实挂载路径或生产配置提交到仓库。手机端只接触歌曲公开元数据和短期房间会话信息。
