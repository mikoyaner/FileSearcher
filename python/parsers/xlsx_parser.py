"""Excel (.xlsx) 文件解析器

使用 openpyxl 遍历所有 Sheets → Rows → Cells 提取文本。

PY6 契约变更：不再内部吞异常 —— 损坏/锁定文件抛出的异常直接上抛，
由调度器统一捕获计数汇总（见 base.py 契约说明）；
wb.close() 移入 finally，异常路径也能释放 read_only 模式的文件句柄。
"""

from parsers.base import BaseParser


class XlsxParser(BaseParser):
    """.xlsx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        from openpyxl import load_workbook
        # data_only=True：公式单元格取缓存计算值而非公式串
        # read_only=True：流式读取，内存占用与工作簿大小解耦
        wb = load_workbook(filepath, data_only=True, read_only=True)
        try:
            text_parts = []

            for sheet_name in wb.sheetnames:
                ws = wb[sheet_name]
                for row in ws.iter_rows():
                    for cell in row:
                        if cell.value is not None:
                            text_parts.append(str(cell.value))

            return '\n'.join(text_parts)
        finally:
            # read_only 工作簿持有打开的文件句柄，异常路径也必须释放
            wb.close()
