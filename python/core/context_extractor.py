"""上下文截取算法

在文本中定位关键词，提取前后约20个字符的上下文片段。
正确处理多字节字符（中文），防止索引越界。

PY7 修复记录：参数 context_bytes 更名为 context_chars —— 原实现按"字符数"截取
（Python 字符串切片天然按字符），参数名却叫 bytes，语义误导。
PY4 修复记录：新增 extract_first_context —— 单次正则扫描同时完成
"计数 + 限量片段提取"。此前搜索器为每个文件先 count_matches（全量 findall）、
再 extract_context（为全部匹配生成片段、却只取第一条）双重全量扫描，
大文件高频词时产生大量即抛的中间字符串。
"""

import re
from typing import List, Tuple


def extract_context(
    text: str,
    keyword: str,
    context_chars: int = 20,
) -> List[Tuple[str, int]]:
    """在文本中查找关键词并提取全部上下文片段

    算法步骤：
    1. 使用正则（re.escape 转义）在文本中查找所有不重叠的关键词出现位置
    2. 对每个匹配位置，计算起止索引并进行安全切片
    3. 清洗片段中的空白字符
    4. 对片段添加标记突出关键词

    Args:
        text: 待搜索的文本（已提取的纯文本）
        keyword: 要搜索的关键词
        context_chars: 上下文截取长度（前后各取多少字符，默认 20）

    Returns:
        (snippet, index) 列表，每个元素为一个匹配的上下文片段及其在文本中的位置
        按出现顺序排列
    """
    if not text or not keyword:
        return []

    results = []
    # 使用正则查找所有匹配位置（不重叠）
    pattern = re.compile(re.escape(keyword), re.IGNORECASE)

    for match in pattern.finditer(text):
        index = match.start()
        kw_end = match.end()

        # 计算上下文的起止索引（安全切片）
        start = max(0, index - context_chars)
        end = min(len(text), kw_end + context_chars)

        # 提取片段
        snippet = text[start:end]

        # 清洗：替换换行/回车/制表符为空格
        snippet = snippet.replace('\n', ' ').replace('\r', ' ').replace('\t', ' ')
        # 去除首尾空白
        snippet = snippet.strip()

        # 如果片段不是从文本开头开始，加前缀 "..."
        if start > 0:
            snippet = '...' + snippet
        # 如果片段不是到文本末尾结束，加后缀 "..."
        if end < len(text):
            snippet = snippet + '...'

        results.append((snippet, index))

    return results


def extract_first_context(
    text: str,
    keyword: str,
    context_chars: int = 20,
    max_snippets: int = 1,
) -> Tuple[int, List[Tuple[str, int]]]:
    """单次正则扫描：统计全部命中数，并只生成前 max_snippets 条片段（PY4 修复）

    搜索器的实际需求是"总命中数 + 首条片段"。此前该需求由
    count_matches（全量 findall 计数）+ extract_context（为每个匹配都构建片段）
    两次独立全量扫描拼接完成：计数扫描与片段扫描各遍历一次全文，
    且片段构建对第 2 个及以后的匹配全部是即抛的浪费。
    高频关键词 × 大文本（如 50MB 日志搜 "error"）时，浪费量为 O(命中数 × 片段长)。

    Args:
        text: 待搜索的文本
        keyword: 要搜索的关键词
        context_chars: 上下文截取长度（前后各取多少字符）
        max_snippets: 最多生成的片段数（搜索器只需 1 条）

    Returns:
        (match_count, snippets) 元组：总命中数 + 片段列表（最多 max_snippets 条）
    """
    if not text or not keyword:
        return 0, []

    pattern = re.compile(re.escape(keyword), re.IGNORECASE)
    match_count = 0
    snippets: List[Tuple[str, int]] = []

    for match in pattern.finditer(text):
        match_count += 1

        # 片段只构建到 max_snippets 为止，后续匹配仅累加计数
        if len(snippets) >= max_snippets:
            continue

        index = match.start()
        kw_end = match.end()

        start = max(0, index - context_chars)
        end = min(len(text), kw_end + context_chars)

        snippet = text[start:end]
        snippet = snippet.replace('\n', ' ').replace('\r', ' ').replace('\t', ' ')
        snippet = snippet.strip()

        if start > 0:
            snippet = '...' + snippet
        if end < len(text):
            snippet = snippet + '...'

        snippets.append((snippet, index))

    return match_count, snippets


def count_matches(text: str, keyword: str) -> int:
    """统计关键词在文本中的不重叠出现次数

    Args:
        text: 待搜索的文本
        keyword: 要搜索的关键词

    Returns:
        匹配次数
    """
    if not text or not keyword:
        return 0
    pattern = re.compile(re.escape(keyword), re.IGNORECASE)
    return len(pattern.findall(text))
