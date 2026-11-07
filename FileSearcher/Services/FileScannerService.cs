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
        /// <param name="rootPath">扫描根目录</param>
        /// <param name="token">F6 修复：取消令牌 —— 此前扫描阶段不可取消，用户在大目录上点"停止"
        /// 后扫描仍会在后台跑完（浪费 I/O），立即重启还会出现双扫描并行</param>
        /// <param name="progress">文件发现进度回调（可选）</param>
        public List<string> ScanFiles(string rootPath, CancellationToken token = default, IProgress<string>? progress = null)
        {
            var files = new List<string>();

            // F6 修复：显式栈替代递归 —— 一则避免极深目录树的调用栈压力，
            // 二则配合 visited 集合实现环路检测
            var pending = new Stack<string>();
            pending.Push(rootPath);

            // F6 修复：已访问集合 —— 防御 junction/符号链接造成的目录环
            //（此前仅靠 Hidden|System 属性过滤兼容性 junction，用户自建 junction 环会无限递归）
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (pending.Count > 0)
            {
                // F6 修复：每个目录边界检查取消令牌，扫描可即时终止
                token.ThrowIfCancellationRequested();

                var directory = pending.Pop();
                if (!visited.Add(directory))
                {
                    continue; // 环路：同一目录第二次到达，直接跳过
                }

                try
                {
                    // 收集当前目录下符合条件的文件
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
                            // 文件可能在枚举后被删除/锁定，跳过
                        }
                    }

                    // 子目录入栈（延迟遍历）
                    foreach (var subDir in Directory.EnumerateDirectories(directory))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(subDir);

                            // 跳过隐藏目录、系统目录、重解析点（junction/符号链接）
                            // ReparsePoint 检查是环路防御的关键：junction 的目录项本身可能不带隐藏属性
                            if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                (dirInfo.Attributes & FileAttributes.System) != 0 ||
                                (dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                                continue;

                            pending.Push(subDir);
                        }
                        catch
                        {
                            // 目录元信息不可读（权限不足等），跳过
                        }
                    }
                }
                catch
                {
                    // 目录不可访问（权限不足/已删除），跳过
                }
            }

            return files;
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
