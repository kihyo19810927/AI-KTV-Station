# 第三方组件声明

AI-KTV Station 发布包包含 .NET 运行时和锁定的应用运行时依赖，并托管 React/Vite 构建产物。依赖版本与许可证审计依据见源码仓库 `docs/project/DEPENDENCY-LICENSE-REVIEW.md` 和各 `packages.lock.json` / `package-lock.json`。

主要运行时组件包括 Microsoft .NET/ASP.NET Core/EF Core（MIT）、QRCoder（MIT）、ToolGood.Words（Apache-2.0）、SQLite/SQLitePCLRaw，以及 React 与 SignalR JavaScript 客户端及其传递依赖。各组件版权和许可仍归各自权利人所有。

发布包会在 `tools/` 中携带 mpv、FFmpeg/ffprobe 及 mpv 所需的 Vulkan 运行库。Station 通过独立进程和 JSON IPC 调用 mpv，不把这些组件链接进业务程序集。随包分发的版本、构建来源和许可证/版权说明位于 `tools/licenses/`；对应的上游源码和许可证入口也列在这些说明文件中。当前构建版本为 mpv CI 开发构建和 Gyan FFmpeg full build，正式公开发布前仍需完成版本锁定、完整许可证材料复核和来源归档。

Station 仓库当前没有项目级开源许可证；除权利人明确授权外，保留项目自身代码的全部权利。本文件不授予超出第三方许可证或权利人授权的许可。
