"""PowerPoint 解析器回归测试

覆盖 PY3 修复：组合形状递归、备注页文本
"""

import pytest

from parsers.pptx_parser import PptxParser


def _build_pptx(path, build_slides):
    from pptx import Presentation
    prs = Presentation()
    build_slides(prs)
    prs.save(str(path))
    return path


class TestPptxParser:
    def test_plain_shape_text_found(self, tmp_path):
        # 基线：普通文本框不回退
        from pptx.util import Pt

        def build(prs):
            slide = prs.slides.add_slide(prs.slide_layouts[6])  # 空白版式
            box = slide.shapes.add_textbox(0, 0, 1000000, 1000000)
            box.text_frame.text = "普通文本框里的预算关键词"

        text = PptxParser().extract_text(str(_build_pptx(tmp_path / "plain.pptx", build)))

        assert "预算关键词" in text

    def test_group_shape_text_found(self, tmp_path):
        # PY3 回归：组合形状（GroupShape）内部文本框，此前只遍历顶层 → 漏检
        from pptx.util import Emu

        def build(prs):
            slide = prs.slides.add_slide(prs.slide_layouts[6])
            group = slide.shapes.add_group_shape()
            inner = group.shapes.add_textbox(Emu(0), Emu(0), Emu(914400), Emu(914400))
            inner.text_frame.text = "组合形状内的秘密关键词"

        text = PptxParser().extract_text(str(_build_pptx(tmp_path / "group.pptx", build)))

        assert "秘密关键词" in text

    def test_notes_slide_text_found(self, tmp_path):
        # PY3 回归：备注页是独立部件，此前完全不读取
        from pptx.util import Emu

        def build(prs):
            slide = prs.slides.add_slide(prs.slide_layouts[6])
            slide.shapes.add_textbox(Emu(0), Emu(0), Emu(914400), Emu(914400))
            # notes_slide 属性首次访问会自动创建备注页（测试构造阶段允许）
            slide.notes_slide.notes_text_frame.text = "备注里的机密词汇，仅演讲者可见"

        text = PptxParser().extract_text(str(_build_pptx(tmp_path / "notes.pptx", build)))

        assert "机密词汇" in text

    def test_table_text_found(self, tmp_path):
        # 表格文本基线（此前已支持，确保不回退）
        def build(prs):
            slide = prs.slides.add_slide(prs.slide_layouts[6])
            table_shape = slide.shapes.add_table(2, 2, 0, 0, 1000000, 1000000)
            table_shape.table.cell(0, 0).text = "表格单元格里的预算关键词"

        text = PptxParser().extract_text(str(_build_pptx(tmp_path / "table.pptx", build)))

        assert "预算关键词" in text

    def test_corrupt_file_raises_for_scheduler(self, tmp_path):
        # PY6 契约回归：损坏文件不再静默返回空串，而是抛出异常由调度器计数
        path = tmp_path / "corrupt.pptx"
        path.write_bytes(b"PK\x03\x04 not a real pptx")

        with pytest.raises(Exception):
            PptxParser().extract_text(str(path))
