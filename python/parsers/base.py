"""解析器抽象基类

所有具体解析器必须继承 BaseParser 并实现 extract_text 方法。
"""

from abc import ABC, abstractmethod


class BaseParser(ABC):
    """解析器抽象基类"""

    @abstractmethod
    def extract_text(self, filepath: str) -> str:
        """从文件中提取纯文本内容

        Args:
            filepath: 文件路径

        Returns:
            提取到的纯文本字符串（空字符串表示解析失败）

        Raises:
            Exception: 解析过程中发生的任何异常应由子类自行捕获并返回空字符串
        """
        pass

    def get_supported_extensions(self) -> list:
        """返回此解析器支持的文件扩展名列表"""
        return []