# 手机 Web 兼容矩阵

| 平台 | 最低基线 | 自动门禁 | 实机状态 |
| --- | --- | --- | --- |
| Android Chrome | Chrome 120 | Vite `chrome120` 目标、320px CSS、触摸与焦点静态断言 | 待验收 |
| Windows/Android Edge | Edge 120 | Vite `edge120` 目标 | 待验收 |
| iPhone Safari | Safari 16.4 | Vite `safari16.4` 目标、viewport-fit 与 safe-area | 待验收 |

版本基线用于 V1 编译兼容范围，不声称已在所有具体机型通过。

## 已实现约束

- 布局从 320px 起可用，窄于 371px 时四列筛选自动改为两列。
- 主要图标按钮最小触摸区为 44×44 CSS px，底部导航保留安全区。
- 所有表单有可访问名称，状态/错误分别使用 `role=status`、`role=alert`。
- 键盘焦点使用高对比 `:focus-visible`；减少动态效果和强制颜色模式有降级规则。
- HTML 声明简体中文、移动 viewport、主题色与明暗配色。

## 实机验收

分别在 Android Chrome 和 iPhone Safari 执行：扫码加入、软键盘搜索、四项筛选、连续点歌、收藏、切换底部页签、锁屏/唤醒及 Wi-Fi 短断线恢复。确认无横向滚动、按钮可触、刘海/底部手势区无遮挡，结果记录到发布检查清单。
