# V1 安全审查

KTVS-052 对监听、授权、令牌、路径、日志和浏览器响应进行了代码与自动测试审查。

## 已验证边界

- Kestrel 实际使用 `Station.Server.BindAddress` 和 `Port`；地址必须是显式 IPv4/IPv6，拒绝主机名、通配字符串和 URL 注入。
- 默认监听 `0.0.0.0:5090` 以满足家庭局域网点歌；二维码仍只生成主机可达的明确局域网地址。若用户只做本机验证可改为 `127.0.0.1`；程序不修改防火墙、路由器或公网映射。
- 开房、读取当前房间和全部扫描管理端点只接受 loopback 来源。未启用 Forwarded Headers，因此客户端不能用普通请求头伪造来源地址。
- 访客和主持人权限继续使用短期 Bearer 令牌；SQLite 只持久化 SHA-256 哈希。二维码只有房间码，SignalR 令牌通过 `Subscribe` 方法发送而不进入 URL。
- OpenAPI/API 回归测试确认公共曲库、队列和播放契约不含 `RootPath`、`RelativePath` 或 `TokenHash`。真实路径只存在于本机管理 DTO、数据库和播放器适配层。
- 设置与诊断只存当前用户 LocalAppData；导出只报告路径是否配置，不导出路径、令牌或底层异常。受控日志限制字段长度、移除换行并限制文件容量。
- 响应增加 `X-Content-Type-Options: nosniff`、`X-Frame-Options: DENY`、`Referrer-Policy: no-referrer`，API 响应增加 `Cache-Control: no-store`。

## 剩余风险与验收

V1 手机流量是家庭局域网 HTTP，不抵御同一不可信网络中的被动监听。短期、可撤销、房间限定令牌降低影响，但不能替代 TLS；严禁公网暴露。手机实机需确认令牌不出现在地址栏、浏览器历史、二维码和服务日志。依赖供应链风险由 KTVS-053 单独审查。
