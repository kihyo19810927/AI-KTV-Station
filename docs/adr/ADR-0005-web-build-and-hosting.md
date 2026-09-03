# ADR-0005：手机 Web 构建与宿主

状态：Accepted  
日期：2026-09-03

## 决策

- 手机端使用 React、TypeScript 和 Vite，保持静态 SPA，不引入 SSR/Next.js。
- ASP.NET Core 在开发时与 Vite 独立运行，在发布时托管版本化构建产物。
- Web 客户端仅调用 REST/SignalR；不得接收 Windows 路径、云盘凭据、永久管理员密钥或视频字节流。
- npm 操作锁定依赖并使用 `npm.cmd`/`npx.cmd`；发布构建必须可从干净克隆重复执行。
- API 契约由服务端 DTO/OpenAPI 定义，不直接序列化 EF 实体或领域内部类型。
