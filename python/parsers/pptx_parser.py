"""PowerPoint (.pptx) 文件解析器

使用 python-pptx 遍历幻灯片提取文本。

PY3 修复记录：
- 此前只遍历 slide.shapes 顶层：组合形状（GroupShape）内部的文本框/表格
  不会被枚举 → 组合形状内文本漏检；现按 _iter_shapes_recursive 深度优先展开。
- 备注页（notes slide）是独立于幻灯片页面的部件，此前完全不读取；
  现读取备注页文本。注意：python-pptx 的 slide.notes_slide 属性在备注页缺失时
  会自动"创建"一个新的备注页（惰性创建副作用），必须先用 slide.has_notes_slide
  守卫，避免在解析过程中修改文件结构意图（对只读搜索而言纯属浪费）。
"""

from parsers.base import BaseParser
from pptx.enum.shapes import MSO_SHAPE_TYPE


class PptxParser(BaseParser):
    """.pptx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        # PY6 契约变更：不再内部吞异常 —— 损坏/锁定文件抛出的异常直接上抛，
        # 由调度器统一捕获计数汇总（见 base.py 契约说明）
        from pptx import Presentation
        prs = Presentation(filepath)
        text_parts = []

        for slide in prs.slides:
            # PY3 修复：深度优先展开形状集合，覆盖组合形状内部
            for shape in _iter_shapes_recursive(slide.shapes):
                _collect_shape_text(shape, text_parts)

            # PY3 修复：读取备注页文本（先守卫，避免惰性创建）
            if slide.has_notes_slide:
                notes_frame = slide.notes_slide.notes_text_frame
                if notes_frame is not None and notes_frame.text.strip():
                    text_parts.append(notes_frame.text)

        return '\n'.join(text_parts)


def _iter_shapes_recursive(shapes):
    """深度优先遍历形状集合

    组合形状（GroupShape）的 shape_type 为 MSO_SHAPE_TYPE.GROUP，
    其 .shapes 属性提供子形状集合，递归展开（PY3 修复核心）。
    """
    for shape in shapes:
        yield shape

        # shape_type 可能在个别异常形状上抛错，降级为"非组合形状"处理
        try:
            is_group = shape.shape_type == MSO_SHAPE_TYPE.GROUP
        except Exception:
            is_group = False

        if is_group:
            yield from _iter_shapes_recursive(shape.shapes)


def _collect_shape_text(shape, text_parts):
    """收集单个形状的文本：文本框段落 + 表格单元格

    has_text_frame / has_table 定义在 BaseShape 基类上，
    对文本框、组合形状、图形占位符均安全（False 表示无该内容）。
    """
    if shape.has_text_frame:
        for para in shape.text_frame.paragraphs:
            if para.text.strip():
                text_parts.append(para.text)

    # 表格文本（GraphicFrame 形状承载 a:tbl）
    if shape.has_table:
        for row in shape.table.rows:
            for cell in row.cells:
                if cell.text.strip():
                    text_parts.append(cell.text)
