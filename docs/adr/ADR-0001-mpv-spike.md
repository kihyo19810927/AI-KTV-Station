# ADR-0001：mpv JSON IPC Spike 边界

状态：Accepted（KTVS-003 软件 Spike 已验证；真实设备仍待验收）  
日期：2026-09-03

## 决策

以独立 `Station.PlayerSpike` 控制 mpv 子进程；业务只接触未来的 `IPlayerAdapter`，本 Spike 不引入 WPF、ASP.NET 或数据库。测试媒体由 FFmpeg 在 `tests/fixtures` 运行时生成，Unicode 路径覆盖文件传递边界。

## 取舍

Spike 使用 Windows named pipe JSON IPC，具备 request_id 响应关联、事件读取、10 秒请求超时和进程退出处理。生产实现仍需在 KTVS-020/021 中抽取正式 `IPlayerAdapter` 与更完整的状态机。测试夹具不提交二进制。
