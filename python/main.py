#!/usr/bin/env python3
"""本地文档内容检索工具

在海量混合格式文件中快速定位包含特定关键词的文件，
并展示关键词前后20字符的上下文片段。

用法:
    python main.py <搜索路径> <关键词> [选项]

示例:
    python main.py D:/文档 预算
    python main.py ./src 函数 --type .py,.js --output-dir ./results -v
    python main.py C:/数据 报告 --no-xlsx
"""

import argparse
import os
import sys

# 将项目根目录加入 sys.path，确保模块导入正常工作
PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from core.searcher import search_files
from core.result_writer import print_results, save_results


def parse_args() -> argparse.Namespace:
    """解析命令行参数"""
    parser = argparse.ArgumentParser(
        description='本地文档内容检索工具 - 快速定位包含关键词的文件',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python main.py D:/文档 预算
  python main.py ./src 函数 --type .py,.js --output-dir ./results -v
  python main.py C:/数据 报告 --no-xlsx --max-rows 10
        """,
    )

    parser.add_argument(
        'search_path',
        help='要搜索的目录路径',
    )
    parser.add_argument(
        'keyword',
        help='搜索关键词',
    )
    parser.add_argument(
        '--type',
        dest='file_types',
        default='',
        help='文件类型过滤，逗号分隔（默认全部支持格式）\n'
             '例如: --type .txt,.docx,.xlsx',
    )
    parser.add_argument(
        '--output-dir',
        dest='output_dir',
        default='.',
        help='结果文件输出目录（默认当前目录）',
    )
    parser.add_argument(
        '--no-csv',
        action='store_true',
        help='不保存 CSV 结果文件',
    )
    parser.add_argument(
        '--no-xlsx',
        action='store_true',
        help='不保存 XLSX 结果文件',
    )
    parser.add_argument(
        '--max-rows',
        type=int,
        default=50,
        help='控制台最大显示行数（默认 50，0 为全部显示）',
    )
    parser.add_argument(
        '--max-depth',
        type=int,
        default=-1,
        help='最大递归深度（默认无限制）',
    )
    parser.add_argument(
        '-v', '--verbose',
        action='store_true',
        help='显示详细处理日志',
    )

    return parser.parse_args()


def main():
    """主入口"""
    args = parse_args()

    # 验证搜索路径
    search_path = args.search_path
    if not os.path.isdir(search_path):
        print(f"错误: 搜索路径不存在或不是目录: {search_path}")
        sys.exit(1)

    # 处理文件类型过滤
    include_extensions = None
    if args.file_types:
        include_extensions = set()
        for ext in args.file_types.split(','):
            ext = ext.strip().lower()
            if not ext.startswith('.'):
                ext = '.' + ext
            include_extensions.add(ext)

    # 执行搜索
    print(f"\n{'='*60}")
    print(f"开始搜索...")
    print(f"  搜索路径: {search_path}")
    print(f"  关键词: '{args.keyword}'")
    if include_extensions:
        print(f"  文件类型: {', '.join(sorted(include_extensions))}")
    print(f"{'='*60}\n")

    results = search_files(
        root_dir=search_path,
        keyword=args.keyword,
        include_extensions=include_extensions,
        max_depth=args.max_depth,
        verbose=args.verbose,
    )

    # 输出结果
    print_results(results, max_rows=args.max_rows)

    # 保存文件
    if results:
        save_results(
            results,
            output_dir=args.output_dir,
            save_xlsx=not args.no_xlsx,
            save_csv=not args.no_csv,
        )


if __name__ == '__main__':
    main()