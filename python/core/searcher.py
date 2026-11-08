"""搜索调度器

核心编排逻辑：
1. 接收搜索路径和关键词
2. 扫描文件列表
3. 对每个文件选择对应解析器提取文本
4. 在文本中搜索关键词并提取上下文
5. 聚合结果
"""

import os
import time
import sys
from typing import List, Dict, Optional

# 确保项目根目录在 sys.path 中
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from utils.file_scanner import scan_files, classify_file
from parsers import get_parser
from .context_extractor import extract_context, count_matches


def search_in_text(text: str, keyword: str) -> tuple:
    """在已提取的文本中搜索关键词

    Args:
        text: 已提取的文件文本内容
        keyword: 要搜索的关键词

    Returns:
        (match_count, first_snippet) 元组
        如果未匹配，match_count 为 0，first_snippet 为空字符串
    """
    if not text or not keyword:
        return 0, ''

    match_count = count_matches(text, keyword)
    if match_count == 0:
        return 0, ''

    snippets = extract_context(text, keyword)
    first_snippet = snippets[0][0] if snippets else ''

    return match_count, first_snippet


def search_files(
    root_dir: str,
    keyword: str,
    include_extensions: Optional[set] = None,
    exclude_dirs: Optional[set] = None,
    max_depth: int = -1,
    verbose: bool = False,
    progress_callback=None,
) -> List[Dict]:
    """在指定目录的所有支持文件中搜索关键词

    Args:
        root_dir: 根目录路径
        keyword: 要搜索的关键词
        include_extensions: 包含的文件扩展名集合（默认全部支持格式）
        exclude_dirs: 排除的目录名集合
        max_depth: 最大递归深度，-1 表示无限制
        verbose: 是否输出详细日志
        progress_callback: 进度回调函数，签名为 callback(current, total, filepath)

    Returns:
        dict 列表，每个元素包含：
        - file_path: 文件绝对路径
        - file_type: 文件扩展名
        - keyword: 搜索关键词
        - snippet: 上下文片段
        - match_count: 匹配次数
    """
    # 扫描文件
    if verbose:
        print(f"正在扫描目录: {root_dir}")

    files = scan_files(root_dir, include_extensions, exclude_dirs, max_depth)
    total_files = len(files)

    if verbose:
        print(f"发现 {total_files} 个支持的文件")

    results = []
    processed = 0
    start_time = time.time()

    for filepath in files:
        processed += 1

        if progress_callback:
            progress_callback(processed, total_files, filepath)

        if verbose and processed % 50 == 0:
            elapsed = time.time() - start_time
            print(f"  进度: {processed}/{total_files} ({elapsed:.1f}s) - 当前: {os.path.basename(filepath)}")

        try:
            # 获取解析器并提取文本
            parser = get_parser(filepath)
            text = parser.extract_text(filepath)

            if not text:
                continue

            # 搜索关键词
            match_count, snippet = search_in_text(text, keyword)

            if match_count > 0:
                results.append({
                    'file_path': filepath,
                    'file_type': os.path.splitext(filepath)[1].lower(),
                    'keyword': keyword,
                    'snippet': snippet,
                    'match_count': match_count,
                })

        except ValueError:
            # 不支持的文件格式，跳过
            continue
        except Exception as e:
            if verbose:
                print(f"  [警告] 解析失败: {filepath} - {e}")
            continue

    # 按匹配次数降序排序
    results.sort(key=lambda x: x['match_count'], reverse=True)

    elapsed = time.time() - start_time
    if verbose:
        print(f"\n搜索完成！耗时: {elapsed:.1f}s")
        print(f"扫描文件: {total_files}, 命中文件: {len(results)}")

    return results