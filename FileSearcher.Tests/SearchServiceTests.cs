using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileSearcher.Services;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 搜索服务回归测试：匹配、上下文、行号、单文件上限与 docx 正文解析
    /// </summary>
    public class SearchServiceTests : IDisposable
    {
        private readonly SearchService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void PlainText_FindsMatch_WithLineNumber()
        {
            string path = TestHelpers.WriteAllText(_dir, "sample.txt",
                "first line\nsecond line contains 目标词 here\nthird line");

            var results = _service.SearchInFile(path, "目标词");

            var result = Assert.Single(results);
            Assert.Equal(path, result.FilePath);
            Assert.Equal(1, result.MatchIndex);
            Assert.Equal(2, result.LineNumber);
            Assert.Contains("目标词", result.Context);
        }

        [Fact]
        public void PlainText_IsCaseInsensitive()
        {
            string path = TestHelpers.WriteAllText(_dir, "case.txt", "Hello World and hello again");

            Assert.Equal(2, _service.SearchInFile(path, "hello").Count);
        }

        [Fact]
        public void PlainText_CapsAtTenMatchesPerFile()
        {
            // 一行 15 个命中：单文件最多返回 10 条，防止极端文件拖垮 UI
            string path = TestHelpers.WriteAllText(_dir, "many.txt",
                string.Join(" ", Enumerable.Repeat("hit", 15)));

            Assert.Equal(10, _service.SearchInFile(path, "hit").Count);
        }

        [Fact]
        public void Context_ClampedAtFileStart()
        {
            // 关键词位于文件开头：上下文不允许越界，且以关键词本身开头
            string path = TestHelpers.WriteAllText(_dir, "start.txt", "关键词 at the very beginning of file");

            var result = Assert.Single(_service.SearchInFile(path, "关键词"));

            Assert.StartsWith("关键词", result.Context);
        }

        [Fact]
        public void Context_ClampedAtFileEnd()
        {
            string path = TestHelpers.WriteAllText(_dir, "end.txt", "file ends with 关键词");

            var result = Assert.Single(_service.SearchInFile(path, "关键词"));

            Assert.EndsWith("关键词", result.Context);
        }

        [Fact]
        public void EmptyFile_ReturnsNoResults()
        {
            string path = TestHelpers.WriteAllText(_dir, "empty.txt", "");

            Assert.Empty(_service.SearchInFile(path, "任何词"));
        }

        [Fact]
        public void EmptyKeyword_ReturnsNoResults()
        {
            // F3 回归：空关键词兜底防御 —— 修复前 IndexOf("") 会在同一位置反复命中，
            // 返回 10 条无意义结果
            string path = TestHelpers.WriteAllText(_dir, "kw.txt", "some content here");

            Assert.Empty(_service.SearchInFile(path, ""));
        }

        [Fact]
        public void SearchInFile_RecordsError_WhenFileLocked()
        {
            // F3 回归：文件被独占锁定时不应静默失败 —— 结果为空但 Errors 必须留痕
            string path = TestHelpers.WriteAllText(_dir, "locked.txt", "含有关键词的内容");
            var service = new SearchService();

            // 以独占方式锁定文件（FileShare.None），模拟被占用且禁止共享读取的场景
            using (var lockStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var results = service.SearchInFile(path, "关键词");

                Assert.Empty(results);
                Assert.NotEmpty(service.Errors);
                Assert.Contains("locked.txt", service.Errors.Single());
            }
        }

        [Fact]
        public void Streaming_LineNumber_AccurateDeepInLargeFile()
        {
            // F5/F9 回归：5000 行文件中第 4321 行包含关键词 ——
            // 行号必须来自流式读取的计数；修复前的 GetLineNumber 对每个匹配从文件头
            // 重新扫描换行符，O(匹配数×文件长度)，且流式改造后该方法已删除
            var lines = Enumerable.Range(1, 5000).Select(i => $"第 {i} 行普通内容").ToList();
            lines[4320] = "line 4321 contains 目标词 here";
            string path = TestHelpers.WriteAllText(_dir, "deep.txt", string.Join("\n", lines));

            var result = Assert.Single(_service.SearchInFile(path, "目标词"));

            Assert.Equal(4321, result.LineNumber);
            Assert.Contains("目标词", result.Context);
        }

        [Fact]
        public void Streaming_ReachesEnd_UnderMatchCap()
        {
            // F9 回归：达到单文件上限后提前停止读取 —— 结果仍精确为 10 条且序号连续
            var lines = Enumerable.Range(1, 500).Select(i => $"hit on line {i}").ToList();
            string path = TestHelpers.WriteAllText(_dir, "cap.txt", string.Join("\n", lines));

            var results = _service.SearchInFile(path, "hit");

            Assert.Equal(10, results.Count);
            Assert.Equal(Enumerable.Range(1, 10), results.Select(r => r.MatchIndex));
            Assert.Equal(Enumerable.Range(1, 10), results.Select(r => r.LineNumber));
        }

        [Fact]
        public void Docx_BodyParagraph_Found()
        {
            string path = Path.Combine(_dir, "sample.docx");
            CreateDocx(path, body => body.Append(new Paragraph(new Run(new Text("正文段落包含秘密关键词")))));

            Assert.NotEmpty(_service.SearchInFile(path, "秘密关键词"));
        }

        [Fact]
        public void Docx_NoMatch_ReturnsEmpty()
        {
            string path = Path.Combine(_dir, "nomatch.docx");
            CreateDocx(path, body => body.Append(new Paragraph(new Run(new Text("完全无关的内容")))));

            Assert.Empty(_service.SearchInFile(path, "秘密关键词"));
        }

        [Fact]
        public void Docx_TableParagraph_Found()
        {
            // F1 回归：表格内的段落位于 body 的更深层级（w:tbl/w:tr/w:tc/w:p），
            // 修复前 Elements&lt;Paragraph&gt; 只读直接子节点 → 表格内容漏检
            string path = Path.Combine(_dir, "table.docx");
            CreateDocx(path, body =>
            {
                body.Append(new Paragraph(new Run(new Text("普通段落"))));
                body.Append(new Table(
                    new TableRow(
                        new TableCell(
                            new Paragraph(new Run(new Text("表格单元格内的秘密关键词")))))));
            });

            var results = _service.SearchInFile(path, "秘密关键词");

            var result = Assert.Single(results);
            Assert.Contains("表格单元格内", result.Context);
        }

        [Fact]
        public void Docx_HeaderAndFooter_Found()
        {
            // F1 回归：页眉/页脚是独立 Part，修复前完全不读取
            string path = Path.Combine(_dir, "header.docx");
            using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(
                    new Paragraph(new Run(new Text("正文内容")))));

                var headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new Header(new Paragraph(new Run(new Text("页眉里的秘密关键词"))));

                var footerPart = mainPart.AddNewPart<FooterPart>();
                footerPart.Footer = new Footer(new Paragraph(new Run(new Text("页脚里的机密词汇"))));
            }

            Assert.NotEmpty(_service.SearchInFile(path, "秘密关键词"));
            Assert.NotEmpty(_service.SearchInFile(path, "机密词汇"));
        }

        /// <summary>
        /// 创建最小可读的 .docx 文件：仅包含主文档部件与调用方注入的 Body 内容
        /// </summary>
        private static void CreateDocx(string path, Action<Body> configureBody)
        {
            using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            var body = new Body();
            configureBody(body);
            mainPart.Document = new Document(body);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
