using System.IO;
using System.Text;
using UtfUnknown;

namespace FileSearcher.Helpers
{
    /// <summary>
    /// 编码检测辅助类
    /// </summary>
    public static class EncodingHelper
    {
        static EncodingHelper()
        {
            // 注册编码提供程序，确保 GBK/GB2312 可用
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 检测文件编码并读取全部文本
        /// </summary>
        public static string ReadFileWithDetectedEncoding(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

            if (fileBytes.Length == 0)
                return string.Empty;

            // 1. 检测 BOM 头
            Encoding? bomEncoding = DetectBom(fileBytes);
            if (bomEncoding != null)
            {
                return bomEncoding.GetString(fileBytes, bomEncoding.GetPreamble().Length,
                    fileBytes.Length - bomEncoding.GetPreamble().Length);
            }

            // 2. 使用 UtfUnknown 自动检测
            try
            {
                var detectionResult = CharsetDetector.DetectFromBytes(fileBytes);
                if (detectionResult.Detected != null && detectionResult.Detected.Confidence > 0.5)
                {
                    return detectionResult.Detected.Encoding.GetString(fileBytes);
                }
            }
            catch
            {
                // 检测失败，使用默认编码
            }

            // 3. 默认使用 GBK（兼容中文办公环境）
            try
            {
                Encoding gbk = Encoding.GetEncoding("GBK");
                return gbk.GetString(fileBytes);
            }
            catch
            {
                // 最终回退到 UTF-8
                return Encoding.UTF8.GetString(fileBytes);
            }
        }

        /// <summary>
        /// 检测 BOM 头
        /// F4 修复：UTF-32 LE 的 BOM（FF FE 00 00）同时是 UTF-16 LE BOM（FF FE）的前缀，
        /// 因此 4 字节 BOM 必须先于 2 字节 BOM 判定 —— 此前顺序颠倒导致 UTF-32 文件
        /// 被误判为 UTF-16，解码出乱码后搜索必然失败；并补充此前完全缺失的 UTF-32 BE 分支
        /// </summary>
        private static Encoding? DetectBom(byte[] bytes)
        {
            // —— 4 字节 BOM 优先 ——
            // UTF-32 LE BOM: FF FE 00 00
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return Encoding.UTF32;

            // UTF-32 BE BOM: 00 00 FE FF
            if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new UTF32Encoding(bigEndian: true, byteOrderMark: true);

            // —— 2 字节 / 3 字节 BOM ——
            // UTF-8 BOM: EF BB BF
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-16 LE BOM: FF FE
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;

            // UTF-16 BE BOM: FE FF
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            return null;
        }
    }
}
