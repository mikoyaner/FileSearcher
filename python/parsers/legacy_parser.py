"""旧版二进制 Office 文件解析器

支持格式：.doc, .xls, .ppt

策略（按优先级）：
1. 尝试 textract（如果已安装）
2. 提供 subprocess 钩子调用 LibreOffice 命令行转换
3. 如果两者不可用，返回提示信息
"""

from parsers.base import BaseParser
import subprocess
import os


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

        # 两者均不可用
        return f"[提示] 旧版 Office 文件需要安装 LibreOffice 或 textract 来解析。文件: {filepath}"

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
        """
        # 检查 LibreOffice 是否可用
        try:
            subprocess.run(
                ['libreoffice', '--headless', '--version'],
                capture_output=True, timeout=5
            )
        except (FileNotFoundError, subprocess.TimeoutExpired):
            try:
                subprocess.run(
                    ['soffice', '--headless', '--version'],
                    capture_output=True, timeout=5
                )
            except (FileNotFoundError, subprocess.TimeoutExpired):
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
                    ['libreoffice', '--headless', '--convert-to', target_ext.lstrip('.'),
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

            except (subprocess.TimeoutExpired, Exception):
                pass

        return ''