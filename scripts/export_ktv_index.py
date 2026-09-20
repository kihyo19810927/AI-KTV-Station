#!/usr/bin/env python3
"""在 NAS 本地挂载目录中生成 AI-KTV 曲库索引。

只读取媒体文件名、相对路径和文件大小，不上传、删除或修改媒体文件。
默认只扫描 16年 至 25年，并明确跳过 2008-2015年。
Python 3.8+，无第三方依赖。
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import tempfile
import unicodedata
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


DEFAULT_YEARS = [f"{year}年" for year in range(16, 26)]
DEFAULT_EXCLUDED_DIRECTORIES = {"2008-2015年"}
DEFAULT_EXTENSIONS = {".mkv"}
LANGUAGES = {
    "国语",
    "粤语",
    "英语",
    "日语",
    "韩语",
    "闽南语",
    "客家语",
    "纯音乐",
}
CATEGORIES = {
    "流行",
    "流行歌曲",
    "合唱",
    "儿歌",
    "民歌",
    "摇滚",
    "经典",
    "舞曲",
    "DJ",
    "古典",
    "轻音乐",
    "影视",
    "电子",
    "朋克",
    "嘻哈",
    "民谣",
}
SEPARATOR_RE = re.compile(r"\s*[-－–—]\s*")


def parse_song_name(file_name: str) -> Dict[str, Optional[str]]:
    stem = unicodedata.normalize("NFKC", Path(file_name).stem).strip()
    parts = [part.strip() for part in SEPARATOR_RE.split(stem) if part.strip()]

    language: Optional[str] = None
    category: Optional[str] = None
    while len(parts) > 1:
        last = parts[-1]
        if language is None and last in LANGUAGES:
            language = parts.pop()
            continue
        if category is None and last in CATEGORIES:
            category = parts.pop()
            continue
        break

    core = "-".join(parts).strip()
    separator = core.find("-")
    if 0 < separator < len(core) - 1:
        artist = core[:separator].strip()
        title = core[separator + 1 :].strip()
    else:
        artist = "未知歌手"
        title = core or stem

    return {
        "artist": artist,
        "title": title,
        "language": language,
        "category": category,
    }


def iter_media_files(
    root: Path,
    year_names: Sequence[str],
    excluded_directories: set[str],
    extensions: set[str],
) -> Iterable[Tuple[Path, str]]:
    for year_name in year_names:
        year_path = root / year_name
        if not year_path.is_dir():
            print(f"警告：跳过不存在的年度目录：{year_name}", file=sys.stderr)
            continue

        print(f"扫描 {year_name} ...")
        for current, dir_names, file_names in os.walk(
            year_path, topdown=True, followlinks=False
        ):
            # 在进入子目录前排除，确保不会读取仍在上传的旧年份目录。
            dir_names[:] = sorted(
                name for name in dir_names if name not in excluded_directories
            )
            current_path = Path(current)
            for file_name in sorted(file_names):
                path = current_path / file_name
                if path.suffix.lower() not in extensions:
                    continue
                yield path, year_name


def build_records(
    root: Path,
    year_names: Sequence[str],
    excluded_directories: set[str],
    extensions: set[str],
) -> List[Dict[str, object]]:
    records: List[Dict[str, object]] = []
    errors: List[str] = []

    for path, year_name in iter_media_files(
        root, year_names, excluded_directories, extensions
    ):
        try:
            relative = path.relative_to(root).as_posix()
            stat = path.stat()
            metadata = parse_song_name(path.name)
            records.append(
                {
                    "relativePath": relative,
                    "fileName": path.name,
                    "extension": path.suffix.lower(),
                    "sizeBytes": stat.st_size,
                    "yearFolder": year_name,
                    "artist": metadata["artist"],
                    "title": metadata["title"],
                    "language": metadata["language"],
                    "category": metadata["category"],
                    "canonicalFileName": path.name,
                    "durationMs": None,
                    "probeStatus": "NotProbed",
                }
            )
            if len(records) % 500 == 0:
                print(f"已发现 {len(records)} 首")
        except OSError as exc:
            errors.append(f"{path}: {exc}")

    if errors:
        print("扫描存在错误，未写出不完整索引：", file=sys.stderr)
        for error in errors:
            print(f"  {error}", file=sys.stderr)
        raise RuntimeError("请确认 NAS 挂载在线、目录权限正常后重试。")

    records.sort(key=lambda item: str(item["relativePath"]))
    if not records:
        raise RuntimeError("没有找到可导出的媒体文件。")
    return records


def atomic_write(path: Path, write_func) -> None:
    path = path.expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_name: Optional[str] = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".tmp",
            delete=False,
        ) as stream:
            temporary_name = stream.name
            write_func(stream)
        os.replace(temporary_name, path)
        temporary_name = None
    finally:
        if temporary_name:
            try:
                os.unlink(temporary_name)
            except FileNotFoundError:
                pass


def write_jsonl(path: Path, records: Sequence[Dict[str, object]]) -> None:
    def write(stream) -> None:
        for record in records:
            stream.write(
                json.dumps(record, ensure_ascii=False, separators=(",", ":"))
                + "\n"
            )

    atomic_write(path, write)


def write_json(path: Path, records: Sequence[Dict[str, object]]) -> None:
    def write(stream) -> None:
        json.dump(records, stream, ensure_ascii=False, indent=2)
        stream.write("\n")

    atomic_write(path, write)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="在 NAS 本地挂载目录生成 AI-KTV JSONL/JSON 曲库索引"
    )
    parser.add_argument(
        "--mount-root",
        required=True,
        help="NAS 上 115 挂载的 KTV_TEST 目录，例如 /volume1/115open/KTV_TEST",
    )
    parser.add_argument(
        "--output-jsonl",
        default="./ktv_songs_index.jsonl",
        help="输出 JSONL 路径，默认写到当前目录",
    )
    parser.add_argument(
        "--output-json",
        default="./ktv_songs_index.json",
        help="输出 JSON 数组路径，默认写到当前目录",
    )
    parser.add_argument(
        "--year",
        dest="years",
        action="append",
        help="指定要扫描的年度目录，可重复传入；默认扫描 16年-25年",
    )
    parser.add_argument(
        "--exclude-dir",
        dest="excluded_directories",
        action="append",
        default=[],
        help="额外排除的目录名；2008-2015年始终默认排除",
    )
    parser.add_argument(
        "--extension",
        dest="extensions",
        action="append",
        help="媒体扩展名，可重复传入；默认只扫描 .mkv",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = Path(args.mount_root).expanduser().resolve()
    if not root.is_dir():
        print(f"错误：NAS 挂载目录不存在或不可访问：{root}", file=sys.stderr)
        return 2

    years = args.years or DEFAULT_YEARS
    excluded = DEFAULT_EXCLUDED_DIRECTORIES | set(args.excluded_directories)
    extensions = {
        value.lower() if value.startswith(".") else f".{value.lower()}"
        for value in (args.extensions or sorted(DEFAULT_EXTENSIONS))
    }

    try:
        records = build_records(root, years, excluded, extensions)
        write_jsonl(Path(args.output_jsonl), records)
        write_json(Path(args.output_json), records)
    except (OSError, RuntimeError) as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 1

    print(f"完成：{len(records)} 首")
    print(f"JSONL：{Path(args.output_jsonl).expanduser().resolve()}")
    print(f"JSON ：{Path(args.output_json).expanduser().resolve()}")
    print("已排除：2008-2015年（不会读取该目录）")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
