"""文件扫描模块

递归扫描目录，按扩展名过滤文件，排除系统文件夹。
"""

import os
from typing import List, Set

# 支持的扩展名映射
TEXT_EXTENSIONS = {'.txt', '.csv', '.md', '.json', '.xml', '.log', '.yaml', '.yml', '.ini', '.cfg', '.conf', '.bat', '.sh'}
OFFICE_OPENXML_EXTENSIONS = {'.docx', '.xlsx', '.pptx'}
OFFICE_LEGACY_EXTENSIONS = {'.doc', '.xls', '.ppt'}
SUPPORTED_EXTENSIONS = TEXT_EXTENSIONS | OFFICE_OPENXML_EXTENSIONS | OFFICE_LEGACY_EXTENSIONS

# 需要排除的系统/缓存文件夹
EXCLUDED_DIRS = {
    '__pycache__', '.git', '.svn', '.hg', '.idea', '.vscode',
    'node_modules', 'venv', '.venv', 'env', '.env',
    '$Recycle.Bin', 'System Volume Information',
    'AppData', 'ProgramData',
}


def scan_files(
    root_dir: str,
    include_extensions: Set[str] = None,
    exclude_dirs: Set[str] = None,
    max_depth: int = -1,
) -> List[str]:
    """递归扫描目录，返回匹配的文件路径列表

    Args:
        root_dir: 根目录路径
        include_extensions: 要包含的文件扩展名集合（默认全部支持格式）
        exclude_dirs: 要排除的目录名集合
        max_depth: 最大递归深度，-1 表示无限制

    Returns:
        文件绝对路径列表
    """
    if include_extensions is None:
        include_extensions = SUPPORTED_EXTENSIONS
    if exclude_dirs is None:
        exclude_dirs = EXCLUDED_DIRS

    matched_files = []
    root_dir = os.path.abspath(root_dir)

    if not os.path.isdir(root_dir):
        return matched_files

    for current_path, dirs, filenames in os.walk(root_dir, topdown=True):
        # 计算当前深度
        if max_depth >= 0:
            rel_path = os.path.relpath(current_path, root_dir)
            if rel_path == '.':
                depth = 0
            else:
                depth = len(rel_path.split(os.sep))
            if depth > max_depth:
                # 跳过深层子目录
                dirs.clear()
                continue

        # 过滤排除目录（原地修改 dirs 以控制 os.walk 的遍历行为）
        dirs[:] = [d for d in dirs if d not in exclude_dirs and not d.startswith('.')]

        for filename in filenames:
            ext = os.path.splitext(filename)[1].lower()
            if ext in include_extensions:
                matched_files.append(os.path.join(current_path, filename))

    return matched_files


def classify_file(filepath: str) -> str:
    """根据文件扩展名返回文件类别

    Returns:
        'text', 'openxml', 'legacy', 或 'unsupported'
    """
    ext = os.path.splitext(filepath)[1].lower()
    if ext in TEXT_EXTENSIONS:
        return 'text'
    elif ext in OFFICE_OPENXML_EXTENSIONS:
        return 'openxml'
    elif ext in OFFICE_LEGACY_EXTENSIONS:
        return 'legacy'
    else:
        return 'unsupported'