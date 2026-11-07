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
        // F9 配套：编码探测只采样流头部 64KB —— 探测成本与文件大小解耦，大文件不再整体读入
        private const int SampleSize = 64 * 1024;

        static EncodingHelper()
        {
            // 注册编码提供程序，确保 GBK/GB2312 可用
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// 从已打开的流中探测编码。
        /// 内部只读取头部样本（SampleSize 字节）用于检测，不关闭、不重置流；
        /// 调用方在读取正文前需自行将 Position 重置为 0。
        /// </summary>
        public static Encoding DetectEncoding(Stream stream)
        {
            byte[] sample = new byte[SampleSize];
            int read = 0;
            while (read < SampleSize)
            {
                int n = stream.Read(sample, read, SampleSize - read);
                if (n <= 0) break;
                read += n;
            }

            if (read == 0)
                return Encoding.UTF8;

            // 1. 检测 BOM 头（F4 修复：4 字节 BOM 先于 2 字节 BOM 判定）
            Encoding? bomEncoding = DetectBom(sample, read);
            if (bomEncoding != null)
            {
                return bomEncoding;
            }

            // 2. 使用 UtfUnknown 对样本做统计检测（仅用实际读到的字节）
            try
            {
                var detectionResult = CharsetDetector.DetectFromBytes(sample, 0, read);
                if (detectionResult.Detected != null && detectionResult.Detected.Confidence > 0.5)
                {
                    return detectionResult.Detected.Encoding;
                }
            }
            catch
            {
                // 检测失败，使用默认编码
            }

            // 3. 默认使用 GBK（兼容中文办公环境；GBK 解码不抛异常，未知字节被替换）
            try
            {
                return Encoding.GetEncoding("GBK");
            }
            catch
            {
                // 最终回退到 UTF-8
                return Encoding.UTF8;
            }
        }

        /// <summary>
        /// 检测 BOM 头
        /// F4 修复：UTF-32 LE 的 BOM（FF FE 00 00）同时是 UTF-16 LE BOM（FF FE）的前缀，
        /// 因此 4 字节 BOM 必须先于 2 字节 BOM 判定 —— 此前顺序颠倒导致 UTF-32 文件
        /// 被误判为 UTF-16，解码出乱码后搜索必然失败；并补充此前完全缺失的 UTF-32 BE 分支
        /// </summary>
        private static Encoding? DetectBom(byte[] bytes, int length)
        {
            // —— 4 字节 BOM 优先 ——
            // UTF-32 LE BOM: FF FE 00 00
            if (length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                return Encoding.UTF32;

            // UTF-32 BE BOM: 00 00 FE FF
            if (length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new UTF32Encoding(bigEndian: true, byteOrderMark: true);

            // —— 3 字节 / 2 字节 BOM ——
            // UTF-8 BOM: EF BB BF
            if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8;

            // UTF-16 LE BOM: FF FE
            if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;

            // UTF-16 BE BOM: FE FF
            if (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            return null;
        }
    }
}
