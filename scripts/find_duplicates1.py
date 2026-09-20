import json
import sys
from collections import defaultdict
import csv
import os

def main():
    if len(sys.argv) != 2:
        print("用法: python find_dupes_csv.py <input.jsonl>")
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = "duplicates.csv"

    groups = defaultdict(list)
    total_files = 0

    try:
        # 读取并解析 JSONL 文件
        with open(input_file, "r", encoding="utf-8") as f:
            for line_num, line in enumerate(f, 1):
                line = line.strip()
                if not line:
                    continue

                try:
                    obj = json.loads(line)

                    # 提取需要的字段
                    fname = obj.get("fileName")
                    size = obj.get("sizeBytes")
                    path = obj.get("relativePath")

                    if fname and size is not None and path:
                        groups[(fname, size)].append(path)
                        total_files += 1

                except json.JSONDecodeError:
                    print(f"⚠️ 第 {line_num} 行 JSON 解析失败，已跳过")
                    continue

    except FileNotFoundError:
        print(f"❌ 找不到文件: {input_file}")
        sys.exit(1)

    # 筛选重复项
    duplicates = {k: v for k, v in groups.items() if len(v) > 1}

    # 写入 CSV
    with open(output_file, "w", encoding="utf-8-sig", newline='') as f:
        writer = csv.writer(f)
        writer.writerow(["FileName", "SizeBytes", "Count", "Paths"])
        for (fname, size), paths in duplicates.items():
            writer.writerow([
                fname,
                size,
                len(paths),
                " | ".join(paths)
            ])

    print(f"✅ 处理完成! 重复文件已保存至: {os.path.abspath(output_file)}")
    print(f"📊 总文件数: {total_files}")
    print(f"🔁 重复组数: {len(duplicates)}")
    print(f"📁 重复文件总数: {sum(len(v) for v in duplicates.values())}")

if __name__ == "__main__":
    main()
