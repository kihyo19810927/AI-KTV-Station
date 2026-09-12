# ADR-0013：发布包携带独立媒体工具

- 状态：Accepted（公开发布前需完成许可证材料与来源复核）
- 日期：2026-09-12
- 关联任务：KTVS-072

## 背景

用户下载安装 Station 后不应再安装 mpv、FFmpeg/ffprobe 或配置 PATH。程序已经通过独立进程和 JSON IPC 使用这些工具，`common/` 中也有经过环境核对的 Windows x64 副本。此前 ADR-0011 为避免许可证范围扩大而禁止随包分发，已经与“解压即用”的产品要求冲突。

## 决策

Windows ZIP 发布包复制以下版本化运行时文件：

- `common/mpv/mpv.exe`
- `common/mpv/vulkan-1.dll`
- `common/ffmpeg/ffmpeg.exe`
- `common/ffmpeg/ffprobe.exe`

它们分别进入包内 `tools/mpv/` 和 `tools/ffmpeg/`。程序优先从包内 `tools/` 定位，保留 PATH 作为开发环境兜底；设置页不再要求用户填写可执行文件路径。`common/` 中的二进制继续被 Git 忽略，不提交仓库。

包内同时携带 `tools/licenses/` 下的版本、来源、版权和许可证说明。当前 ffprobe 来自启用 GPL/版本 3 组件的 FFmpeg full build，mpv 为 CI 开发构建；因此发布脚本在工具或说明缺失时失败，正式公开发布前必须锁定可复现版本并复核对应完整许可证文本、源码获取方式和版权声明。

## 取舍

- 优点：普通用户无需额外安装和配置，发布包可自包含运行。
- 代价：包体积显著增加，且分发者承担第三方二进制许可证材料和来源归档责任。
- 边界：Station 仍通过独立进程 IPC 调用 mpv，不链接 mpv/FFmpeg 库；本 ADR 不对项目自身许可证作决定。
- 安全：工具只读媒体，发布包不携带数据库、设置、令牌或真实曲库。
