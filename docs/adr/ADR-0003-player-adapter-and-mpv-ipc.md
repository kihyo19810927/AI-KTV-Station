# ADR-0003：播放器适配层与 mpv IPC

状态：Accepted  
日期：2026-09-03

## 决策

- Application 只通过 `IPlayerAdapter` 发出加载、暂停、继续、停止、定位、音量、音轨和字幕命令，并接收状态与事件。
- Infrastructure 使用 mpv 子进程和 Windows named-pipe JSON IPC；每个请求使用唯一 `request_id` 关联响应。
- 适配器负责启动超时、命令超时、进程退出、协议错误和事件去重，不把原始 mpv JSON 泄漏到业务层。
- 媒体路径只在主机内部传递，不出现在访客 API、SignalR 消息或普通日志中。
- 播放完成与失败必须转换为有标识的领域事件，编排器保证同一队列项只推进一次。

## 依据

KTVS-003 已用生成的双音轨/字幕 MKV 验证 named-pipe IPC、状态读取、切轨和结束事件。真实设备兼容性仍按实机清单验收。
