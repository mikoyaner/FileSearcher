using System.Text;
using FileSearcher.Services;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 旧版二进制格式（.doc/.ppt）启发式文本提取回归测试
    /// </summary>
    public class LegacyParserTests : IDisposable
    {
        private readonly LegacyParserService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void Doc_BinaryExtraction_FindsUtf16LeText()
        {
            // 模拟 OLE 文件结构：文件头噪声 + UTF-16LE 正文 + 尾部噪声
            byte[] headerNoise = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00 };
            byte[] text = Encoding.Unicode.GetBytes("Hello你好世界，可提取的正文内容");
            byte[] tailNoise = { 0x12, 0x34, 0x00, 0x00, 0xAB, 0xCD };
            byte[] bytes = headerNoise.Concat(text).Concat(tailNoise).ToArray();
            string path = TestHelpers.WriteAllBytes(_dir, "sample.doc", bytes);

            string content = _service.ParseDoc(path);

            Assert.Contains("Hello你好", content);
        }

        [Fact]
        public void Ppt_BinaryExtraction_FindsUtf16LeText()
        {
            byte[] text = Encoding.Unicode.GetBytes("幻灯片正文关键词出现这里");
            string path = TestHelpers.WriteAllBytes(_dir, "sample.ppt", text);

            string content = _service.ParsePpt(path);

            Assert.Contains("幻灯片正文", content);
        }

        [Fact]
        public void Doc_ShortSegments_FilteredOut()
        {
            // 长度 < 4 的可打印段应被过滤，不产生垃圾文本
            byte[] text = Encoding.Unicode.GetBytes("ab");
            string path = TestHelpers.WriteAllBytes(_dir, "short.doc", text);

            string content = _service.ParseDoc(path);

            Assert.DoesNotContain("ab", content);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
