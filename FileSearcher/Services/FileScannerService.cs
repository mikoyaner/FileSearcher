using System.IO;

namespace FileSearcher.Services
{
    /// <summary>
    /// 文件扫描服务 - 递归遍历文件夹
    /// </summary>
    public class FileScannerService
    {
        // 支持的文件后缀
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".log", ".md", ".json", ".xml", ".html", ".htm",
            ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt"
        };

        // 最大文件大小：100MB
        private const long MaxFileSize = 100L * 1024 * 1024;

        /// <summary>
        /// 递归扫描文件夹，返回所有符合条件的文件路径
        /// </summary>
        public List<string> ScanFiles(string rootPath, IProgress<string>? progress = null)
        {
            var files = new List<string>();
            ScanDirectory(rootPath, files, progress);
            return files;
        }

        private void ScanDirectory(string directory, List<string> files, IProgress<string>? progress)
        {
            try
            {
                // 获取当前目录下的文件
                foreach (var filePath in Directory.EnumerateFiles(directory))
                {
                    try
                    {
                        if (IsValidFile(filePath))
                        {
                            files.Add(filePath);
                            progress?.Report(filePath);
                        }
                    }
                    catch
                    {
                        // 跳过无法访问的文件
                    }
                }

                // 递归子目录
                foreach (var subDir in Directory.EnumerateDirectories(directory))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(subDir);

                        // 跳过隐藏目录和系统目录
                        if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                            (dirInfo.Attributes & FileAttributes.System) != 0)
                            continue;

                        ScanDirectory(subDir, files, progress);
                    }
                    catch
                    {
                        // 跳过无法访问的目录（权限不足等）
                    }
                }
            }
            catch
            {
                // 跳过无法访问的目录
            }
        }

        /// <summary>
        /// 判断文件是否有效（后缀、大小、属性等）
        /// </summary>
        private bool IsValidFile(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            // 跳过隐藏文件
            if ((fileInfo.Attributes & FileAttributes.Hidden) != 0)
                return false;

            // 跳过系统文件
            if ((fileInfo.Attributes & FileAttributes.System) != 0)
                return false;

            // 跳过 ~$ 开头的临时文件
            if (fileInfo.Name.StartsWith("~$"))
                return false;

            // 检查后缀
            string ext = fileInfo.Extension;
            if (!SupportedExtensions.Contains(ext))
                return false;

            // 跳过超过 100MB 的文件
            if (fileInfo.Length > MaxFileSize)
                return false;

            return true;
        }
    }
}
