using System.Collections.Concurrent;
using System.IO;
using System.Text;
using FileSearcher.Helpers;
using FileSearcher.Models;

namespace FileSearcher.Services
{
    /// <summary>
    /// 关键词搜索服务 - 匹配 + 上下文提取
    /// </summary>
    public class SearchService
    {
        private readonly TextParserService _textParser;
        private readonly DocxParserService _docxParser;
        private readonly XlsxParserService _xlsxParser;
        private readonly PptxParserService _pptxParser;
        private readonly LegacyParserService _legacyParser;

        // 每个文件最多匹配数
        private const int MaxMatchesPerFile = 10;
        // 上下文前后字符数
        private const int ContextLength = 50;

        // F3 修复：解析/处理失败的文件记录（文件名 + 原因），供 UI 层在搜索结束后汇总展示。
        // 此前整条链路空 catch 吞异常，用户无法区分"没有匹配"与"没搜到"（静默假阴性）。
        // ConcurrentQueue：多个并行工作体会同时写入。
        public ConcurrentQueue<string> Errors { get; } = new();

        public SearchService()
        {
            _textParser = new TextParserService();
            _docxParser = new DocxParserService();
            _xlsxParser = new XlsxParserService();
            _pptxParser = new PptxParserService();
            _legacyParser = new LegacyParserService();
        }

        /// <summary>
        /// 在指定文件中搜索关键词
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="keyword">搜索关键词（调用方已保证非空）</param>
        /// <param name="ct">取消令牌：C1 修复 —— 搜索被取消/超时后，解析流程在边界处协作式退出，
        /// 避免"孤儿任务"在取消后继续完整解析大文件</param>
        public List<SearchResult> SearchInFile(string filePath, string keyword, CancellationToken ct = default)
        {
            var results = new List<SearchResult>();

            // 空关键词防御：UI 已拒绝空关键词，此处兜底 —— IndexOf("") 会从同一位置
            // 反复命中直至触及 MaxMatchesPerFile，产生 10 条无意义的重复结果
            if (string.IsNullOrEmpty(keyword))
                return results;

            try
            {
                if (_textParser.CanParse(filePath))
                {
                    // F9 修复：纯文本文件走流式逐行匹配 —— 内存占用与文件大小解耦
                    //（此前 File.ReadAllBytes 整文件入内存，100MB 上限 × 并行度峰值可达数 GB）
                    results = SearchInTextFile(filePath, keyword, ct);
                }
                else
                {
                    string content = ParseFile(filePath);
                    if (string.IsNullOrEmpty(content) || ct.IsCancellationRequested)
                        return results;

                    results = FindMatches(filePath, content, keyword, ct);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // F3 修复：单文件失败不中断整个搜索，但必须留下原因；
                // OOM 不吞 —— 继续运行可能恶化状态，交由上层统一处理
                Errors.Enqueue($"{Path.GetFileName(filePath)}：{ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// 根据文件类型选择解析器（二进制 Office 格式；纯文本已在上游分流到流式路径）
        /// </summary>
        private string ParseFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".docx" => _docxParser.Parse(filePath),
                ".xlsx" => _xlsxParser.Parse(filePath),
                ".pptx" => _pptxParser.Parse(filePath),
                ".doc" => _legacyParser.ParseDoc(filePath),
                ".xls" => _legacyParser.ParseXls(filePath),
                ".ppt" => _legacyParser.ParsePpt(filePath),
                _ => string.Empty
            };
        }

        /// <summary>
        /// F9 修复：纯文本文件流式搜索 —— 逐行读取匹配。
        /// 行号在逐行读取时天然获得（F5 修复），无需此前 O(匹配数 × 文件长度) 的
        /// GetLineNumber 回溯扫描；达到单文件上限即停止读取，大文件提前收工。
        /// 行为变化：上下文在行内截取（此前可跨行拼接后把换行替换为空格）。
        /// </summary>
        private List<SearchResult> SearchInTextFile(string filePath, string keyword, CancellationToken ct)
        {
            var results = new List<SearchResult>();
            int matchCount = 0;

            // 单次打开文件：先探测编码（只读流头部样本），再重置到起点用该编码逐行读取，
            // 避免探测/读取两次打开产生 TOCTOU 窗口
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Encoding encoding = EncodingHelper.DetectEncoding(stream);
                stream.Position = 0; // 探测只消费了头部样本，重置后交给 StreamReader 完整读取

                using var reader = new StreamReader(stream, encoding);
                int lineNumber = 0;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    // C1：逐行检查取消，长文件可尽快退出
                    if (ct.IsCancellationRequested)
                        break;

                    int searchIndex = 0;
                    while (matchCount < MaxMatchesPerFile)
                    {
                        int foundIndex = line.IndexOf(keyword, searchIndex, StringComparison.OrdinalIgnoreCase);
                        if (foundIndex < 0)
                            break;

                        matchCount++;
                        results.Add(new SearchResult
                        {
                            FilePath = filePath,
                            MatchIndex = matchCount,
                            LineNumber = lineNumber,
                            Context = BuildContext(line, foundIndex, keyword.Length)
                        });
                        searchIndex = foundIndex + keyword.Length;
                    }

                    // 达到单文件上限：停止读取整个文件
                    if (matchCount >= MaxMatchesPerFile)
                        break;
                }
            }

            return results;
        }

        /// <summary>
        /// 构建匹配上下文：关键词前后各 ContextLength 字符（行内截取，天然不越界）
        /// </summary>
        private static string BuildContext(string line, int foundIndex, int keywordLength)
        {
            int contextStart = Math.Max(0, foundIndex - ContextLength);
            int contextEnd = Math.Min(line.Length, foundIndex + keywordLength + ContextLength);
            // 行内容不含换行符（ReadLine 已剥离行结束符），无需再做换行替换
            return line.Substring(contextStart, contextEnd - contextStart).Trim();
        }

        /// <summary>
        /// 在解析出的整段文本中查找所有匹配，提取上下文。
        /// 仅用于 docx/xlsx/pptx/doc/xls/ppt 等二进制 Office 格式 —— 此类格式无"行"概念，
        /// LineNumber 恒为 0；纯文本文件的行号由 SearchInTextFile 逐行提供。
        /// </summary>
        private List<SearchResult> FindMatches(string filePath, string content, string keyword, CancellationToken ct)
        {
            var results = new List<SearchResult>();
            int matchCount = 0;
            int searchIndex = 0;

            while (matchCount < MaxMatchesPerFile)
            {
                // C1：每个匹配间隙检查取消，长文本多匹配时可尽快退出
                if (ct.IsCancellationRequested)
                    break;

                int foundIndex = content.IndexOf(keyword, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    break;

                matchCount++;

                // 提取上下文：关键词前后各50字符
                int contextStart = Math.Max(0, foundIndex - ContextLength);
                int contextEnd = Math.Min(content.Length, foundIndex + keyword.Length + ContextLength);
                string context = content.Substring(contextStart, contextEnd - contextStart);

                // 替换换行符为空格
                context = context.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

                results.Add(new SearchResult
                {
                    FilePath = filePath,
                    MatchIndex = matchCount,
                    LineNumber = 0,
                    Context = context.Trim()
                });

                searchIndex = foundIndex + keyword.Length;
            }

            return results;
        }
    }
}
