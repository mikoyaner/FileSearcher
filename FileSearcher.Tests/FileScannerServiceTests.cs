using FileSearcher.Services;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// 文件扫描回归测试：递归收集、取消令牌、junction 环防御
    /// </summary>
    public class FileScannerServiceTests : IDisposable
    {
        private readonly FileScannerService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void ScanFiles_FindsSupportedFiles_Recursively()
        {
            // 基线行为：递归收集支持的后缀，跳过不支持的
            TestHelpers.WriteAllText(_dir, "a.txt", "hello");
            string sub = Path.Combine(_dir, "sub");
            Directory.CreateDirectory(sub);
            TestHelpers.WriteAllText(sub, "b.docx", "fake content");
            TestHelpers.WriteAllText(_dir, "c.unsupported", "ignored");

            var files = _service.ScanFiles(_dir);

            Assert.Equal(2, files.Count);
            Assert.Contains(files, f => f.EndsWith("a.txt"));
            Assert.Contains(files, f => f.EndsWith("b.docx"));
        }

        [Fact]
        public void ScanFiles_ThrowsOperationCanceled_WhenTokenAlreadyCancelled()
        {
            // F6 回归：扫描阶段必须响应取消令牌 —— 修复前扫描不可取消，
            // 大目录上点"停止"后扫描仍在后台跑完
            TestHelpers.WriteAllText(_dir, "a.txt", "hello");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => _service.ScanFiles(_dir, cts.Token));
        }

        [Fact]
        public void ScanFiles_TerminatesOnJunctionCycle()
        {
            // F6 回归：junction 自环（root/loop → root）不得导致无限递归。
            // 修复前仅靠 Hidden|System 属性过滤兼容性 junction，用户自建的无隐藏属性
            // junction 环会让 Directory.EnumerateDirectories 无限深入
            string nested = Path.Combine(_dir, "nested");
            Directory.CreateDirectory(nested);
            TestHelpers.WriteAllText(nested, "inside.txt", "hello");

            string link = Path.Combine(_dir, "loop");
            if (!CreateJunction(link, _dir))
            {
                // 环境不允许创建 junction（FAT 分区/组策略）时退化为普通扫描验证
                Assert.Single(_service.ScanFiles(_dir));
                return;
            }

            // ReparsePoint 过滤 + visited 集合双重防御下必须有限时间内完成
            var files = _service.ScanFiles(_dir);

            Assert.Contains(files, f => f.EndsWith("inside.txt"));
            // loop 指向扫描根自身，若被误入栈会在 inside.txt 之外产生重复文件路径
            Assert.Single(files);
        }

        /// <summary>
        /// 通过 mklink /J 创建目录 junction（普通用户权限即可创建，无需管理员）
        /// </summary>
        /// <returns>是否创建成功（失败表示当前环境不支持 junction）</returns>
        private static bool CreateJunction(string linkPath, string targetPath)
        {
            try
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                process?.WaitForExit(5000);
                return process?.ExitCode == 0 && Directory.Exists(linkPath);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
