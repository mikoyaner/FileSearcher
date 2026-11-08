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


class TestReparsePointDefense:
    """PY5 回归：junction/符号链接环防御"""

    def test_is_reparse_point_detects_junction(self, tmp_path):
        from utils.file_scanner import _is_reparse_point

        target = tmp_path / "target"
        target.mkdir()
        link = tmp_path / "junction"
        if not _create_junction(str(link), str(target)):
            pytest.skip("当前环境不允许创建 junction（FAT 分区/组策略限制）")

        assert _is_reparse_point(str(link)) is True
        assert _is_reparse_point(str(target)) is False

    def test_is_reparse_point_treats_unreadable_as_dangerous(self, tmp_path, monkeypatch):
        # lstat 失败（权限不足/竞态删除）时按危险处理 → 跳过该目录
        from utils import file_scanner

        def broken_lstat(path):
            raise OSError(path)

        monkeypatch.setattr(file_scanner.os, "lstat", broken_lstat)
        assert file_scanner._is_reparse_point(str(tmp_path / "whatever")) is True

    def test_scan_terminates_on_junction_cycle(self, tmp_path):
        # PY5 回归：junction 指向扫描根自身（自环）——
        # 修复前 os.walk 跟随 junction（is_symlink()=False）无限遍历
        nested = tmp_path / "nested"
        nested.mkdir()
        (nested / "inside.txt").write_text("hello", encoding="utf-8")

        link = tmp_path / "loop"
        if not _create_junction(str(link), str(tmp_path)):
            pytest.skip("当前环境不允许创建 junction")

        files = scan_files(str(tmp_path))

        # 只找到 nested/inside.txt，junction 未被进入
        assert [os.path.basename(f) for f in files] == ["inside.txt"]

    def test_scan_excludes_hidden_dirs_on_windows(self, tmp_path):
        from utils.file_scanner import _is_hidden_dir, _is_reparse_point
        from utils import file_scanner

        normal = tmp_path / "normal"
        normal.mkdir()
        (normal / "ok.txt").write_text("x", encoding="utf-8")

        hidden = tmp_path / "secret"
        hidden.mkdir()
        (hidden / "leak.txt").write_text("y", encoding="utf-8")

        # 仅当文件系统支持隐藏属性时模拟 Windows 隐藏目录
        if not hasattr(file_scanner.os.lstat(str(tmp_path)), "st_file_attributes"):
            pytest.skip("非 Windows 文件系统，无 st_file_attributes")

        import ctypes
        FILE_ATTRIBUTE_HIDDEN = 0x2
        ctypes.windll.kernel32.SetFileAttributesW(str(hidden), FILE_ATTRIBUTE_HIDDEN)

        assert _is_hidden_dir(str(hidden)) is True
        assert _is_hidden_dir(str(normal)) is False

        files = scan_files(str(tmp_path))
        assert [os.path.basename(f) for f in files] == ["ok.txt"]


def _create_junction(link_path: str, target_path: str) -> bool:
    """通过 mklink /J 创建目录 junction（普通用户权限即可），失败表示环境不支持"""
    import subprocess

    try:
        proc = subprocess.run(
            ["cmd.exe", "/c", "mklink", "/J", link_path, target_path],
            capture_output=True, timeout=10,
        )
        return proc.returncode == 0 and os.path.isdir(link_path)
    except (OSError, subprocess.TimeoutExpired):
        return False


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
