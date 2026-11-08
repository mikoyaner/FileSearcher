"""结果输出模块回归测试

覆盖：CSV/XLSX 保存（BOM、表头、数据行）与控制台打印
"""

import os

import openpyxl
import pytest

from core.result_writer import print_results, save_results


def make_results(keyword="预算"):
    return [
        {
            "file_path": os.path.abspath("docs/a.txt"),
            "file_type": ".txt",
            "keyword": keyword,
            "snippet": "包含预算的片段",
            "match_count": 3,
        },
    ]


class TestSaveResults:
    def test_csv_saved_with_bom_and_content(self, tmp_path):
        csv_path, _ = save_results(make_results(), output_dir=str(tmp_path), save_xlsx=False)

        assert csv_path is not None
        raw = open(csv_path, "rb").read()
        # utf-8-sig：Excel 直接打开不乱码
        assert raw[:3] == b"\xef\xbb\xbf"

        text = raw.decode("utf-8-sig")
        lines = text.strip().splitlines()
        assert lines[0] == "File Path,File Type,Keyword,Snippet,Match Count"
        assert "包含预算的片段" in lines[1]

    def test_xlsx_saved_and_readable(self, tmp_path):
        _, xlsx_path = save_results(make_results(), output_dir=str(tmp_path), save_csv=False)

        assert xlsx_path is not None
        wb = openpyxl.load_workbook(xlsx_path)
        ws = wb.active
        header = [cell.value for cell in ws[1]]
        assert header == ["File Path", "File Type", "Keyword", "Snippet", "Match Count"]
        assert ws.cell(row=2, column=5).value == 3

    def test_both_saved(self, tmp_path):
        csv_path, xlsx_path = save_results(make_results(), output_dir=str(tmp_path))
        assert csv_path is not None and os.path.exists(csv_path)
        assert xlsx_path is not None and os.path.exists(xlsx_path)

    def test_no_results_returns_none_paths(self, tmp_path, capsys):
        csv_path, xlsx_path = save_results([], output_dir=str(tmp_path))
        assert csv_path is None
        assert xlsx_path is None


class TestPrintResults:
    def test_empty_results_message(self, capsys):
        print_results([])
        output = capsys.readouterr().out
        assert "未找到匹配文件" in output

    def test_results_printed_as_table(self, capsys):
        print_results(make_results(), max_rows=10)
        output = capsys.readouterr().out
        assert "预算" in output
        assert "Snippet" in output

    def test_max_rows_truncation(self, capsys):
        results = [
            {"file_path": f"a{i}.txt", "file_type": ".txt", "keyword": "kw", "snippet": "s", "match_count": i}
            for i in range(5)
        ]
        print_results(results, max_rows=2)
        output = capsys.readouterr().out
        assert "仅显示前 2 条" in output
