"""Excel (.xlsx) 文件解析器

使用 openpyxl 遍历所有 Sheets → Rows → Cells 提取文本。
"""

from parsers.base import BaseParser


class XlsxParser(BaseParser):
    """.xlsx 文件解析器"""

    def extract_text(self, filepath: str) -> str:
        try:
            from openpyxl import load_workbook
            wb = load_workbook(filepath, data_only=True, read_only=True)
            text_parts = []

            for sheet_name in wb.sheetnames:
                ws = wb[sheet_name]
                for row in ws.iter_rows():
                    for cell in row:
                        if cell.value is not None:
                            text_parts.append(str(cell.value))

            wb.close()
            return '\n'.join(text_parts)
        except Exception:
            return ''