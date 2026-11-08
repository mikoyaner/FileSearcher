# DocSearcher —— 文档内容搜索工具（双引擎）

在本地海量混合格式文档中搜索关键词：**Windows 桌面程序（GUI）** 与 **Python 命令行/模块** 两个入口并存，共用同一套需求，各自独立演进。

支持格式：`.txt` `.csv` `.log` `.md` `.json` `.xml` `.html` `.htm`（C#）/ `.yaml` `.yml` `.ini` `.cfg` `.conf` `.bat` `.sh`（Python）+ Office 文档 `.docx` `.xlsx` `.pptx`（新版）与 `.doc` `.xls` `.ppt`（旧版）。

## 目录结构

```
FileSearcher/
├── FileSearcher/            # C# WPF 桌面程序源码（Windows GUI）
│   ├── MainWindow.xaml(.cs) #   界面与搜索调度
│   └── Services/            #   扫描/解析/搜索/导出服务
├── FileSearcher.Tests/      # C# 回归测试（xUnit，dotnet test 运行）
├── python/                  # Python 引擎（CLI + 可导入模块）
│   ├── main.py              #   命令行入口
│   ├── core/                #   搜索调度 / 上下文提取 / 结果输出
│   ├── parsers/             #   各格式解析器（工厂注册表）
│   ├── utils/               #   扫描器 / 编码检测
│   └── tests/               #   pytest 回归测试
├── publish/                 # GUI 发布产物（不入库，本地生成）
└── AGENTS.md                # 代理协作规范
```

---

## 方式一：Windows 桌面程序（GUI，推荐日常使用）

### 运行

```bash
# 从源码构建运行（需要 .NET 7 SDK）
dotnet run --project FileSearcher

# 或构建自包含发布版（无需安装 .NET 运行时的单目录程序）
dotnet publish FileSearcher -c Release -r win-x64 --self-contained true -o publish
```

构建后运行 `publish/DocSearcher.exe`。

### 使用步骤

1. **选择文件夹** —— 选择要搜索的目录（隐藏/系统目录、junction 链接自动跳过）；
2. **输入关键词** → 回车或点击 **开始搜索**；结果实时流式显示（含文件路径、行号、上下文）；
3. 搜索中可点击 **停止搜索**（扫描与解析阶段均可即时取消）；
4. 完成后点击 **导出 CSV**（UTF-8 BOM，Excel 直接打开；含 文件路径/匹配序号/行号/上下文 四列）；
5. 若有文件解析失败或超时，结果区末尾会汇总提示"N 个文件处理失败或超时，已跳过"。

### 行为要点

- 每个文件最多返回 10 条匹配；单文件处理超时 5 秒自动跳过并计入失败汇总；
- 界面展示超过 2MB 后停止追加（提示改用 CSV 导出），**导出数据始终完整**；
- 旧版 `.doc`/`.xls`/`.ppt` 通过二进制启发式提取文本，可能含噪声、且非全覆盖（详见"已知限制"）。

---

## 方式二：Python 命令行（CLI）

### 安装依赖

```bash
pip install -r python/requirements.txt
```

### 基本用法

```bash
cd python

# 在 D:/文档 目录下搜索"预算"
python main.py D:/文档 预算

# 只搜指定类型，结果输出到 ./results，显示详细日志
python main.py ./src 函数 --type .py,.txt --output-dir ./results -v

# 只控制台显示，不落盘
python main.py D:/文档 报告 --no-csv --no-xlsx
```

### 参数说明

| 参数 | 说明 |
|---|---|
| `search_path`（位置参数） | 要搜索的目录路径 |
| `keyword`（位置参数） | 搜索关键词（不区分大小写） |
| `--type .a,.b` | 文件类型过滤，逗号分隔（默认全部支持格式） |
| `--output-dir` | 结果文件输出目录（默认当前目录） |
| `--no-csv` / `--no-xlsx` | 不保存对应格式的结果文件 |
| `--max-rows N` | 控制台最大显示行数（默认 50，0 = 全部） |
| `--max-depth N` | 最大递归深度（默认 -1 = 无限制） |
| `-v, --verbose` | 显示详细处理日志与失败明细 |

### 结果口径

按 **文件** 聚合：每个命中文件一行，含 `file_path`、`file_type`、`keyword`、`snippet`（首条命中的上下文片段）、`match_count`（总命中次数），按命中次数降序；自动保存 `search_results.csv` 与 `search_results.xlsx`。解析失败的文件会汇总提示"N 个文件解析失败已跳过"（`-v` 看明细）。

> 注意：CSV/XLSX 固定文件名 `search_results.*`，同目录重复搜索会覆盖上一次结果，需要保留时请更换 `--output-dir`。

---

## 方式三：Python 模块导入（二次开发）

```python
import sys
sys.path.insert(0, "python")          # 仓库根目录下将 python/ 加入模块路径

from core.searcher import search_files
from core.result_writer import save_results

results = search_files(
    root_dir="D:/文档",               # 搜索根目录
    keyword="预算",                    # 关键词
    include_extensions=None,           # None=全部支持格式；或 {".docx", ".txt"}
    exclude_dirs=None,                 # None=默认排除 .git/node_modules/AppData 等
    max_depth=-1,                      # 递归深度，-1 无限制
    verbose=False,
    progress_callback=lambda cur, total, path: print(f"{cur}/{total} {path}"),  # 可选
)

for r in results:
    print(r["file_path"], r["match_count"], r["snippet"])

save_results(results, output_dir="./out")   # 返回 (csv_path, xlsx_path)
```

单文件级 API：

```python
from parsers import get_parser
from core.context_extractor import extract_first_context, extract_context, count_matches

text = get_parser("报告.docx").extract_text("报告.docx")   # 不支持的格式抛 ValueError
count, snippets = extract_first_context(text, "预算", max_snippets=1)
```

---

## 两个引擎的能力对比

| | C# GUI（DocSearcher） | Python CLI/模块 |
|---|---|---|
| 结果粒度 | 逐条匹配（行号 + 上下文，每文件≤10 条） | 每文件一条（首条片段 + 总命中数） |
| 并发 | 多核并行 + 单文件 5s 超时 | 单线程顺序处理 |
| 导出 | CSV（4 列，含行号） | CSV + XLSX |
| 展示 | 实时流式刷新 + 2MB 截断 | 控制台表格（tabulate） |
| 文本编码探测 | UtfUnknown + GBK 兜底，流式 64KB 采样 | chardet，64KB 采样 + 多编码降级 |
| docx | 表格/文本框/页眉/页脚 | 表格（含嵌套）/页眉/页脚 |
| pptx | 表格/组合形状/备注/SmartArt | 表格/组合形状/备注 |
| 旧版 doc/xls/ppt | 二进制启发式提取 | textract / LibreOffice 转换（需安装） |
| 独有格式 | html/htm | yaml/yml/ini/cfg/conf/bat/sh |

选型建议：**交互式查文件用 GUI；批量任务、脚本集成、需要 XLSX 报表用 Python。**

## 开发与测试

```bash
# C# 侧（43 项回归测试）
dotnet test FileSearcher.sln

# Python 侧（78 项回归测试；需要 pytest）
python -m pytest python/tests
```

## 已知限制

1. **旧版 `.doc`/`.xls`/`.ppt`**：C# 引擎为启发式提取（可能产生噪声或漏检）；Python 引擎依赖 textract 或 LibreOffice（`soffice`），二者均未安装时跳过该类文件。精确解析建议先转换为 `.docx` 等新版格式。
2. **OCR（PDF 扫描件/图片）**：`python/parsers/ocr_parser.py` 仅为预留骨架，未实现、未注册。
3. **Python 引擎单线程**：超大目录可先用 `--type` 缩小范围；性能敏感场景用 C# GUI。
4. 本仓库不收录任何真实数据文件；`python/test_data/` 内为无隐私的合成样例。
