namespace FileSearcher.Models
{
    /// <summary>
    /// 搜索结果数据模型
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// 文件完整路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 匹配序号（在该文件中的第几个匹配）
        /// </summary>
        public int MatchIndex { get; set; }

        /// <summary>
        /// 匹配行号（如适用，纯文本文件有效）
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// 上下文内容（关键词前后各50字符）
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// 格式化为显示字符串
        /// </summary>
        public override string ToString()
        {
            var lineInfo = LineNumber > 0 ? $"[行 {LineNumber}] " : "";
            return $"[{MatchIndex}] {lineInfo}{Context}";
        }
    }
}
