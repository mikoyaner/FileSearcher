"""Word 解析器回归测试

覆盖 PY2 修复：页眉/页脚读取、嵌套表格递归
"""

import pytest

from parsers.docx_parser import DocxParser


class TestDocxParser:
    def test_header_and_footer_found(self, tmp_path):
        # PY2 回归：页眉/页脚是 section 独立部件，此前完全不读取
        from docx import Document
        doc = Document()
        doc.add_paragraph("正文普通内容")
        doc.sections[0].header.paragraphs[0].text = "页眉里的秘密关键词"
        doc.sections[0].footer.paragraphs[0].text = "页脚里的机密词汇"

        path = tmp_path / "header.docx"
        doc.save(str(path))

        text = DocxParser().extract_text(str(path))

        assert "秘密关键词" in text
        assert "机密词汇" in text
        assert "正文普通内容" in text

    def test_nested_table_found(self, tmp_path):
        # PY2 回归：doc.tables 只含顶层表格，单元格内嵌套表格此前遗漏
        from docx import Document
        doc = Document()
        outer = doc.add_table(rows=1, cols=1)
        outer_cell = outer.rows[0].cells[0]
        outer_cell.paragraphs[0].text = "外层单元格内容"

        # 在外层单元格内再建一张嵌套表格
        nested = outer_cell.add_table(rows=1, cols=1)
        nested.rows[0].cells[0].paragraphs[0].text = "嵌套表格内的秘密关键词"

        path = tmp_path / "nested.docx"
        doc.save(str(path))

        text = DocxParser().extract_text(str(path))

        assert "外层单元格内容" in text
        assert "嵌套表格内的秘密关键词" in text

    def test_top_level_table_still_found(self, tmp_path):
        # 顶层表格作为基线，确保修复不回退
        from docx import Document
        doc = Document()
        doc.add_table(rows=1, cols=1)
        doc.tables[0].rows[0].cells[0].paragraphs[0].text = "顶层表格里的预算关键词"

        path = tmp_path / "table.docx"
        doc.save(str(path))

        assert "预算关键词" in DocxParser().extract_text(str(path))

    def test_corrupt_file_raises_for_scheduler(self, tmp_path):
        # PY6 契约回归：损坏文件不再静默返回空串，而是抛出异常由调度器计数
        import pytest

        path = tmp_path / "corrupt.docx"
        path.write_bytes(b"PK\x03\x04 not a real docx")

        with pytest.raises(Exception):
            DocxParser().extract_text(str(path))
