"""解析器注册表（工厂模式）

维护扩展名到解析器类的映射，新增文件格式只需在此注册。
"""

from typing import Dict, Type
from parsers.base import BaseParser
from parsers.text_parser import TextParser
from parsers.docx_parser import DocxParser
from parsers.xlsx_parser import XlsxParser
from parsers.pptx_parser import PptxParser
from parsers.legacy_parser import LegacyParser
from parsers.ocr_parser import OcrParser

# 解析器注册表
PARSER_REGISTRY: Dict[str, Type[BaseParser]] = {
    # 纯文本类
    '.txt': TextParser,
    '.csv': TextParser,
    '.md': TextParser,
    '.json': TextParser,
    '.xml': TextParser,
    '.log': TextParser,
    '.yaml': TextParser,
    '.yml': TextParser,
    '.ini': TextParser,
    '.cfg': TextParser,
    '.conf': TextParser,
    '.bat': TextParser,
    '.sh': TextParser,
    # Office OpenXML 类
    '.docx': DocxParser,
    '.xlsx': XlsxParser,
    '.pptx': PptxParser,
    # 旧版 Office 二进制类
    '.doc': LegacyParser,
    '.xls': LegacyParser,
    '.ppt': LegacyParser,
    # OCR 预留（Future Work）
    # '.pdf': OcrParser,  # PDF 扫描件 OCR
    # '.png': OcrParser,  # 图片 OCR（蓝图）
    # '.jpg': OcrParser,
    # '.jpeg': OcrParser,
    # '.tiff': OcrParser,
    # '.bmp': OcrParser,
}


def get_parser(filepath: str) -> BaseParser:
    """根据文件路径获取对应的解析器实例

    Args:
        filepath: 文件路径

    Returns:
        解析器实例

    Raises:
        ValueError: 不支持的文件格式
    """
    import os
    ext = os.path.splitext(filepath)[1].lower()
    parser_class = PARSER_REGISTRY.get(ext)
    if parser_class is None:
        raise ValueError(f"不支持的文件格式: {ext}")
    return parser_class()


__all__ = [
    'BaseParser', 'PARSER_REGISTRY', 'get_parser',
    'TextParser', 'DocxParser', 'XlsxParser', 'PptxParser',
    'LegacyParser', 'OcrParser',
]