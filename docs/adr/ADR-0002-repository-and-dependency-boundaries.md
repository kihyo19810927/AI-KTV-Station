# ADR-0002：仓库与依赖边界

状态：Accepted  
日期：2026-09-03

## 背景

Station 负责家庭 KTV 的索引、点歌、队列和播放，AI-KTV-Builder 负责成品媒体制作。桌面、Web、持久化和播放器技术都可能变化，核心规则不能被具体框架绑定。

## 决策

- Station 保持独立仓库，通过成品 MKV/NFO 或未来显式、版本化 API 与 Builder 交付。
- 依赖方向固定为：UI/Server/Infrastructure → Application → Domain。
- Domain 不引用 WPF、ASP.NET Core、EF Core、mpv、FFmpeg 或文件系统。
- Application 定义播放器、持久化、时钟和文件目录等端口；Infrastructure 实现这些端口。
- V1 优先单进程：WPF 进程承载 Generic Host、内嵌 ASP.NET Core 和后台服务，mpv 作为受控子进程。

## 后果

领域规则可快速单测；外部实现可替换。代价是需要明确 DTO/端口映射，不能从 endpoint 或 code-behind 直接访问数据库与 mpv。
