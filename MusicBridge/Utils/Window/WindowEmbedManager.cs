using MusicBridge.Controllers;
using MusicBridge.Utils.UI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MusicBridge.Utils.Window
{
    /// <summary>
    /// 管理窗口嵌入操作
    /// </summary>
    public class WindowEmbedManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;
        private readonly AppHost _appHost;
        private UIStateManager _uiStateManager; // 新增：UI状态管理器引用

        private nint _embeddedWindowHandle = nint.Zero;
        

        public bool IsWindowEmbedded => _embeddedWindowHandle != nint.Zero && WinAPI.IsWindow(_embeddedWindowHandle);

        public nint EmbeddedWindowHandle => _embeddedWindowHandle;
        

        public WindowEmbedManager(Dispatcher dispatcher, Action<string> updateStatus, AppHost appHost)
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
            _appHost = appHost;
        }
        
        /// <summary>
        /// 设置UI状态管理器引用
        /// </summary>
        public void SetUIStateManager(UIStateManager uiStateManager)
        {
            _uiStateManager = uiStateManager;
        }
        
        public async Task<bool> LaunchAndEmbedAsync(IMusicApp controller)
        {
            if (controller == null) 
            {
                _updateStatus("请先选择播放器");
                return false;
            }

            // 如果已嵌入，不执行任何操作
            if (IsWindowEmbedded)
            {
                _updateStatus($"{controller.Name} 已嵌入，请先分离。");
                return false;
            }

            // 显示加载提示
            _uiStateManager?.ShowLoadingOverlay(controller.Name);
            _updateStatus($"正在启动 {controller.Name} ...");

            try
            {
                // 1. 启动进程 (如果未运行)
                if (!controller.IsRunning())
                {
                    await controller.LaunchAsync();
                    
                    // 等待应用启动和创建窗口
                    // 显示正在等待创建窗口的消息
                    _updateStatus($"等待 {controller.Name} 创建窗口，请稍候...");
                    await Task.Delay(5000); // 等待较长时间让窗口创建
                }
                else
                {
                    // 如果已运行，确保窗口不是最小化
                    nint existingHwnd = WinAPI.FindMainWindow(controller.ProcessName);
                    if (existingHwnd != nint.Zero && WinAPI.IsIconic(existingHwnd))
                    {
                        WinAPI.ShowWindow(existingHwnd, WinAPI.SW_RESTORE);
                        await Task.Delay(500); // 等待窗口恢复
                    }
                    _updateStatus($"{controller.Name} 已在运行，尝试查找窗口...");
                }

                // 2. 查找主窗口句柄 (尝试多次)
                nint targetHwnd = nint.Zero;
                for (int i = 0; i < 7; i++) // 增加尝试次数，从5次到7次
                {
                    targetHwnd = WinAPI.FindMainWindow(controller.ProcessName);
                    if (targetHwnd != nint.Zero) break; // 找到即退出循环
                    
                    // 更新等待提示，告知用户还在尝试
                    _updateStatus($"第 {i + 1} 次查找 {controller.Name} 窗口，请稍候...");
                    Debug.WriteLine($"第 {i + 1} 次查找 {controller.Name} 窗口失败，等待 1 秒后重试...");
                    await Task.Delay(1000);
                }

                // 3. 尝试嵌入
                if (targetHwnd != nint.Zero)
                {
                    _updateStatus($"找到窗口 {targetHwnd}，正在嵌入...");
                    bool success = false;
                    
                    // AppHost 操作需要回到 UI 线程
                    await _dispatcher.InvokeAsync(() =>
                    {
                        success = _appHost.EmbedWindow(targetHwnd);
                    });

                    if (success)
                    {
                        _embeddedWindowHandle = targetHwnd; // 记录嵌入的句柄
                        _appHost.CurrentController = controller; // --- 新增：设置 AppHost 的当前控制器 ---
                        _updateStatus($"{controller.Name} 已嵌入。");
                        // 隐藏加载提示
                        _uiStateManager?.HideLoadingOverlay();
                        return true;
                    }
                    else
                    {
                        _embeddedWindowHandle = nint.Zero;
                        _updateStatus($"嵌入 {controller.Name} 失败。");
                        // 隐藏加载提示
                        _uiStateManager?.HideLoadingOverlay();
                        MessageBox.Show($"嵌入 {controller.Name} 失败。\n可能原因：\n- 权限不足 (尝试以管理员运行本程序)\n- 目标应用窗口结构不兼容\n- 目标应用有反嵌入机制", "嵌入失败", MessageBoxButton.OK);
                        return false;
                    }
                }
                else
                {
                    _updateStatus($"未能找到 {controller.Name} 的主窗口，无法嵌入。");
                    // 隐藏加载提示
                    _uiStateManager?.HideLoadingOverlay();
                    MessageBox.Show($"未能找到 {controller.Name} 的主窗口。\n请确认应用是否已正常启动，可尝试手动启动后再使用重新嵌入功能。", "找不到窗口", MessageBoxButton.OK);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LaunchAndEmbedAsync] 错误: {ex}");
                _updateStatus($"启动并嵌入应用时出错: {ex.Message}");
                // 发生异常时也要隐藏加载提示
                _uiStateManager?.HideLoadingOverlay();
                return false;
            }
        }
        
        /// <summary>
        /// Detaches the currently embedded window
        /// </summary>
        public void DetachEmbeddedWindow()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(DetachEmbeddedWindow);
                return;
            }
            
            if (_appHost != null)
            {
                _appHost.RestoreHostedWindow(); // AppHost 负责恢复窗口 (内部会清除 CurrentController)
            }
            _embeddedWindowHandle = nint.Zero; // 清除记录
            Debug.WriteLine("嵌入窗口已分离");
        }
        
        /// <summary>
        /// 嵌入现有的窗口（用于重新嵌入功能）
        /// </summary>
        /// <param name="hwnd">要嵌入的窗口句柄</param>
        /// <returns>嵌入是否成功</returns>
        public bool EmbedExistingWindow(nint hwnd)
        {
            if (!_dispatcher.CheckAccess())
            {
                return _dispatcher.Invoke(() => EmbedExistingWindow(hwnd));
            }
            
            // 如果已经有嵌入的窗口，先分离
            if (IsWindowEmbedded)
            {
                DetachEmbeddedWindow();
            }
            
            // 确认窗口仍然有效
            if (hwnd == nint.Zero || !WinAPI.IsWindow(hwnd))
            {
                _updateStatus("错误：无效的窗口句柄，无法嵌入。");
                return false;
            }

            // 确保窗口不是最小化状态
            if (WinAPI.IsIconic(hwnd))
            {
                WinAPI.ShowWindow(hwnd, WinAPI.SW_RESTORE);
                Thread.Sleep(500); // 等待窗口恢复
            }
            
            // 尝试嵌入
            bool success = _appHost.EmbedWindow(hwnd);
            
            if (success)
            {
                _embeddedWindowHandle = hwnd; // 记录嵌入的句柄
                // --- 注意：这里无法直接设置 AppHost.CurrentController，因为它不知道是哪个 Controller ---
                // --- 需要在调用 EmbedExistingWindow 的地方 (MainWindow.xaml.cs) 设置 ---
                _updateStatus("窗口已重新嵌入。");
                return true;
            }
            else
            {
                _embeddedWindowHandle = nint.Zero;
                _updateStatus("重新嵌入窗口失败。");
                return false;
            }
        }
    }
}