"""结果输出模块

支持两种输出方式：
1. 控制台打印：使用 pandas + tabulate 美化输出表格
2. 文件保存：自动输出 search_results.csv 和 search_results.xlsx
"""

import os
import pandas as pd
from typing import List, Dict, Optional


COLUMNS = ['file_path', 'file_type', 'keyword', 'snippet', 'match_count']
COLUMN_NAMES = {
    'file_path': 'File Path',
    'file_type': 'File Type',
    'keyword': 'Keyword',
    'snippet': 'Snippet',
    'match_count': 'Match Count',
}


def _make_dataframe(results: List[Dict]) -> pd.DataFrame:
    """将结果列表转换为 DataFrame"""
    if not results:
        return pd.DataFrame(columns=COLUMNS)

    df = pd.DataFrame(results, columns=COLUMNS)
    # 设置 match_count 为整数类型
    df['match_count'] = df['match_count'].astype(int)
    return df


def print_results(results: List[Dict], max_rows: int = 50) -> None:
    """将搜索结果打印到控制台

    Args:
        results: 搜索结果列表
        max_rows: 最大显示行数（默认 50，0 表示全部显示）
    """
    if not results:
        print("=" * 60)
        print("搜索结果：未找到匹配文件")
        print("=" * 60)
        return

    df = _make_dataframe(results)
    display_df = df.copy()

    # 截断过长 snippet 以便控制台显示
    display_df['snippet'] = display_df['snippet'].apply(
        lambda x: x[:80] + '...' if len(x) > 80 else x
    )

    # 重命名列
    display_df = display_df.rename(columns=COLUMN_NAMES)

    # 限制行数
    if max_rows > 0 and len(display_df) > max_rows:
        display_df = display_df.head(max_rows)
        print(f"\n（仅显示前 {max_rows} 条，共 {len(results)} 条命中）\n")

    print("\n" + "=" * 80)
    print(f"搜索结果 - 关键词: '{results[0]['keyword']}'")
    print(f"命中文件数: {len(results)}")
    print("=" * 80 + "\n")

    # 使用 tabulate 美化输出
    try:
        from tabulate import tabulate
        print(tabulate(
            display_df,
            headers='keys',
            tablefmt='grid',
            showindex=False,
            maxcolwidths=[60, 10, 15, 80, 10],
        ))
    except ImportError:
        # fallback 到 pandas to_string
        pd.set_option('display.max_colwidth', 80)
        pd.set_option('display.width', 120)
        print(display_df.to_string(index=False))

    print()


def save_results(
    results: List[Dict],
    output_dir: str = '.',
    save_xlsx: bool = True,
    save_csv: bool = True,
) -> tuple:
    """将搜索结果保存为 CSV 和/或 XLSX 文件

    Args:
        results: 搜索结果列表
        output_dir: 输出目录
        save_xlsx: 是否保存 .xlsx
        save_csv: 是否保存 .csv

    Returns:
        (csv_path, xlsx_path) 元组
        如果未保存对应格式，路径为 None
    """
    if not results:
        print("没有结果需要保存")
        return None, None

    df = _make_dataframe(results)
    # 重命名列
    df = df.rename(columns=COLUMN_NAMES)

    os.makedirs(output_dir, exist_ok=True)

    csv_path = None
    xlsx_path = None

    if save_csv:
        csv_path = os.path.join(output_dir, 'search_results.csv')
        df.to_csv(csv_path, index=False, encoding='utf-8-sig')
        print(f"CSV 已保存: {csv_path}")

    if save_xlsx:
        xlsx_path = os.path.join(output_dir, 'search_results.xlsx')
        df.to_excel(xlsx_path, index=False, engine='openpyxl')
        print(f"XLSX 已保存: {xlsx_path}")

    return csv_path, xlsx_path