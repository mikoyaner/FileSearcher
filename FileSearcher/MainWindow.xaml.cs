using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FileSearcher.Models;
using FileSearcher.Services;
using Microsoft.Win32;

namespace FileSearcher
{
    public partial class MainWindow : Window
    {
        private readonly FileScannerService _scannerService;
        private readonly SearchService _searchService;
        private readonly CsvExportService _csvExportService;

        private string _selectedFolder = string.Empty;
        private List<SearchResult> _searchResults = new();
        private CancellationTokenSource? _cts;

        // 实时结果缓冲队列（后台线程写入，UI线程定时读取）
        private readonly ConcurrentQueue<SearchResult> _pendingResults = new();
        private DispatcherTimer? _uiTimer;
        private int _processedCount;
        private int _totalFiles;
        private string _currentKeyword = string.Empty;
        private string? _lastDisplayedFile;
        private string? _currentProcessingFile;
        private bool _isSearching;
        private volatile bool _stopRequested;

        // 每次刷新最多写入的结果数（防止UI卡顿）
        private const int MaxFlushPerTick = 100;

        public MainWindow()
        {
            InitializeComponent();
            _scannerService = new FileScannerService();
            _searchService = new SearchService();
            _csvExportService = new CsvExportService();
        }

        /// <summary>
        /// 选择文件夹
        /// </summary>
        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择要搜索的文件夹",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedFolder = dialog.SelectedPath;
                TxtFolderPath.Text = _selectedFolder;
                TxtFolderPath.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        /// <summary>
        /// 回车触发搜索
        /// </summary>
        private void TxtKeyword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isSearching)
            {
                BtnSearch_Click(sender, e);
            }
        }

        /// <summary>
        /// 开始搜索
        /// </summary>
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            string keyword = TxtKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                System.Windows.MessageBox.Show("请输入搜索关键词。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtKeyword.Focus();
                return;
            }

            if (string.IsNullOrEmpty(_selectedFolder) || !Directory.Exists(_selectedFolder))
            {
                System.Windows.MessageBox.Show("请先选择有效的文件夹。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 取消之前的搜索
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // 初始化状态
            _currentKeyword = keyword;
            _searchResults.Clear();
            _pendingResults.Clear();
            _processedCount = 0;
            _totalFiles = 0;
            _lastDisplayedFile = null;
            _currentProcessingFile = null;
            _stopRequested = false;
            TxtResults.Clear();
            BtnExport.IsEnabled = false;
            SetSearchingState(true);

            // 启动 UI 定时刷新器（每200ms批量刷新结果到界面）
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            try
            {
                // 1. 扫描文件（后台线程）
                var files = await Task.Run(() => _scannerService.ScanFiles(_selectedFolder), token);
                _totalFiles = files.Count;

                ProgressBar.Maximum = _totalFiles;
                TxtStatus.Text = $"共 {_totalFiles} 个文件";

                if (token.IsCancellationRequested) return;

                // 2. 并行搜索
                // C1 修复：此前为 Parallel.ForEach 工作体内嵌 Task.Run + workTask.Wait(5s) 同步阻塞，
                // P 个并行体全部阻塞等待自己排队的内层任务 → 线程池饥饿、实际并行度被线程注入速率钳制、
                // 内层任务可能仅因排队而"伪超时"被静默跳过。现改为 ForEachAsync + WaitAsync：
                //   - 等待超时不再占用线程池线程（异步等待）；
                //   - 超时（TimeoutException）与取消（OperationCanceledException）可区分，不再混淆；
                //   - 内层任务携带 token，在解析边界协作式提前退出，孤儿任务可尽早止血。
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = token
                };

                try
                {
                    await Parallel.ForEachAsync(files, options, async (filePath, ct) =>
                    {
                        try
                        {
                            _currentProcessingFile = Path.GetFileName(filePath);

                            // C1：单文件 5 秒超时保护 —— WaitAsync 超时抛 TimeoutException，token 取消抛 OCE
                            List<SearchResult> fileResults =
                                await Task.Run(() => _searchService.SearchInFile(filePath, keyword, ct), ct)
                                          .WaitAsync(TimeSpan.FromSeconds(5), ct);

                            // C2 修复：入队守卫使用本次搜索的 ct（闭包捕获，代际隔离）。
                            // 此前用的是共享字段 _stopRequested —— 它会被下一次搜索复位为 false，
                            // 导致旧搜索的残留工作体把旧关键词结果混入新搜索（结果串扰 + 进度污染）。
                            if (!ct.IsCancellationRequested)
                            {
                                foreach (var r in fileResults)
                                {
                                    _pendingResults.Enqueue(r);
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            // 单文件超时跳过（F3 将补充失败记录）
                        }
                        catch (OperationCanceledException)
                        {
                            // 搜索被取消：交由 ForEachAsync 统一向外传播，本工作体退出
                            throw;
                        }
                        catch
                        {
                            // 单文件失败不中断搜索（F3 将补充失败记录）
                        }
                        finally
                        {
                            // C2：进度计数同样以 ct 为代际守卫，防止旧代际污染新搜索的进度条
                            if (!ct.IsCancellationRequested)
                            {
                                Interlocked.Increment(ref _processedCount);
                            }
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // 取消时正常退出
                }
            }
            catch (OperationCanceledException)
            {
                // 搜索被取消 - 正常
            }
            catch (Exception ex)
            {
                if (!_stopRequested)
                {
                    System.Windows.MessageBox.Show($"搜索过程中发生错误：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                // 停止定时器
                _uiTimer?.Stop();
                _uiTimer = null;
                _currentProcessingFile = null;

                // 取消时丢弃未刷新的结果（防止卡死）；正常完成时刷新剩余
                if (!_stopRequested)
                {
                    FlushPendingResults(MaxFlushPerTick * 5);
                }
                _pendingResults.Clear();

                FinishSearch(_stopRequested || token.IsCancellationRequested);
            }
        }

        /// <summary>
        /// 停止搜索按钮
        /// </summary>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _stopRequested = true;
            _cts?.Cancel();
            BtnStop.IsEnabled = false;
        }

        /// <summary>
        /// 定时器回调：批量刷新搜索结果到 UI
        /// </summary>
        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            FlushPendingResults(MaxFlushPerTick);

            // 更新进度条和状态
            int processed = _processedCount;
            if (processed > _totalFiles) processed = _totalFiles;
            ProgressBar.Value = processed;

            if (_stopRequested)
            {
                TxtStatus.Text = $"正在停止... 已处理：{processed}/{_totalFiles}";
            }
            else
            {
                string currentFile = _currentProcessingFile ?? "";
                if (processed < _totalFiles && !string.IsNullOrEmpty(currentFile))
                {
                    TxtStatus.Text = $"进度：{processed}/{_totalFiles} | 已找到：{_searchResults.Count} 条 | 正在处理：{currentFile}";
                }
                else
                {
                    TxtStatus.Text = $"进度：{processed}/{_totalFiles} | 已找到：{_searchResults.Count} 条";
                }
            }
        }

        /// <summary>
        /// 将缓冲队列中的结果批量写入界面（限制每次最多 maxCount 条，防止UI卡顿）
        /// </summary>
        private void FlushPendingResults(int maxCount)
        {
            if (_pendingResults.IsEmpty) return;

            var sb = new StringBuilder();
            int flushed = 0;

            while (flushed < maxCount && _pendingResults.TryDequeue(out var result))
            {
                _searchResults.Add(result);

                if (result.FilePath != _lastDisplayedFile)
                {
                    sb.AppendLine();
                    sb.AppendLine($"📄 {result.FilePath}");
                    sb.AppendLine(new string('─', 50));
                    _lastDisplayedFile = result.FilePath;
                }

                var lineInfo = result.LineNumber > 0 ? $"[行 {result.LineNumber}] " : "";
                sb.AppendLine($"  [{result.MatchIndex}] {lineInfo}{result.Context}");
                flushed++;
            }

            if (flushed > 0)
            {
                TxtResults.AppendText(sb.ToString());
                TxtResults.ScrollToEnd();
                BtnExport.IsEnabled = true;
            }
        }

        /// <summary>
        /// 搜索完成后的收尾工作
        /// </summary>
        private void FinishSearch(bool wasCancelled)
        {
            SetSearchingState(false);

            // 清除进度状态文字
            TxtStatus.Text = string.Empty;

            if (wasCancelled)
            {
                TxtInfo.Text = $"搜索已停止 | 已匹配：{_searchResults.Count} 条 | 文件夹：{_selectedFolder}";
                if (_searchResults.Count == 0)
                {
                    TxtResults.AppendText("\n搜索已取消，未找到匹配结果。");
                }
            }
            else
            {
                if (_searchResults.Count == 0)
                {
                    TxtResults.Text = $"未找到包含 \"{_currentKeyword}\" 的内容。";
                }
                TxtInfo.Text = $"搜索完成 | 匹配：{_searchResults.Count} 条 | 文件夹：{_selectedFolder}";
            }

            BtnExport.IsEnabled = _searchResults.Count > 0;
        }

        /// <summary>
        /// 导出 CSV
        /// </summary>
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_searchResults.Count == 0)
            {
                System.Windows.MessageBox.Show("没有可导出的搜索结果。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出搜索结果为 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"搜索结果_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _csvExportService.ExportToCsv(_searchResults, dialog.FileName);
                    System.Windows.MessageBox.Show($"导出成功！\n文件路径：{dialog.FileName}", "导出完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 设置搜索中/完成状态
        /// </summary>
        private void SetSearchingState(bool isSearching)
        {
            _isSearching = isSearching;
            BtnSearch.IsEnabled = !isSearching;
            BtnSelectFolder.IsEnabled = !isSearching;
            TxtKeyword.IsEnabled = !isSearching;
            ProgressBar.Visibility = isSearching ? Visibility.Visible : Visibility.Collapsed;
            BtnStop.Visibility = isSearching ? Visibility.Visible : Visibility.Collapsed;
            BtnStop.IsEnabled = isSearching;
            BtnSearch.Content = isSearching ? "搜索中..." : "开始搜索";

            if (isSearching)
            {
                ProgressBar.Value = 0;
                TxtStatus.Text = "正在扫描文件...";
            }
        }
    }
}
