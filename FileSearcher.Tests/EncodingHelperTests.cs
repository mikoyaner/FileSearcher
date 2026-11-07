using System.Text;
using FileSearcher.Helpers;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 编码检测回归测试：BOM 判定与自动检测回退链
    /// F9 重构后 API 为 DetectEncoding(Stream)（采样头部 64KB），
    /// 解码正确性通过"探测 + StreamReader 读回"组合验证，与生产用法一致
    /// </summary>
    public class EncodingHelperTests : IDisposable
    {
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void DetectEncoding_Utf8Bom_ReturnsUtf8()
        {
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin",
                Encoding.UTF8.GetPreamble()));

            Assert.Equal(Encoding.UTF8, encoding);
        }

        [Fact]
        public void DetectEncoding_Utf16LeBom_ReturnsUnicode()
        {
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin",
                Encoding.Unicode.GetPreamble()));

            Assert.Equal(Encoding.Unicode, encoding);
        }

        [Fact]
        public void DetectEncoding_Utf16BeBom_ReturnsBigEndianUnicode()
        {
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin",
                Encoding.BigEndianUnicode.GetPreamble()));

            Assert.Equal(Encoding.BigEndianUnicode, encoding);
        }

        [Fact]
        public void DetectEncoding_Utf32LeBom_ReturnsUtf32()
        {
            // F4 回归：UTF-32 LE BOM（FF FE 00 00）是 UTF-16 LE BOM（FF FE）的扩展前缀，
            // 修复前先命中 UTF-16 分支 → 误解码为乱码
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin",
                Encoding.UTF32.GetPreamble()));

            Assert.Equal(Encoding.UTF32, encoding);
        }

        [Fact]
        public void DetectEncoding_Utf32BeBom_ReturnsUtf32BigEndian()
        {
            // F4 回归：修复前完全没有 UTF-32 BE 分支
            var beEncoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin", beEncoding.GetPreamble()));

            Assert.Equal(beEncoding, encoding);
        }

        [Fact]
        public void DetectEncoding_EmptyStream_ReturnsUtf8()
        {
            var encoding = Detect(TestHelpers.WriteAllBytes(_dir, "t.bin", Array.Empty<byte>()));

            Assert.Equal(Encoding.UTF8, encoding);
        }

        // —— 解码端到端：探测编码 + StreamReader 读回，与 SearchService 生产路径一致 ——

        [Fact]
        public void Utf8_WithBom_DecodesCorrectly()
        {
            string content = "中文abc123";
            string path = TestHelpers.WriteAllBytes(_dir, "utf8bom.txt",
                Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray());

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        [Fact]
        public void Utf16Le_WithBom_DecodesCorrectly()
        {
            string content = "中文测试文本，用于 UTF-16 LE BOM 判定";
            string path = TestHelpers.WriteAllBytes(_dir, "utf16le.txt",
                Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray());

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        [Fact]
        public void Utf16Be_WithBom_DecodesCorrectly()
        {
            string content = "中文测试文本，用于 UTF-16 BE BOM 判定";
            string path = TestHelpers.WriteAllBytes(_dir, "utf16be.txt",
                Encoding.BigEndianUnicode.GetPreamble()
                    .Concat(Encoding.BigEndianUnicode.GetBytes(content)).ToArray());

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        [Fact]
        public void Utf32Le_WithBom_DecodesCorrectly()
        {
            // F4 端到端回归：此前 UTF-32 LE 文件解码为乱码，关键词搜索必然失败
            string content = "中文内容，用于验证 UTF-32 LE BOM 判定顺序";
            string path = TestHelpers.WriteAllBytes(_dir, "utf32le.txt",
                Encoding.UTF32.GetPreamble().Concat(Encoding.UTF32.GetBytes(content)).ToArray());

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        [Fact]
        public void NoBom_GbkChinese_DecodesCorrectly()
        {
            // 无 BOM 的 GBK 文本：足够长以保证 UtfUnknown 检出 GB18030（GBK 超集，解码结果一致）
            string content = "这是一段用于编码自动检测的中文文本，包含足够多的汉字以便统计检测器识别编码。";
            string path = TestHelpers.WriteAllText(_dir, "gbk.txt", content, Encoding.GetEncoding("GBK"));

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        [Fact]
        public void NoBom_Ascii_DecodesCorrectly()
        {
            string content = "plain ascii text without bom 12345";
            string path = TestHelpers.WriteAllText(_dir, "ascii.txt", content, new UTF8Encoding(false));

            Assert.Equal(content, ReadAllTextDetected(path));
        }

        /// <summary>
        /// 与 SearchService.SearchInTextFile 相同的生产读取方式：探测编码 → 重置流 → StreamReader 全文读回
        /// </summary>
        private static string ReadAllTextDetected(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var encoding = EncodingHelper.DetectEncoding(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream, encoding);
            return reader.ReadToEnd();
        }

        private static Encoding Detect(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return EncodingHelper.DetectEncoding(stream);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
