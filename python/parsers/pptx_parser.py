"""PowerPoint (.pptx) 文件解析器

使用 python-pptx 遍历 Slides → Shapes → TextFrames 提取文本。
"""

from parsers.base import BaseParser


class PptxParser(BaseParser):
    """.pptx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        try:
            from pptx import Presentation
            prs = Presentation(filepath)
            text_parts = []

            for slide in prs.slides:
                for shape in slide.shapes:
                    if shape.has_text_frame:
                        for para in shape.text_frame.paragraphs:
                            if para.text.strip():
                                text_parts.append(para.text)
                    # 表格中的文本
                    if shape.has_table:
                        table = shape.table
                        for row in table.rows:
                            for cell in row.cells:
                                if cell.text.strip():
                                    text_parts.append(cell.text)

            return '\n'.join(text_parts)
        except Exception:
            return ''