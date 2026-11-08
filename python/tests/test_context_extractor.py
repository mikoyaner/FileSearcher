"""上下文截取模块回归测试

覆盖：计数、片段截取、边界钳制、正则转义、换行清洗、单次扫描限量提取（PY4）
"""

import pytest
from core.context_extractor import extract_context, count_matches, extract_first_context


class TestCountMatches:
    def test_counts_non_overlapping_occurrences(self):
        assert count_matches("预算一 预算二 预算三", "预算") == 3

    def test_case_insensitive_for_english(self):
        assert count_matches("Hello world and hello again", "hello") == 2

    def test_regex_special_chars_are_escaped(self):
        # 关键词中的 '.' 必须按字面匹配，不能当正则通配符
        assert count_matches("v1.2 v1x2", "1.2") == 1

    def test_empty_keyword_returns_zero(self):
        assert count_matches("some text", "") == 0

    def test_empty_text_returns_zero(self):
        assert count_matches("", "关键词") == 0


class TestExtractContext:
    def test_snippet_contains_keyword(self):
        snippets = extract_context("前文铺垫这里有关键词出现后文收尾", "关键词")
        assert len(snippets) == 1
        snippet, index = snippets[0]
        assert "关键词" in snippet
        assert index >= 0

    def test_context_clamped_at_text_start(self):
        # 匹配位于开头：不加 "..." 前缀
        snippets = extract_context("关键词在开头", "关键词")
        snippet, _ = snippets[0]
        assert snippet.startswith("关键词")
        assert not snippet.startswith("...")

    def test_context_clamped_at_text_end(self):
        # 匹配位于结尾：不加 "..." 后缀
        snippets = extract_context("结尾是关键词", "关键词")
        snippet, _ = snippets[0]
        assert snippet.endswith("关键词")
        assert not snippet.endswith("...")

    def test_ellipsis_added_when_clipped(self):
        text = "x" * 100 + "关键词" + "y" * 100
        snippet, _ = extract_context(text, "关键词")[0]
        assert snippet.startswith("...")
        assert snippet.endswith("...")

    def test_newlines_replaced_by_spaces(self):
        snippets = extract_context("上一行\n关键词\n下一行", "关键词")
        snippet, _ = snippets[0]
        assert "\n" not in snippet

    def test_multiple_matches_ordered_by_position(self):
        text = "a关键词b关键词c"
        snippets = extract_context(text, "关键词")
        indexes = [index for _, index in snippets]
        assert indexes == sorted(indexes)
        assert len(snippets) == 2

    def test_keyword_longer_than_text(self):
        snippets = extract_context("短", "关键词很长很长")
        assert snippets == []

    def test_empty_inputs_return_empty(self):
        assert extract_context("", "kw") == []
        assert extract_context("text", "") == []


class TestExtractFirstContext:
    """PY4 回归：单次扫描同时返回总数与限量片段"""

    def test_counts_all_but_builds_limited_snippets(self):
        text = "预算 " * 100
        match_count, snippets = extract_first_context(text, "预算", max_snippets=1)

        assert match_count == 100  # 总数不丢
        assert len(snippets) == 1  # 片段只建 1 条

    def test_equivalent_to_count_plus_extract(self):
        # 与"两次全量扫描"的老路径结果等价
        text = "关键词甲在开头，中间出现关键词，结尾也有关键词"
        count, snippets = extract_first_context(text, "关键词", max_snippets=1)

        assert count == count_matches(text, "关键词")
        assert snippets[0][0] == extract_context(text, "关键词")[0][0]

    def test_empty_inputs(self):
        assert extract_first_context("", "kw") == (0, [])
        assert extract_first_context("text", "") == (0, [])

    def test_no_match(self):
        assert extract_first_context("无关内容", "关键词") == (0, [])
