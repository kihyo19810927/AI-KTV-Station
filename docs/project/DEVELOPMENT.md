# 本地开发

要求：Windows 11、.NET SDK 10.0.400、Node 24、npm 11。PowerShell 中使用 `npm.cmd`/`npx.cmd`。

```powershell
# 检查工具并按锁文件恢复依赖
pwsh.exe -File .\scripts\bootstrap.ps1

# 格式校验、Release 构建、依赖边界检查
pwsh.exe -File .\scripts\build.ps1 -NoRestore

# xUnit 与 Cobertura 覆盖率
pwsh.exe -File .\scripts\test.ps1 -NoRestore

# Server 启动烟测
pwsh.exe -File .\scripts\smoke-test-server.ps1

# 启动本地 Server 或 WPF 主控
pwsh.exe -File .\scripts\run.ps1 -Target Server
pwsh.exe -File .\scripts\run.ps1 -Target Desktop
```

测试媒体使用 `scripts/generate-test-media.ps1` 生成到忽略目录，不提交二进制。所有真实路径和凭据通过未入库的本机配置提供。
