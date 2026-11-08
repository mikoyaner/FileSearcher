"""旧版 Office 解析器回归测试

覆盖 PY1 修复：假阳性提示文本消除、探测命令缓存
"""

import pytest

from parsers import legacy_parser
from parsers.legacy_parser import LegacyParser


@pytest.fixture
def no_office(monkeypatch):
    """假定本机无 textract/LibreOffice：探测函数固定返回 None，测试环境稳定"""
    monkeypatch.setattr(legacy_parser, "_detect_office_command", lambda: None)


class TestLegacyParser:
    @pytest.fixture(autouse=True)
    def _setup(self, no_office):
        pass

    def test_unparseable_doc_returns_empty_not_hint_text(self, tmp_path):
        # PY1 回归：此前返回 "[提示]..." 文本 → 搜索"文件/解析/安装"等词时
        # 每个未解析的 .doc 文件都产生假阳性命中
        path = tmp_path / "fake.doc"
        path.write_bytes(b"\xd0\xcf\x11\xe0garbage-not-a-real-doc")

        result = LegacyParser().extract_text(str(path))

        assert result == ""
        assert "[提示]" not in result

    def test_unparseable_ppt_returns_empty(self, tmp_path):
        path = tmp_path / "fake.ppt"
        path.write_bytes(b"\x00\x01\x02not-a-ppt")

        assert LegacyParser().extract_text(str(path)) == ""

    def test_unparseable_xls_returns_empty(self, tmp_path):
        path = tmp_path / "fake.xls"
        path.write_bytes(b"\x09\x08not-an-xls")

        assert LegacyParser().extract_text(str(path)) == ""


class TestOfficeCommandProbe:
    def test_detection_cached_per_process(self, monkeypatch):
        # PY1 回归：探测结果必须进程级缓存 —— 此前每个旧版文件都重新探测一次，
        # 100 个 .doc 文件会白跑上百次 --version 子进程
        probe_calls = []

        def fake_run(cmd, **kwargs):
            probe_calls.append(cmd[0])
            raise FileNotFoundError(cmd[0])

        monkeypatch.setattr(legacy_parser.subprocess, "run", fake_run)
        monkeypatch.setattr(legacy_parser, "_OFFICE_PROBED", False)
        monkeypatch.setattr(legacy_parser, "_OFFICE_COMMAND", None)

        assert legacy_parser._detect_office_command() is None
        assert legacy_parser._detect_office_command() is None  # 第二次必须走缓存

        # 两个候选命令只探测一轮
        assert len(probe_calls) == len(legacy_parser._CANDIDATE_COMMANDS)
