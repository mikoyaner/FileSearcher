using System.IO;
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
        public List<SearchResult> SearchInFile(string filePath, string keyword)
        {
            var results = new List<SearchResult>();

            try
            {
                string content = ParseFile(filePath);
                if (string.IsNullOrEmpty(content))
                    return results;

                // 不区分大小写搜索
                results = FindMatches(filePath, content, keyword);
            }
            catch
            {
                // 单个文件解析失败不中断整个搜索
            }

            return results;
        }

        /// <summary>
        /// 根据文件类型选择解析器
        /// </summary>
        private string ParseFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".txt" or ".csv" or ".log" or ".md" or ".json" or ".xml" or ".html" or ".htm"
                    => _textParser.Parse(filePath),
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
        /// 在文本内容中查找所有匹配，提取上下文
        /// </summary>
        private List<SearchResult> FindMatches(string filePath, string content, string keyword)
        {
            var results = new List<SearchResult>();
            int matchCount = 0;
            int searchIndex = 0;

            // 预计算行号（用于纯文本文件）
            bool isTextFile = _textParser.CanParse(filePath);

            while (matchCount < MaxMatchesPerFile)
            {
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

                // 计算行号（如适用）
                int lineNumber = 0;
                if (isTextFile)
                {
                    lineNumber = GetLineNumber(content, foundIndex);
                }

                results.Add(new SearchResult
                {
                    FilePath = filePath,
                    MatchIndex = matchCount,
                    LineNumber = lineNumber,
                    Context = context.Trim()
                });

                searchIndex = foundIndex + keyword.Length;
            }

            return results;
        }

        /// <summary>
        /// 计算指定位置所在的行号
        /// </summary>
        private int GetLineNumber(string content, int position)
        {
            int line = 1;
            for (int i = 0; i < position && i < content.Length; i++)
            {
                if (content[i] == '\n')
                    line++;
            }
            return line;
        }
    }
}
