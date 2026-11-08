"""纯文本文件解析器

支持格式：.txt, .csv, .md, .json, .xml, .log, .yaml, .yml, .ini, .cfg, .conf, .bat, .sh
使用 chardet 自动检测编码，兼容 GBK/UTF-8/BOM 等。

PY6 契约变更：不再内部吞异常 —— 文件被独占锁定等 OSError 直接上抛，
由调度器统一捕获计数（此前静默返回空串，用户无法感知文件被漏扫）。
"""

from parsers.base import BaseParser
from utils.encoding_detector import read_text_file_safe


class TextParser(BaseParser):
    """纯文本文件解析器"""

    def extract_text(self, filepath: str) -> str:
        # 读写失败（权限/锁定/删除竞态）异常上抛 → 调度器计入失败汇总
        # 编码层面由 read_text_file_safe 的降级链兜底（latin-1 最终必成功）
        return read_text_file_safe(filepath)
