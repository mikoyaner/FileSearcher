"""文件扫描模块回归测试

覆盖：递归收集、扩展名过滤、排除目录、深度限制、文件分类
"""

import os

import pytest

from utils.file_scanner import scan_files, classify_file, EXCLUDED_DIRS


@pytest.fixture
def tree(tmp_path):
    """构造三层目录树：根文件 + 一级子目录文件 + 二级子目录文件"""
    (tmp_path / "root.txt").write_text("root", encoding="utf-8")
    (tmp_path / "root.xyz").write_text("ignored", encoding="utf-8")

    level1 = tmp_path / "level1"
    level1.mkdir()
    (level1 / "l1.txt").write_text("l1", encoding="utf-8")

    level2 = level1 / "level2"
    level2.mkdir()
    (level2 / "l2.md").write_text("l2", encoding="utf-8")

    return tmp_path


class TestScanFiles:
    def test_recursive_collection(self, tree):
        files = scan_files(str(tree))
        names = {os.path.basename(f) for f in files}
        assert names == {"root.txt", "l1.txt", "l2.md"}

    def test_extension_filter(self, tree):
        files = scan_files(str(tree), include_extensions={".md"})
        assert [os.path.basename(f) for f in files] == ["l2.md"]

    def test_max_depth_zero_only_root(self, tree):
        files = scan_files(str(tree), max_depth=0)
        assert [os.path.basename(f) for f in files] == ["root.txt"]

    def test_max_depth_one_excludes_second_level(self, tree):
        files = scan_files(str(tree), max_depth=1)
        names = {os.path.basename(f) for f in files}
        assert names == {"root.txt", "l1.txt"}

    def test_exclude_dirs(self, tmp_path):
        junk = tmp_path / ".git"
        junk.mkdir()
        (junk / "in_git.txt").write_text("x", encoding="utf-8")
        (tmp_path / "keep.txt").write_text("y", encoding="utf-8")

        files = scan_files(str(tmp_path))
        assert [os.path.basename(f) for f in files] == ["keep.txt"]

    def test_nonexistent_root_returns_empty(self):
        assert scan_files("Z:/不存在的目录/xyz") == []


class TestClassifyFile:
    @pytest.mark.parametrize("filename, expected", [
        ("a.txt", "text"),
        ("a.json", "text"),
        ("a.docx", "openxml"),
        ("a.xlsx", "openxml"),
        ("a.pptx", "openxml"),
        ("a.doc", "legacy"),
        ("a.xls", "legacy"),
        ("a.ppt", "legacy"),
        ("a.exe", "unsupported"),
        ("a", "unsupported"),
    ])
    def test_classification(self, filename, expected):
        assert classify_file(filename) == expected
