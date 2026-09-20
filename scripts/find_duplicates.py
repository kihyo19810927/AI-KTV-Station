import json
import sys
from collections import defaultdict

def find_duplicates(file_path):
    # 用字典存储：键为 (fileName, sizeBytes)，值为该文件在所有地方出现的 relativePath 列表
    file_map = defaultdict(list)
    total_records = 0

    print(f"正在读取并解析: {file_path} ...")

    # 按行流式读取，不会爆内存
    with open(file_path, "r", encoding="utf-8") as f:
        for line_num, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                item = json.loads(line)
                file_name = item.get("fileName")
                size_bytes = item.get("sizeBytes")
                rel_path = item.get("relativePath", "")

                # 仅在文件名和大小都存在时进行统计
                if file_name is not None and size_bytes is not None:
                    file_map[(file_name, size_bytes)].append(rel_path)
                    total_records += 1
            except json.JSONDecodeError:
                # 兼容可能存在的格式异常行
                continue

    # 筛选出重复的项目（出现次数 >= 2）
    duplicates = {k: paths for k, paths in file_map.items() if len(paths) > 1}

    # 统计数据
    duplicate_groups_count = len(duplicates)  # 存在重复的不同文件数
    duplicate_extra_count = sum(len(paths) - 1 for paths in duplicates.values()) # 多出来的冗余文件总数

    print("\n" + "=" * 50)
    print("【统计结果】")
    print(f"总读取有效歌曲记录数: {total_records}")
    print(f"存在重复的文件种数 (唯一标识相同): {duplicate_groups_count} 组")
    print(f"产生的冗余/多余记录总数: {duplicate_extra_count} 条")
    print("=" * 50 + "\n")

    # 打印前 10 组重复文件作为参考
    if duplicates:
        print("--- 部分重复文件明细 (最多展示前 10 组) ---")
        for idx, ((fname, size), paths) in enumerate(duplicates.items(), 1):
            size_mb = size / (1024 * 1024)
            print(f"\n[{idx}] 文件名: {fname} (大小: {size_mb:.2f} MB / {size} 字节)")
            print(f"    重复次数: {len(paths)} 次")
            print("    分布路径:")
            for p in paths:
                print(f"      - {p}")
            if idx >= 10:
                print(f"\n... 其余 {duplicate_groups_count - 10} 组重复记录已省略 ...")
                break
    else:
        print("未发现文件名和大小完全相同的重复文件。")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("用法: python find_duplicates.py <input.jsonl>")
        raise SystemExit(1)
    find_duplicates(sys.argv[1])
