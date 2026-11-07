using System.Text;
using FileSearcher.Helpers;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 编码检测回归测试：BOM 判定与自动检测回退链
    /// （基线覆盖 UTF-8/UTF-16 正确路径；UTF-32 用例随 F4 修复补充）
    /// </summary>
    public class EncodingHelperTests : IDisposable
    {
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void Utf8_WithBom_DecodesCorrectly()
        {
            string content = "中文abc123";
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
            string path = TestHelpers.WriteAllBytes(_dir, "utf8bom.txt", bytes);

            Assert.Equal(content, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        [Fact]
        public void Utf16Le_WithBom_DecodesCorrectly()
        {
            string content = "中文测试文本，用于 UTF-16 LE BOM 判定";
            byte[] bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(content)).ToArray();
            string path = TestHelpers.WriteAllBytes(_dir, "utf16le.txt", bytes);

            Assert.Equal(content, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        [Fact]
        public void Utf16Be_WithBom_DecodesCorrectly()
        {
            string content = "中文测试文本，用于 UTF-16 BE BOM 判定";
            byte[] bytes = Encoding.BigEndianUnicode.GetPreamble()
                .Concat(Encoding.BigEndianUnicode.GetBytes(content)).ToArray();
            string path = TestHelpers.WriteAllBytes(_dir, "utf16be.txt", bytes);

            Assert.Equal(content, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        [Fact]
        public void NoBom_GbkChinese_DecodesCorrectly()
        {
            // 无 BOM 的 GBK 文本：足够长以保证 UtfUnknown 检出 GB18030（GBK 超集，解码结果一致）
            string content = "这是一段用于编码自动检测的中文文本，包含足够多的汉字以便统计检测器识别编码。";
            string path = TestHelpers.WriteAllText(_dir, "gbk.txt", content, Encoding.GetEncoding("GBK"));

            Assert.Equal(content, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        [Fact]
        public void NoBom_Ascii_DecodesCorrectly()
        {
            string content = "plain ascii text without bom 12345";
            string path = TestHelpers.WriteAllText(_dir, "ascii.txt", content, new UTF8Encoding(false));

            Assert.Equal(content, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        [Fact]
        public void EmptyFile_ReturnsEmptyString()
        {
            string path = TestHelpers.WriteAllBytes(_dir, "empty.txt", Array.Empty<byte>());

            Assert.Equal(string.Empty, EncodingHelper.ReadFileWithDetectedEncoding(path));
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
