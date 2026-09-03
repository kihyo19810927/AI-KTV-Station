# 持续集成

`.github/workflows/ci.yml` 在 Windows runner 上执行锁定依赖恢复、格式验证、Release 构建、xUnit/coverlet 和 Web 构建，并上传 Cobertura 报告。权限限制为只读仓库内容，不发布、不推送。

手机 Web 的正式 React/Vite 工程由 KTVS-034 建立；在此之前 CI 明确报告延后，而不是伪造前端测试。首次推送前只能验证工作流语法和对应本地命令，GitHub runner 结果标记为待外部验证。
