"""旧版二进制 Office 文件解析器

支持格式：.doc, .xls, .ppt

策略（按优先级）：
1. 尝试 textract（如果已安装）
2. 提供 subprocess 钩子调用 LibreOffice 命令行转换，复用对应新版格式解析器
3. 两者均不可用时返回空字符串（解析失败不产生文本）

PY1 修复记录：
- 此前解析失败返回 "[提示] 旧版 Office 文件需要安装 LibreOffice..." 提示文本，
  该文本会被搜索器当作正文参与关键词匹配 → 每个未解析的 .doc 文件在搜索
  "文件/解析/安装" 等词时都产生假阳性命中。现改为返回空字符串，由搜索器跳过。
- 此前可用性探测与转换命令不一致：探测先试 'libreoffice' 再试 'soffice'，
  但转换始终硬编码 'libreoffice' —— 机器上仅有 soffice 命令时探测通过、
  转换必然 FileNotFoundError 静默失败。现统一使用探测成功的命令名。
- 命令探测结果按进程缓存（模块级变量），避免每个旧版文件都重复探测一次
  （每次探测最多阻塞 2×5 秒，100 个 .doc 文件原先要白跑上百次 --version）。
"""

from parsers.base import BaseParser
import os
import subprocess

# LibreOffice 在不同发行方式下的可执行文件名：
# Linux 发行版包通常为 libreoffice，Windows 官方安装通常为 soffice
_CANDIDATE_COMMANDS = ('libreoffice', 'soffice')

# 进程级缓存：_OFFICE_PROBED 表示是否已探测；_OFFICE_COMMAND 为探测到的命令名
#（None 表示尚未探测或探测失败）
_OFFICE_COMMAND = None
_OFFICE_PROBED = False


def _detect_office_command():
    """探测本机可用的 LibreOffice 命令名（进程内只探测一次）

    Returns:
        可用的命令名（'libreoffice' 或 'soffice'）；两者都不可用时返回 None
    """
    global _OFFICE_COMMAND, _OFFICE_PROBED
    if _OFFICE_PROBED:
        return _OFFICE_COMMAND

    _OFFICE_PROBED = True
    for command in _CANDIDATE_COMMANDS:
        try:
            subprocess.run(
                [command, '--headless', '--version'],
                capture_output=True, timeout=5
            )
            _OFFICE_COMMAND = command
            return command
        except (FileNotFoundError, subprocess.TimeoutExpired, OSError):
            continue

    _OFFICE_COMMAND = None
    return _OFFICE_COMMAND


class LegacyParser(BaseParser):
    """旧版 Office 二进制文件解析器"""

    def extract_text(self, filepath: str) -> str:
        ext = os.path.splitext(filepath)[1].lower()

        # 尝试 textract
        text = self._try_textract(filepath)
        if text:
            return text

        # 尝试 LibreOffice 命令行转换
        text = self._try_libreoffice(filepath, ext)
        if text:
            return text

        # PY1 修复：两者均不可用时返回空字符串（此前返回提示文本造成搜索假阳性）。
        # 空字符串会被搜索器按"无可提取文本"跳过，不会污染结果。
        return ''

    def _try_textract(self, filepath: str) -> str:
        """尝试使用 textract 解析"""
        try:
            import textract
            result = textract.process(filepath)
            if result:
                return result.decode('utf-8', errors='replace')
        except ImportError:
            pass
        except Exception:
            pass
        return ''

    def _try_libreoffice(self, filepath: str, ext: str) -> str:
        """尝试使用 LibreOffice 命令行转换

        将 .doc/.xls/.ppt 转换为临时 .docx/.xlsx/.pptx，然后复用对应解析器。
        PY1 修复：转换命令与探测结果保持一致，不再硬编码 'libreoffice'。
        """
        command = _detect_office_command()
        if not command:
            return ''

        # 转换映射
        target_ext_map = {
            '.doc': '.docx',
            '.xls': '.xlsx',
            '.ppt': '.pptx',
        }
        target_ext = target_ext_map.get(ext, '.docx')

        # 转换到临时文件
        import tempfile
        with tempfile.TemporaryDirectory() as tmp_dir:
            try:
                subprocess.run(
                    [command, '--headless', '--convert-to', target_ext.lstrip('.'),
                     '--outdir', tmp_dir, filepath],
                    capture_output=True, timeout=60
                )

                # 查找转换后的文件
                base_name = os.path.splitext(os.path.basename(filepath))[0]
                converted_path = os.path.join(tmp_dir, f"{base_name}{target_ext}")

                if os.path.exists(converted_path):
                    # 复用对应解析器
                    if target_ext == '.docx':
                        from parsers.docx_parser import DocxParser
                        parser = DocxParser()
                    elif target_ext == '.xlsx':
                        from parsers.xlsx_parser import XlsxParser
                        parser = XlsxParser()
                    else:
                        from parsers.pptx_parser import PptxParser
                        parser = PptxParser()
                    return parser.extract_text(converted_path)

            except (subprocess.TimeoutExpired, OSError):
                # 转换超时或进程创建失败：按不可解析处理，返回空字符串
                pass

        return ''
