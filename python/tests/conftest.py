"""pytest 全局配置

将 python/ 根目录加入 sys.path，确保测试可直接导入 parsers/utils/core 包。
"""

import os
import sys

# 测试文件位于 python/tests/，其上一级即 python/ 包根目录
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)
