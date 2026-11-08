"""解析器抽象基类

所有具体解析器必须继承 BaseParser 并实现 extract_text 方法。
"""

from abc import ABC, abstractmethod


class BaseParser(ABC):
    """解析器抽象基类"""

    @abstractmethod
    def extract_text(self, filepath: str) -> str:
        """从文件中提取纯文本内容

        PY6 契约变更：解析失败（文件损坏/锁定/格式异常）应直接抛出异常，
        由搜索调度器统一捕获并计数汇总，不再吞掉 —— 此前各解析器内部
        catch-all 返回空字符串，调度器无法区分"没有匹配"与"没搜到"，
        非 verbose 模式下解析失败完全静默（假阴性）。

        返回空字符串仅表示"文件合法但无文本内容"（空文件/空表格/未安装
        旧版格式解析依赖等），调度器按"无可提取文本"跳过，不计入错误。

        Args:
            filepath: 文件路径

        Returns:
            提取到的纯文本字符串（空字符串 = 合法文件但无文本）

        Raises:
            Exception: 解析异常，由调度器捕获并计入失败汇总
        """
        pass

    def get_supported_extensions(self) -> list:
        """返回此解析器支持的文件扩展名列表"""
        return []
