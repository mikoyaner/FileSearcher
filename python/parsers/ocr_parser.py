"""OCR 预留接口 — 面向未来的扩展

本模块为后续集成 OCR 功能（PDF 扫描件、图片文字识别）提供接口骨架。
当前所有方法返回空字符串，仅作为架构占位符。

Future Work:
- [ ] 集成 Tesseract OCR (pytesseract)
- [ ] 集成 PaddleOCR (PPOCR) 以获得更好的中文识别精度
- [ ] 集成 pdfplumber / PyMuPDF 提取 PDF 文本层
- [ ] 集成 pdf2image + OCR 处理扫描版 PDF
"""

from parsers.base import BaseParser


class OcrParser(BaseParser):
    """OCR 解析器（预留）

    目标支持格式：
    - 扫描件 PDF (.pdf)
    - 常见图片格式 (.png, .jpg, .jpeg, .tiff, .bmp)
    """

    def extract_text(self, filepath: str) -> str:
        """OCR 文本提取（当前返回空字符串）

        待实现：
        1. 检测文件类型
        2. PDF 扫描件：pdf2image 转图片 → OCR
        3. 图片：直接 OCR
        4. 返回识别后的文本

        Args:
            filepath: 文件路径

        Returns:
            当前返回空字符串
        """
        return ''

    def get_supported_extensions(self) -> list:
        """返回计划支持的扩展名列表"""
        return ['.pdf', '.png', '.jpg', '.jpeg', '.tiff', '.bmp']