# 本机公共运行时目录

此目录仅用于当前电脑的外部媒体工具，不随 Git 或发布 ZIP 分发。

预期布局：

```text
common/
  mpv/
    mpv.exe
    vulkan-1.dll
  ffmpeg/
    ffmpeg.exe
    ffprobe.exe
```

AI-KTV Station 优先从这里查找工具，再回退到 `PATH` 和 WinGet 的 mpv 安装目录。设置页填写的 mpv 绝对路径仍具有最高优先级。

Station 不依赖 Python，因此这里不复制 Python。测试媒体生成脚本需要 `ffmpeg`，媒体索引运行时只调用 `ffprobe`，播放运行时只调用 `mpv`。

这些二进制来自本机独立安装，仅供本机验收。不要提交、上传或复制进正式发布包。
