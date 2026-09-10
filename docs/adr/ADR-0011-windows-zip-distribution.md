# ADR-0011：Windows 自包含 ZIP 分发

- 状态：Accepted
- 日期：2026-09-10

## 背景

V1 需要在 Windows 11 上可安装运行，同时当前项目自身许可尚未确定，mpv/FFmpeg 的精确二进制许可组合也未锁定。引入 MSI/Inno Setup 等安装器还会增加工具和许可证审计面。

## 决策

候选版使用 .NET `win-x64` self-contained 文件夹压缩为版本化 ZIP。包中包含 Web 静态资源、安装说明、第三方声明、逐文件 SHA-256 清单，并为 ZIP 生成独立 SHA-256。发布脚本拒绝 mpv、FFmpeg/ffprobe、SQLite 业务数据库和用户设置进入包。

mpv 与 FFmpeg 继续由用户从可信来源单独安装。正式 GitHub Release、签名和安装器留待项目许可确定及候选版实机验收后执行。

## 后果

- 目标 Windows 主机无需预装 .NET 运行时。
- ZIP 安装可审计、可复制且不引入新的安装器依赖。
- 包体积大于框架依赖部署；自动更新和系统级卸载暂不提供。
- 升级必须由 KTVS-056 的数据库备份与恢复流程保护，不能删库重建。
