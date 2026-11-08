"""纯文本文件解析器

支持格式：.txt, .csv, .md, .json, .xml, .log, .yaml, .yml, .ini, .cfg, .conf, .bat, .sh
使用 chardet 自动检测编码，兼容 GBK/UTF-8/BOM 等。
"""

from parsers.base import BaseParser
from utils.encoding_detector import read_text_file_safe


class TextParser(BaseParser):
    """纯文本文件解析器"""

    def extract_text(self, filepath: str) -> str:
        try:
            return read_text_file_safe(filepath)
        except Exception:
            return ''