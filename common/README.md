# 本机公共运行时目录

此目录保存当前电脑的外部媒体工具。发布脚本会在工具文件存在且许可证材料齐备时，将它们复制到发布 ZIP 的 `tools/` 目录；Git 仍忽略二进制。

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

AI-KTV Station 优先从程序内置 `tools/`、仓库 `common/` 查找工具，再回退到 `PATH` 和 WinGet 的 mpv 安装目录。设置页不再要求用户输入工具路径。

Station 不依赖 Python，因此这里不复制 Python。测试媒体生成脚本需要 `ffmpeg`，媒体索引运行时只调用 `ffprobe`，播放运行时只调用 `mpv`。

发布前必须核对 `common/licenses/` 中的版本和许可证材料，并由发布者确认对应二进制的源码提供义务。
