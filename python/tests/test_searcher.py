"""搜索调度器端到端回归测试

覆盖：结果结构、命中计数、排序、空输入、不支持格式跳过
"""

import os

import pytest

from core.searcher import search_files, search_in_text


@pytest.fixture
def docs(tmp_path):
    """构造包含不同命中数量的文本目录"""
    (tmp_path / "many.txt").write_text("预算 " * 5, encoding="utf-8")
    (tmp_path / "one.txt").write_text("只有一次预算", encoding="utf-8")
    (tmp_path / "none.txt").write_text("毫无关联的内容", encoding="utf-8")
    return tmp_path


class TestSearchInText:
    def test_returns_count_and_first_snippet(self):
        count, snippet = search_in_text("预算一 预算二 预算三", "预算")
        assert count == 3
        assert "预算" in snippet

    def test_no_match_returns_zero_and_empty(self):
        assert search_in_text("无关内容", "关键词") == (0, "")

    def test_empty_inputs(self):
        assert search_in_text("", "kw") == (0, "")
        assert search_in_text("text", "") == (0, "")


class TestSearchFiles:
    def test_result_structure(self, docs):
        results = search_files(str(docs), "预算")

        assert len(results) == 2  # none.txt 不命中
        result = results[0]
        assert set(result.keys()) == {"file_path", "file_type", "keyword", "snippet", "match_count"}
        assert result["keyword"] == "预算"
        assert result["file_type"] == ".txt"

    def test_sorted_by_match_count_desc(self, docs):
        results = search_files(str(docs), "预算")
        counts = [r["match_count"] for r in results]
        assert counts == sorted(counts, reverse=True)

    def test_match_count_counts_all_occurrences(self, docs):
        many = [r for r in search_files(str(docs), "预算") if r["file_path"].endswith("many.txt")]
        assert many[0]["match_count"] == 5

    def test_no_match_returns_empty(self, docs):
        assert search_files(str(docs), "不存在的词") == []

    def test_empty_keyword_returns_empty(self, docs):
        assert search_files(str(docs), "") == []

    def test_include_extensions_filter(self, docs):
        results = search_files(str(docs), "预算", include_extensions={".txt"})
        assert len(results) == 2

    def test_include_extensions_excludes_everything(self, docs):
        assert search_files(str(docs), "预算", include_extensions={".py"}) == []

    def test_unsupported_files_ignored(self, tmp_path):
        (tmp_path / "data.bin").write_bytes(b"\x00\x01\x02")
        (tmp_path / "ok.txt").write_text("含有关键词", encoding="utf-8")

        results = search_files(str(tmp_path), "关键词")

        assert len(results) == 1
        assert results[0]["file_path"].endswith("ok.txt")
