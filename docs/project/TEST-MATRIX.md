# 测试矩阵

| 范围 | 方法 | 当前状态 | 阻塞 |
|---|---|---|---|
| 文档边界/术语 | 人工审阅、链接检查 | 可执行 | 无 |
| .NET 领域单元测试 | xUnit | 未开始 | .NET SDK |
| Host/API/SignalR | 集成测试 | 未开始 | .NET SDK |
| mpv IPC | 可控进程/协议替身 + 实机 Spike | 未开始 | .NET SDK、mpv |
| ffprobe 解析 | 固定 JSON 样本 | 未开始 | 无（实现阶段） |
| React Web | npm build/test | 未开始 | Web 工程尚未建立 |
| 真实 MKV/设备 | 用户验收步骤 | 待验证 | 用户设备、样本和挂载 |

禁止把替身、模拟器或文档审阅结果写成真实设备通过。
