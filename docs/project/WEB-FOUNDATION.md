# 手机 Web 基础工程

KTVS-034 在 `src/Station.Web` 建立 React 19、TypeScript 7、Vite 8 和 React Router 7 工程，视觉基线采用用户确认的 Demo：移动端单列、紫色强调、正在播放条、歌曲列表与底部导航，并支持系统深色模式和 320px 起布局。

- `StationApiClient` 统一 Bearer 头和 RFC 7807 错误；令牌不进入 URL。
- `SessionProvider` 定义房间、昵称、角色、短期令牌和过期时间状态边界。
- Vite 开发代理转发 `/api` 和 `/hubs`；生产输出到 Server `wwwroot`。
- ASP.NET Core 启用静态文件、默认页和 SPA 路由回退。
- `npm.cmd run typecheck`、`npm.cmd test`、`npm.cmd run build` 纳入本地脚本和 Windows CI。
- `node_modules`、TypeScript 缓存和哈希构建产物不提交；`package-lock.json` 是依赖事实来源。

当前页面中的歌曲与房间内容仅用于视觉骨架，KTVS-035～039 将逐步替换为真实 API/SignalR 状态。
