"""编码自动检测模块

封装 chardet，兼容 GBK/UTF-8/BOM/Shift-JIS 等多种编码，
防止中文乱码。
"""

import chardet


def detect_encoding(filepath: str, read_size: int = 65536) -> str:
    """自动检测文件编码

    Args:
        filepath: 文件路径
        read_size: 用于检测的字节数（默认 64KB）

    Returns:
        检测到的编码名称，如 'utf-8', 'gbk', 'shift-jis' 等
        如果检测失败，返回 'utf-8' 作为 fallback
    """
    try:
        with open(filepath, 'rb') as f:
            raw_data = f.read(read_size)

        result = chardet.detect(raw_data)
        encoding = result.get('encoding', 'utf-8')

        # 统一编码名称
        if encoding:
            encoding = encoding.lower().replace('-', '')
            if encoding in ('utf8', 'utf-8', 'ascii'):
                encoding = 'utf-8'
            elif encoding in ('gb2312', 'gbk', 'gb18030', 'gbk2312'):
                encoding = 'gbk'
            elif encoding in ('shift_jis', 'shift-jis', 'sjis'):
                encoding = 'shift-jis'
            elif encoding in ('big5', 'big5hkscs'):
                encoding = 'big5'

        return encoding or 'utf-8'

    except Exception:
        return 'utf-8'


def read_text_file_safe(filepath: str) -> str:
    """安全读取文本文件（自动编码检测 + 降级尝试）

    先用 chardet 检测编码，如果解码失败则尝试常见编码降级。

    Args:
        filepath: 文件路径

    Returns:
        文件文本内容

    Raises:
        UnicodeDecodeError: 所有编码尝试均失败
    """
    encoding = detect_encoding(filepath)

    try:
        with open(filepath, 'r', encoding=encoding) as f:
            return f.read()
    except (UnicodeDecodeError, LookupError):
        # 降级尝试常见编码
        fallback_encodings = ['utf-8', 'gbk', 'gb18030', 'big5', 'shift-jis', 'latin-1']
        for enc in fallback_encodings:
            if enc == encoding:
                continue
            try:
                with open(filepath, 'r', encoding=enc) as f:
                    return f.read()
            except (UnicodeDecodeError, LookupError):
                continue
        # 最后尝试 latin-1（不会失败，但可能乱码）
        with open(filepath, 'r', encoding='latin-1') as f:
            return f.read()