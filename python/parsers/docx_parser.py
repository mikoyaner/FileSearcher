"""Word (.docx) 文件解析器

使用 python-docx 遍历段落和表格提取文本。

PY2 修复记录：
- 此前只读 doc.paragraphs + doc.tables：页眉/页脚是 section 下的独立部件
  （HeaderPart/FooterPart），此前完全不读取 → 页眉页脚内容漏检；
  现按 section 遍历页眉/页脚（含首页/奇偶页变体）。
- doc.tables 只返回正文顶层表格，单元格内嵌套表格遗漏 → _collect_tables 递归收集。
"""

from parsers.base import BaseParser


class DocxParser(BaseParser):
    """.docx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        try:
            from docx import Document
            doc = Document(filepath)
            text_parts = []

            # 遍历正文段落（doc.paragraphs 仅含 body 直接子段落，
            # 表格内的段落由 _collect_tables 单独收集）
            for para in doc.paragraphs:
                if para.text.strip():
                    text_parts.append(para.text)

            # 遍历正文表格（含嵌套表格）
            _collect_tables(doc.tables, text_parts)

            # PY2 修复：遍历每个 section 的页眉/页脚及其首页/奇偶页变体。
            # python-docx 中这些属性始终返回容器代理对象（未定义时内容为空），可安全遍历。
            for section in doc.sections:
                header_footer_containers = (
                    section.header, section.first_page_header, section.even_page_header,
                    section.footer, section.first_page_footer, section.even_page_footer,
                )
                for container in header_footer_containers:
                    if container is None:
                        continue
                    for para in container.paragraphs:
                        if para.text.strip():
                            text_parts.append(para.text)
                    _collect_tables(container.tables, text_parts)

            return '\n'.join(text_parts)
        except Exception:
            # 解析失败返回空字符串，由搜索器按"无可提取文本"跳过
            return ''


def _collect_tables(tables, text_parts):
    """递归收集表格文本

    doc.tables / container.tables 只返回该层级的顶层表格，
    单元格内的嵌套表格需通过 cell.tables 递归下探（PY2 修复）。
    """
    for table in tables:
        for row in table.rows:
            for cell in row.cells:
                # cell.text 只拼接单元格内段落，不含嵌套表格文本，二者互补
                if cell.text.strip():
                    text_parts.append(cell.text)
                _collect_tables(cell.tables, text_parts)
