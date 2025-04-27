using MusicBridge.Controllers;
using MusicBridge.Utils.UI;
using MusicBridge.Utils.Window;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicBridge
{
    public partial class MainWindow : Window
    {
        private readonly List<IMusicApp> controllers = new List<IMusicApp>(); // 所有控制器
        private readonly DispatcherTimer statusTimer = new DispatcherTimer(); // 状态刷新定时器

        // 辅助类实例
        private readonly WindowEmbedManager _windowEmbedManager;
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

         
                
            // 初始化应用图标选择器
            _appIconSelector = new AppIconSelector();
            _appIconSelector.RegisterAppIcon(QQMusicIcon);
            _appIconSelector.RegisterAppIcon(NeteaseMusicIcon);
            _appIconSelector.RegisterAppIcon(KugouMusicIcon);
            
            // 初始化应用切换管理器
            _appSwitchManager = new AppSwitchManager(
                Dispatcher,
                _uiStateManager.UpdateStatus,
                _windowEmbedManager
                );

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
            // 提示用户确保已退出账号
            MessageBoxResult result = MessageBox.Show("请确保已退出所有音乐应用的账号再关闭此软件。是否继续关闭？", "关闭提醒", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; // 取消关闭操作
                return;
            }

            // 直接关闭所有音乐应用
            CloseAllMusicApps();
    
            // 尝试恢复（分离）当前嵌入的窗口
            _windowEmbedManager.DetachEmbeddedWindow();
        }
    
        // 退出前关闭所有音乐应用
        private void CloseAllMusicApps()
        {
            // 获取所有正在运行的音乐应用
            List<IMusicApp> runningApps = controllers.Where(c => c.IsRunning()).ToList();
    
            // 如果有运行中的音乐应用，关闭它们
            if (runningApps.Count > 0)
            {
                // 输出日志
                string appNames = string.Join(", ", runningApps.Select(c => c.Name));
                Debug.WriteLine($"正在关闭音乐应用: {appNames}");
                
                // 关闭所有运行中的音乐应用
                foreach (var app in runningApps)
                {
                    app.CloseAppAsync();
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
            // 如果正在加载应用，跳过刷新以避免干扰加载过程中的UI状态
            if (_isLoadingApp)
            {
                Debug.WriteLine("[定时器] 正在加载应用，跳过刷新状态");
                return;
            }

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
        private async void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_windowEmbedManager.IsWindowEmbedded)
            {
                _uiStateManager.UpdateStatus("没有窗口被嵌入。");
                return;
            }

            _windowEmbedManager.DetachEmbeddedWindow();
            _uiStateManager.UpdateStatus("窗口已分离。");
            
            // 刷新状态以更新按钮可用性
            await RefreshMusicAppStatusAsync();
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
            await currentController.SendCommandAsync(targetHwnd, command);
            
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

        // 定义一个新的标志，用于指示是否正在加载应用
        private bool _isLoadingApp = false;
    
        // AppIcon_MouseDown事件 - 处理音乐应用图标点击 - 自动启动和切换
        private async void AppIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int appIndex))
            {
                if (appIndex < 0 || appIndex >= controllers.Count)
                    return;
                
                // 设置加载标志，防止定时器任务重新显示操作区域叠加层
                _isLoadingApp = true;
                
                try
                {
                    // 立即隐藏操作区域叠加层，避免与加载层重叠显示
                    OperationOverlay.Visibility = Visibility.Collapsed;
                    
                    // 更新选中图标状态
                    _appIconSelector.SelectAppIcon(appIndex);
                    
                    // 使用应用切换管理器执行切换
                    var targetController = controllers[appIndex];
                    
                    // 自动切换到选中的应用
                    await _appSwitchManager.SwitchToAppAsync(targetController);
                    
                    // 刷新状态以更新按钮和UI
                    await RefreshMusicAppStatusAsync();
                }
                finally
                {
                    // 无论成功与否，都复位加载标志
                    _isLoadingApp = false;
                }
            }
        }
        
        // CloseAppButton_Click事件 - 处理关闭当前音乐应用按钮点击
        private async void CloseAppButton_Click(object sender, RoutedEventArgs e)
        {
            // 提示用户确保已退出账号
            MessageBoxResult result = MessageBox.Show($"请确保已退出 {_appSwitchManager.CurrentController?.Name ?? "当前音乐应用"} 的账号再关闭。是否继续关闭？", "关闭提醒", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                return; // 取消关闭操作
            }

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
                    // --- 新增：重新嵌入成功后，设置 AppHost 的控制器 ---
                    AppHostControl.CurrentController = currentController;
                    // --- 新增结束 ---

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
        /// 确保嵌入窗口在弹出系统虚拟键盘后能获得输入焦点
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
                    await Task.Delay(500); // 等待 osk.exe 启动

                    // --- 修改：根据控制器类型决定是否强制设置焦点 ---
                    var currentController = _appSwitchManager.CurrentController; // 获取当前控制器
                    if (!(currentController is Controllers.NeteaseMusicController))
                    {
                        // 如果不是网易云，则尝试设置焦点 (恢复之前的行为)
                        Debug.WriteLine("[SystemKeyboardButton_Click] 非网易云应用，尝试设置焦点回嵌入窗口...");
                        await EnsureEmbeddedWindowInputFocusForKeyboard(embeddedHwnd);
                    }
                    else
                    {
                        Debug.WriteLine("[SystemKeyboardButton_Click] 网易云应用，跳过强制设置焦点。");
                    }
                    // --- 修改结束 ---
                }
                else // 键盘已运行，需要关闭
                {
                    SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(232, 244, 255)); // #E8F4FF
                    SystemKeyboardButton.ToolTip = "打开系统虚拟键盘";

                    // --- 新增：尝试将焦点设置回主窗口，以获取关闭 osk.exe 的权限 ---
                    try
                    {
                        IntPtr mainWindowHandle = new WindowInteropHelper(this).Handle;
                        if (mainWindowHandle != IntPtr.Zero)
                        {
                            Debug.WriteLine("[SystemKeyboardButton_Click] 尝试将焦点设置回主窗口以关闭键盘...");
                            WinAPI.SetForegroundWindow(mainWindowHandle); // 尝试将主窗口带到前台
                            WinAPI.SetFocus(mainWindowHandle);           // 尝试设置键盘焦点到主窗口
                            await Task.Delay(100); // 短暂延迟，等待焦点切换生效
                        }
                        else
                        {
                             Debug.WriteLine("[SystemKeyboardButton_Click] 获取主窗口句柄失败，无法设置焦点。");
                        }
                    }
                    catch (Exception focusEx)
                    {
                        Debug.WriteLine($"[SystemKeyboardButton_Click] 设置主窗口焦点时出错: {focusEx.Message}");
                        // 即使设置焦点失败，也继续尝试关闭键盘
                    }
                    // --- 新增结束 ---

                    // 调用关闭方法
                    bool closed = SystemKeyboardHelper.Close();

                    if (closed)
                    {
                        _uiStateManager.UpdateStatus("系统虚拟键盘已关闭");
                    }
                    else
                    {
                         _uiStateManager.UpdateStatus("关闭系统虚拟键盘失败 (可能需要管理员权限)");
                         // 即使关闭失败，也更新按钮状态
                         SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(232, 244, 255)); // #E8F4FF
                         SystemKeyboardButton.ToolTip = "打开系统虚拟键盘";
                    }
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