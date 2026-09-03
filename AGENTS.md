# AI-KTV Station repository rules

- 遵守根目录权威开发计划及 `docs/project/CHARTER.md`。
- 保持 Station 与 AI-KTV-Builder 独立；不得操作真实媒体文件。
- 领域层不得依赖 WPF、ASP.NET、EF Core、mpv 或文件系统。
- 播放业务只能通过 `IPlayerAdapter`；不得散落 mpv IPC。
- 远端文件不可用时标记状态并记录错误，不删除索引。
- 每个 KTVS 任务都必须同步测试、文档、状态并使用包含任务号的本地提交。
- 不提交密钥、Cookie、真实个人数据或本机绝对路径配置。
