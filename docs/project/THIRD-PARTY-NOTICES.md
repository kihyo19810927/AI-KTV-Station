# 第三方组件声明

AI-KTV Station 发布包包含 .NET 运行时和锁定的应用运行时依赖，并托管 React/Vite 构建产物。依赖版本与许可证审计依据见源码仓库 `docs/project/DEPENDENCY-LICENSE-REVIEW.md` 和各 `packages.lock.json` / `package-lock.json`。

主要运行时组件包括 Microsoft .NET/ASP.NET Core/EF Core（MIT）、QRCoder（MIT）、ToolGood.Words（Apache-2.0）、SQLite/SQLitePCLRaw，以及 React 与 SignalR JavaScript 客户端及其传递依赖。各组件版权和许可仍归各自权利人所有。

mpv、FFmpeg 和 ffprobe 不包含在本发布包中。它们是用户单独安装并由 Station 作为外部进程调用的软件，其许可与源码提供义务由相应分发来源负责。不得把本机二进制复制进 Station 包。

Station 仓库当前没有项目级开源许可证；除权利人明确授权外，保留项目自身代码的全部权利。本文件不授予超出第三方许可证或权利人授权的许可。
