using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 管理音乐应用的搜索功能
    /// </summary>
    public class SearchManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;

        /// <summary>
        /// 创建 SearchManager 实例
        /// </summary>
        public SearchManager(Dispatcher dispatcher, Action<string> updateStatus)
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
        }

        /// <summary>
        /// 直接对嵌入的窗口执行搜索操作
        /// </summary>
        public async Task<bool> PerformDirectSearch(IntPtr targetHwnd, string searchText)
        {
            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                _updateStatus("错误：无效的窗口句柄，无法执行搜索");
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _updateStatus("错误：搜索文本为空");
                return false;
            }

            try
            {
                await Task.Run(() =>
                {
                    // 激活目标窗口
                    WinAPI.SetForegroundWindow(targetHwnd);
                    // 等待窗口获得焦点
                    System.Threading.Thread.Sleep(300);

                    // 清除可能的剪贴板内容
                    try { Clipboard.Clear(); } catch { /* 忽略剪贴板错误 */ }

                    // 使用剪贴板设置搜索文本（更可靠的输入方式）
                    Clipboard.SetText(searchText);
                    System.Threading.Thread.Sleep(100);

                    // 模拟按下 Ctrl+F 打开搜索框
                    WinAPI.SendKeys(targetHwnd, "^f");
                    System.Threading.Thread.Sleep(500);

                    // 模拟 Ctrl+V 粘贴搜索内容
                    WinAPI.SendKeys(targetHwnd, "^v");
                    System.Threading.Thread.Sleep(300);

                    // 模拟按下回车执行搜索
                    WinAPI.SendKeys(targetHwnd, "{ENTER}");
                });

                _updateStatus($"已执行搜索：{searchText}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchManager.PerformDirectSearch] 错误: {ex}");
                _updateStatus($"执行搜索时出错: {ex.Message}");
                return false;
            }
        }
    }
}