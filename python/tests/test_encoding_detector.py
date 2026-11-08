"""编码检测模块回归测试

覆盖：BOM 文件、GBK 中文、ASCII、空文件与安全读取降级链
"""

import pytest

from utils.encoding_detector import detect_encoding, read_text_file_safe


class TestDetectEncoding:
    def test_utf8_detected(self, tmp_path):
        path = tmp_path / "u.txt"
        path.write_text("普通 UTF-8 内容", encoding="utf-8")
        assert detect_encoding(str(path)) == "utf-8"

    def test_ascii_detected_as_utf8(self, tmp_path):
        path = tmp_path / "a.txt"
        path.write_text("plain ascii 12345", encoding="utf-8")
        assert detect_encoding(str(path)) == "utf-8"

    def test_gbk_chinese_detected(self, tmp_path):
        # 足够长的中文保证 chardet 检出 GB 系列（映射到 'gbk'）
        path = tmp_path / "g.txt"
        path.write_text("这是一段用于编码自动检测的中文文本，包含足够多的汉字供统计检测。" * 3, encoding="gbk")
        assert detect_encoding(str(path)) == "gbk"

    def test_empty_file_falls_back_to_utf8(self, tmp_path):
        path = tmp_path / "e.txt"
        path.write_bytes(b"")
        assert detect_encoding(str(path)) == "utf-8"


class TestReadTextFileSafe:
    def test_utf8_roundtrip(self, tmp_path):
        content = "中文 UTF-8 内容 123"
        path = tmp_path / "u.txt"
        path.write_text(content, encoding="utf-8")
        assert read_text_file_safe(str(path)) == content

    def test_gbk_roundtrip(self, tmp_path):
        content = "GBK 编码的中文内容，应当被正确读取。"
        path = tmp_path / "g.txt"
        path.write_text(content, encoding="gbk")
        assert read_text_file_safe(str(path)) == content

    def test_utf16le_bom_roundtrip(self, tmp_path):
        content = "UTF-16 LE 带字节序标记的内容"
        path = tmp_path / "w.txt"
        path.write_text(content, encoding="utf-16")  # Python 的 utf-16 编码默认带 BOM
        assert read_text_file_safe(str(path)) == content

    def test_latin1_fallback_never_fails(self, tmp_path):
        # 任意字节序列最终都能通过 latin-1 读出（可能乱码但不抛异常）
        path = tmp_path / "bin.txt"
        path.write_bytes(bytes(range(0, 256)))
        assert isinstance(read_text_file_safe(str(path)), str)
