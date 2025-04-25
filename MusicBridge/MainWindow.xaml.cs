using MusicBridge.Controllers;
using MusicBridge.Utils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicBridge
{
    public partial class MainWindow : Window
    {
        private readonly List<IMusicAppController> controllers = new List<IMusicAppController>(); // 所有控制器
        private readonly DispatcherTimer statusTimer = new DispatcherTimer(); // 状态刷新定时器

        // 辅助类实例
        private readonly WindowEmbedManager _windowEmbedManager;
        private readonly MediaPlayerHandler _mediaPlayerHandler;
        private readonly UIStateManager _uiStateManager;
        private readonly AppIconSelector _appIconSelector;
        private readonly AppSwitchManager _appSwitchManager; // 新增：应用切换管理器

        // 构造函数
        public MainWindow()
        {
            InitializeComponent(); // 初始化 XAML 组件

            // 初始化辅助类
            _uiStateManager = new UIStateManager(
                Dispatcher,
                CurrentStatusTextBlock,
                CurrentSongTextBlock,
                null, // 移除了启动并嵌入按钮
                DetachButton,
                CloseAppButton,
                PlayPauseButton,
                NextButton,
                PreviousButton,
                VolumeUpButton,
                VolumeDownButton,
                MuteButton,
                OperationOverlay,
                AppHostControl);
                
            // 设置重新嵌入按钮引用
            _uiStateManager.SetReEmbedButton(ReEmbedButton);

            _windowEmbedManager = new WindowEmbedManager(
                Dispatcher,
                _uiStateManager.UpdateStatus,
                AppHostControl);
                
            // 重要：设置互相引用关系，使加载提示功能正常工作
            _windowEmbedManager.SetUIStateManager(_uiStateManager);

            _mediaPlayerHandler = new MediaPlayerHandler(
                Dispatcher,
                _uiStateManager.UpdateStatus);
                
            // 初始化应用图标选择器
            _appIconSelector = new AppIconSelector();
            _appIconSelector.RegisterAppIcon(QQMusicIcon);
            _appIconSelector.RegisterAppIcon(NeteaseMusicIcon);
            _appIconSelector.RegisterAppIcon(KugouMusicIcon);
            
            // 初始化应用切换管理器
            _appSwitchManager = new AppSwitchManager(
                Dispatcher,
                _uiStateManager.UpdateStatus,
                _windowEmbedManager,
                _mediaPlayerHandler);

            // --- 初始化控制器列表 ---
            try
            {
                controllers.Add(new QQMusicController());
                controllers.Add(new NeteaseMusicController());
                controllers.Add(new KugouMusicController());

                // 配置状态刷新定时器
                statusTimer.Interval = TimeSpan.FromSeconds(2); // 缩短刷新间隔以更快响应
                statusTimer.Tick += StatusTimer_Tick;
                statusTimer.Start();

                // 监听 AppHost 大小变化
                AppHostControl.SizeChanged += AppHostControl_SizeChanged;
                // 窗口关闭时尝试恢复嵌入的窗口
                this.Closing += MainWindow_Closing;
                // 窗口加载后执行初始操作
                Loaded += MainWindow_Loaded;

                // 初始化时隐藏 AppHost，显示操作区叠加层
                AppHostControl.Visibility = Visibility.Collapsed;
                OperationOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化应用时发生严重错误: {ex.Message}", "初始化失败", MessageBoxButton.OK);
            }
        }

        // 窗口加载完成
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 异步查找所有路径
            await FindAllControllerPathsAsync();
            // 初始刷新状态和控件
            await RefreshMusicAppStatusAsync();
        }

        // 异步查找所有控制器路径
        private async Task FindAllControllerPathsAsync()
        {
            Debug.WriteLine("开始查找所有控制器路径...");
            List<Task> tasks = new List<Task>();
            foreach (var controller in controllers)
            {
                tasks.Add(controller.FindExecutablePathAsync());
            }
            await Task.WhenAll(tasks);
            Debug.WriteLine("所有控制器路径查找完成。");
            
            // 路径查找完成后，重新刷新一次状态
            await RefreshMusicAppStatusAsync();
        }

        // 窗口关闭中
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 直接关闭所有音乐应用
            CloseAllMusicApps();

            // 尝试恢复（分离）当前嵌入的窗口
            _windowEmbedManager.DetachEmbeddedWindow();
        }

        // 退出前关闭所有音乐应用
        private void CloseAllMusicApps()
        {
            // 获取所有正在运行的音乐应用
            List<IMusicAppController> runningApps = controllers.Where(c => c.IsRunning()).ToList();

            // 如果有运行中的音乐应用，关闭它们
            if (runningApps.Count > 0)
            {
                // 输出日志
                string appNames = string.Join(", ", runningApps.Select(c => c.Name));
                Debug.WriteLine($"正在关闭音乐应用: {appNames}");
                
                // 关闭所有运行中的音乐应用
                foreach (var app in runningApps)
                {
                    CloseAppProcess(app);
                }
                
                // 等待应用完全关闭
                WaitForAppClosing(runningApps);
            }
        }

        // 关闭指定应用进程
        private void CloseAppProcess(IMusicAppController controller)
        {
            try
            {
                Debug.WriteLine($"正在关闭应用: {controller.Name}");
                Process[] processes = Process.GetProcessesByName(controller.ProcessName);
                foreach (var process in processes)
                {
                    try
                    {
                        // 尝试正常关闭进程
                        if (!process.HasExited)
                        {
                            // 先尝试发送关闭窗口消息
                            IntPtr hwnd = WinAPI.FindMainWindow(controller.ProcessName);
                            if (hwnd != IntPtr.Zero)
                            {
                                WinAPI.PostMessage(hwnd, WinAPI.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                            else
                            {
                                // 如果找不到窗口，则直接结束进程
                                process.Kill();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"关闭 {controller.Name} 进程失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭 {controller.Name} 时发生错误: {ex}");
            }
        }

        // 等待应用完全关闭
        private void WaitForAppClosing(List<IMusicAppController> apps)
        {
            // 设置最大等待时间为3秒
            int maxWaitTime = 3000; // 3秒
            int startTime = Environment.TickCount;
            
            while (Environment.TickCount - startTime < maxWaitTime)
            {
                // 检查是否所有应用都已关闭
                bool allClosed = true;
                foreach (var app in apps)
                {
                    if (app.IsRunning())
                    {
                        allClosed = false;
                        break;
                    }
                }
                
                if (allClosed)
                {
                    Debug.WriteLine("所有音乐应用已成功关闭");
                    return;
                }
                
                // 短暂等待
                System.Threading.Thread.Sleep(100);
            }
            
            Debug.WriteLine("等待音乐应用关闭超时，继续退出");
            
            // 超时后，强制关闭所有仍在运行的应用
            foreach (var app in apps)
            {
                if (app.IsRunning())
                {
                    try
                    {
                        Debug.WriteLine($"强制关闭应用: {app.Name}");
                        Process[] processes = Process.GetProcessesByName(app.ProcessName);
                        foreach (var process in processes)
                        {
                            if (!process.HasExited)
                            {
                                process.Kill(true); // 强制立即终止进程
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"强制关闭 {app.Name} 失败: {ex.Message}");
                    }
                }
            }
        }

        // AppHost 控件大小改变
        private void AppHostControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 通知 AppHost 调整内部嵌入窗口的大小
            AppHostControl?.ResizeEmbeddedWindow();
        }

        // 定时器触发
        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            // 定期刷新状态
            await RefreshMusicAppStatusAsync();

            // 额外检查：如果记录了嵌入句柄，但该窗口已失效，则自动分离
            if (_windowEmbedManager.EmbeddedWindowHandle != IntPtr.Zero && 
                !WinAPI.IsWindow(_windowEmbedManager.EmbeddedWindowHandle))
            {
                Debug.WriteLine($"[定时器] 检测到嵌入窗口句柄 {_windowEmbedManager.EmbeddedWindowHandle} 已失效，自动分离。");
                await Dispatcher.InvokeAsync(_windowEmbedManager.DetachEmbeddedWindow); // 确保在 UI 线程分离
            }
        }

        // "分离窗口"按钮点击
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_windowEmbedManager.IsWindowEmbedded) 
            { 
                _uiStateManager.UpdateStatus("没有窗口被嵌入。"); 
                return; 
            }

            _windowEmbedManager.DetachEmbeddedWindow();
            _uiStateManager.UpdateStatus("窗口已分离。");
            
            // 刷新状态以更新按钮可用性
            RefreshMusicAppStatusAsync();
        }

        // 封装发送命令逻辑
        private async Task SendMediaCommandAsync(MediaCommand command)
        {
            var currentController = _appSwitchManager.CurrentController;
            if (currentController == null) return;

            // 确定目标窗口：优先使用嵌入的窗口句柄，否则查找主窗口
            IntPtr targetHwnd = _windowEmbedManager.IsWindowEmbedded
                                ? _windowEmbedManager.EmbeddedWindowHandle
                                : WinAPI.FindMainWindow(currentController.ProcessName);

            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                _uiStateManager.UpdateStatus($"无法找到 {currentController.Name} 的窗口来发送命令 {command}。");
                // 检查应用是否还在运行
                if (!currentController.IsRunning() && _windowEmbedManager.IsWindowEmbedded)
                {
                    Debug.WriteLine($"[SendMediaCommand] 应用已停止，但仍记录为嵌入，执行分离。");
                    await Dispatcher.InvokeAsync(_windowEmbedManager.DetachEmbeddedWindow);
                }
                return;
            }

            // 发送命令
            await _mediaPlayerHandler.SendMediaCommandAsync(currentController, targetHwnd, command);
            
            // 命令发送后刷新状态
            await RefreshMusicAppStatusAsync();
        }

        // 按钮事件绑定到 SendMediaCommandAsync
        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PlayPause);
        private async void NextButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.NextTrack);
        private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PreviousTrack);
        private async void VolumeUpButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeUp);
        private async void VolumeDownButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeDown);
        private async void MuteButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeMute);

        // 刷新音乐应用状态 (异步)
        private async Task RefreshMusicAppStatusAsync()
        {
            var currentController = _appSwitchManager.CurrentController;
            if (currentController == null)
            {
                await _uiStateManager.UpdateUIStateForNoController();
                return;
            }

            bool isRunning = false;
            bool isEmbedded = _windowEmbedManager.IsWindowEmbedded;
            string song = "无";
            string status = $"[{currentController.Name}] ";

            try
            {
                // 检查进程是否仍在运行
                isRunning = currentController.IsRunning();

                // 状态同步：如果记录为嵌入但进程已停止，则执行分离
                if (isEmbedded && !isRunning)
                {
                    Debug.WriteLine($"[刷新状态] 检测到嵌入窗口的进程已停止，执行分离。");
                    await Dispatcher.InvokeAsync(_windowEmbedManager.DetachEmbeddedWindow);
                    isEmbedded = false; // 更新本地状态
                    status += "已停止 (自动分离)";
                }
                // 如果记录为嵌入，但窗口句柄失效，也执行分离
                else if (isEmbedded && !WinAPI.IsWindow(_windowEmbedManager.EmbeddedWindowHandle))
                {
                    Debug.WriteLine($"[刷新状态] 检测到嵌入窗口句柄 {_windowEmbedManager.EmbeddedWindowHandle} 已失效，执行分离。");
                    await Dispatcher.InvokeAsync(_windowEmbedManager.DetachEmbeddedWindow);
                    isEmbedded = false;
                    status += "窗口失效 (自动分离)";
                }
                else if (isRunning)
                {
                    status += isEmbedded ? "已嵌入" : "运行中 (未嵌入)";
                    // 获取歌曲信息，传递当前有效的窗口句柄
                    IntPtr songTargetHwnd = isEmbedded ? _windowEmbedManager.EmbeddedWindowHandle : WinAPI.FindMainWindow(currentController.ProcessName);
                    // 增加判断，防止在窗口句柄无效时调用 GetCurrentSong 导致错误
                    if (WinAPI.IsWindow(songTargetHwnd))
                    {
                        song = currentController.GetCurrentSong(songTargetHwnd);
                    } else {
                        song = "窗口无效";
                    }
                }
                else // 未运行
                {
                    status += "未运行";
                    // 确保如果未运行，则嵌入句柄也清空，并隐藏 AppHost
                    if (_windowEmbedManager.IsWindowEmbedded)
                    {
                        await Dispatcher.InvokeAsync(_windowEmbedManager.DetachEmbeddedWindow);
                    }
                }

                // 更新UI状态
                _uiStateManager.UpdateStatus(status);
                await _uiStateManager.UpdateUIState(currentController, isRunning, isEmbedded, song);
            }
            catch (Exception ex)
            {
                _uiStateManager.UpdateStatus($"刷新 {currentController.Name} 状态时出错: {ex.Message}");
                Debug.WriteLine($"[RefreshMusicAppStatusAsync] Error: {ex}");
                
                // 出错时更新UI状态
                await _uiStateManager.UpdateUIStateForError();
            }
        }

        // AppIcon_MouseDown事件 - 处理音乐应用图标点击 - 自动启动和切换
        private async void AppIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int appIndex))
            {
                if (appIndex < 0 || appIndex >= controllers.Count)
                    return;
                
                // 更新选中图标状态
                _appIconSelector.SelectAppIcon(appIndex);
                
                // 使用应用切换管理器执行切换
                var targetController = controllers[appIndex];
                
                // 自动切换到选中的应用
                await _appSwitchManager.SwitchToAppAsync(targetController);
                
                // 刷新状态以更新按钮和UI
                await RefreshMusicAppStatusAsync();
            }
        }
        
        // CloseAppButton_Click事件 - 处理关闭当前音乐应用按钮点击
        private async void CloseAppButton_Click(object sender, RoutedEventArgs e)
        {
            // 使用应用切换管理器关闭当前应用
            await _appSwitchManager.CloseCurrentAppAsync();
            
            // 清除当前图标选择
            _appIconSelector.ClearSelection();
            
            // 重置当前控制器
            _appSwitchManager.SetCurrentController(null);
            
            // 更新UI状态
            await _uiStateManager.UpdateUIStateForNoController();
        }
        
        // ReEmbedButton_Click事件 - 处理重新嵌入窗口按钮点击
        private async void ReEmbedButton_Click(object sender, RoutedEventArgs e)
        {
            var currentController = _appSwitchManager.CurrentController;
            if (currentController == null)
            {
                _uiStateManager.UpdateStatus("无应用可重新嵌入");
                return;
            }
            
            // 检查应用是否运行
            if (!currentController.IsRunning())
            {
                _uiStateManager.UpdateStatus($"{currentController.Name} 未运行，无法重新嵌入");
                return;
            }
            
            try
            {
                // 查找应用窗口
                IntPtr hwnd = WinAPI.FindMainWindow(currentController.ProcessName);
                if (hwnd == IntPtr.Zero || !WinAPI.IsWindow(hwnd))
                {
                    _uiStateManager.UpdateStatus($"找不到 {currentController.Name} 的窗口，无法重新嵌入");
                    return;
                }
                
                // 尝试重新嵌入窗口
                if (_windowEmbedManager.EmbedExistingWindow(hwnd))
                {
                    _uiStateManager.UpdateStatus($"{currentController.Name} 已重新嵌入");
                    
                    // 更新UI状态
                    await RefreshMusicAppStatusAsync();
                }
                else
                {
                    _uiStateManager.UpdateStatus($"重新嵌入 {currentController.Name} 失败");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReEmbedButton_Click] 错误: {ex}");
                _uiStateManager.UpdateStatus($"重新嵌入时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 确保嵌入窗口在弹出系统虚拟键盘后能获得输入焦点（多次尝试，提升输入成功率）
        /// </summary>
        /// <param name="embeddedHwnd">嵌入窗口句柄</param>
        /// <param name="retryCount">尝试次数</param>
        /// <param name="delayMs">每次尝试间隔（毫秒）</param>
        private async Task EnsureEmbeddedWindowInputFocusForKeyboard(IntPtr embeddedHwnd, int retryCount = 5, int delayMs = 120)
        {
            // 多次尝试将焦点切回嵌入窗口
            for (int i = 0; i < retryCount; i++)
            {
                if (embeddedHwnd == IntPtr.Zero || !WinAPI.IsWindow(embeddedHwnd))
                    return;
                WinAPI.SetForegroundWindow(embeddedHwnd);
                WinAPI.SetFocus(embeddedHwnd);
                await Task.Delay(delayMs);
            }
        }

        // SystemKeyboardButton_Click事件 - 处理系统虚拟键盘按钮点击
        private async void SystemKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 检查是否有嵌入窗口
                if (!_windowEmbedManager.IsWindowEmbedded || _windowEmbedManager.EmbeddedWindowHandle == IntPtr.Zero)
                {
                    _uiStateManager.UpdateStatus("错误：没有有效的嵌入窗口。请先启动音乐应用。");
                    return;
                }
                // 2. 获取当前键盘状态
                bool isKeyboardRunning = SystemKeyboardHelper.IsRunning();
                IntPtr embeddedHwnd = _windowEmbedManager.EmbeddedWindowHandle;
                // 3. 根据当前状态执行操作
                if (!isKeyboardRunning) // 键盘未运行，需要启动
                {
                    SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(179, 215, 255)); // #B3D7FF
                    SystemKeyboardButton.ToolTip = "关闭系统虚拟键盘";
                    WinAPI.RECT rect = new WinAPI.RECT();
                    WinAPI.GetWindowRect(embeddedHwnd, ref rect);
                    bool opened = SystemKeyboardHelper.Open();
                    if (!opened)
                    {
                        _uiStateManager.UpdateStatus("无法启动系统虚拟键盘");
                        return;
                    }
                    _uiStateManager.UpdateStatus("系统虚拟键盘已启动");
                    await Task.Delay(500);
                    // 确保焦点回到嵌入窗口
                    // 新增：多次确保焦点回到嵌入窗口，提升输入成功率
                    await EnsureEmbeddedWindowInputFocusForKeyboard(embeddedHwnd);
                }
                else // 键盘已运行，需要关闭
                {
                    SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(232, 244, 255)); // #E8F4FF
                    SystemKeyboardButton.ToolTip = "打开系统虚拟键盘";
                    WinAPI.SetForegroundWindow(embeddedHwnd);
                    await Task.Delay(50);
                    SystemKeyboardHelper.Close();
                    _uiStateManager.UpdateStatus("系统虚拟键盘已关闭");
                    await Task.Delay(200);
                    WinAPI.SetForegroundWindow(embeddedHwnd);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardButton_Click] 错误: {ex}");
                _uiStateManager.UpdateStatus($"操作系统虚拟键盘时出错: {ex.Message}");
                SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(232, 244, 255)); // #E8F4FF
                SystemKeyboardButton.ToolTip = "打开系统虚拟键盘";
            }
        }
    }
}