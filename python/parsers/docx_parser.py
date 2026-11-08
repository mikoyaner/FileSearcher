"""Word (.docx) 文件解析器

使用 python-docx 遍历 Paragraphs 和 Tables 提取文本。
"""

from parsers.base import BaseParser


class DocxParser(BaseParser):
    """.docx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        try:
            from docx import Document
            doc = Document(filepath)
            text_parts = []

            # 遍历段落
            for para in doc.paragraphs:
                if para.text.strip():
                    text_parts.append(para.text)

            # 遍历表格
            for table in doc.tables:
                for row in table.rows:
                    for cell in row.cells:
                        if cell.text.strip():
                            text_parts.append(cell.text)

            return '\n'.join(text_parts)
        except Exception:
            return ''