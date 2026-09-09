# 主控健康仪表盘

KTVS-042 在 WPF 仪表盘展示点歌服务、SQLite、媒体挂载和 mpv 四类健康状态。`IStationHealthService` 位于 Application 层，只返回安全等级和摘要；Infrastructure 负责实际探测，不向 UI 暴露路径、连接串或底层异常。

SQLite 使用连通性检查；媒体挂载根据已启用媒体源的持久化可用状态汇总，离线只显示状态，不修改或删除索引；mpv 检查显式配置、PATH 与 WinGet 常见目录。内嵌 Web 服务将在 KTVS-048 建立生命周期，在此之前明确显示待接入而非伪报正常。

真实 CloudDrive/115 挂载、Windows 视觉布局和断网恢复仍待实机验收，不阻塞后续软件任务。
