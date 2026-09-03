# 自动化测试

## 本地命令

首次运行或锁文件变化后：

```powershell
pwsh.exe -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

依赖已恢复时：

```powershell
pwsh.exe -ExecutionPolicy Bypass -File .\scripts\test.ps1 -NoRestore
```

脚本发现 `tests/**/*.csproj`，逐项目运行 Release xUnit，并用 coverlet 生成 Cobertura 覆盖率到忽略的 `TestResults/`。测试数据通过代码工厂生成；不得依赖真实曲库、凭据或本机绝对媒体路径。

当前 Windows 沙箱会限制 testhost/MSBuild 命名管道，Codex 环境中可能需授权本地主机执行；普通开发 PowerShell 不受该产品沙箱限制。
