using System.Text;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 测试辅助：为每个用例创建独立临时目录，避免 xUnit 并行执行时互相干扰
    /// </summary>
    internal static class TestHelpers
    {
        static TestHelpers()
        {
            // 与生产代码一致：注册 GBK/GB2312 等代码页编码，测试中直接构造 GBK 字节时需要
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 创建独立的临时测试目录（位于系统临时目录下，用例结束时由 Dispose 清理）
        /// </summary>
        public static string CreateTempDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DocSearcherTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// 在临时目录中写入二进制文件并返回完整路径
        /// </summary>
        public static string WriteAllBytes(string dir, string fileName, byte[] content)
        {
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, content);
            return path;
        }

        /// <summary>
        /// 在临时目录中写入文本文件（默认无 BOM UTF-8）并返回完整路径
        /// </summary>
        public static string WriteAllText(string dir, string fileName, string content, Encoding? encoding = null)
        {
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
            return path;
        }
    }
}
