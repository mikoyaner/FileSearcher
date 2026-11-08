"""上下文截取算法

在文本中定位关键词，提取前后约20字节的上下文片段。
正确处理多字节字符（中文），防止索引越界。
"""

import re
from typing import List, Tuple


def extract_context(
    text: str,
    keyword: str,
    context_bytes: int = 20,
) -> List[Tuple[str, int]]:
    """在文本中查找关键词并提取上下文片段

    算法步骤：
    1. 使用正则（re.escape 转义）在文本中查找所有不重叠的关键词出现位置
    2. 对每个匹配位置，计算起止索引并进行安全切片
    3. 清洗片段中的空白字符
    4. 对片段添加标记突出关键词

    Args:
        text: 待搜索的文本（已提取的纯文本）
        keyword: 要搜索的关键词
        context_bytes: 上下文截取长度（前后各取多少字符，默认 20）

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
        start = max(0, index - context_bytes)
        end = min(len(text), kw_end + context_bytes)

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


def count_matches(text: str, keyword: str) -> int:
    """统计关键词在文本中的不重叠出现次数

    Args:
        text: 待搜索的文本
        keyword: 关键词

    Returns:
        匹配次数
    """
    if not text or not keyword:
        return 0
    pattern = re.compile(re.escape(keyword), re.IGNORECASE)
    return len(pattern.findall(text))