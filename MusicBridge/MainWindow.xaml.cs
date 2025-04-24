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
        private readonly SearchManager _searchManager;
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

            _mediaPlayerHandler = new MediaPlayerHandler(
                Dispatcher,
                _uiStateManager.UpdateStatus);

            _searchManager = new SearchManager(
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
            // 尝试恢复（分离）当前嵌入的窗口
            _windowEmbedManager.DetachEmbeddedWindow();
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

        // 打开虚拟键盘按钮点击事件
        private void OpenKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果没有嵌入的窗口，提示用户
            if (!_windowEmbedManager.IsWindowEmbedded)
            {
                _uiStateManager.UpdateStatus("错误：没有有效的嵌入窗口。请先启动音乐应用。");
                return;
            }

            // 直接初始化虚拟键盘，无需通过TextBox
            VirtualKeyboardControl.DirectSearchMode = true;
            VirtualKeyboardControl.Initialize(null);
            
            // 订阅虚拟键盘的搜索完成事件
            VirtualKeyboardControl.SearchCompleted -= VirtualKeyboard_DirectSearch; // 避免重复订阅
            VirtualKeyboardControl.SearchCompleted += VirtualKeyboard_DirectSearch;
            
            // 显示虚拟键盘弹出窗口
            KeyboardPopup.PlacementTarget = OpenKeyboardButton;
            KeyboardPopup.IsOpen = true;
        }

        // 直接搜索事件处理
        private void VirtualKeyboard_DirectSearch(object sender, string searchText)
        {
            // 关闭键盘弹出窗口
            KeyboardPopup.IsOpen = false;
            
            // 如果返回的搜索文本为空（取消操作），不执行任何操作
            if (string.IsNullOrWhiteSpace(searchText))
                return;
            
            // 执行直接搜索
            DirectPerformSearch(searchText);
        }

        // 执行直接搜索
        private async void DirectPerformSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return;
                
            // 检查是否有嵌入的窗口并执行直接搜索
            await _searchManager.PerformDirectSearch(_windowEmbedManager.EmbeddedWindowHandle, searchText);
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
        
        // SystemKeyboardButton_Click事件 - 处理系统虚拟键盘按钮点击
        private void SystemKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换系统虚拟键盘的显示状态
                bool isOpen = SystemKeyboardHelper.ToggleSystemKeyboard();
                
                // 更新按钮状态和提示文本
                if (isOpen)
                {
                    // 键盘已打开，将按钮背景色改为更加明显的选中状态
                    SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(179, 215, 255)); // #B3D7FF
                    SystemKeyboardButton.ToolTip = "关闭系统虚拟键盘";
                    _uiStateManager.UpdateStatus("系统虚拟键盘已启动");
                }
                else
                {
                    // 键盘已关闭，恢复按钮默认背景色
                    SystemKeyboardButton.Background = new SolidColorBrush(Color.FromRgb(232, 244, 255)); // #E8F4FF
                    SystemKeyboardButton.ToolTip = "打开系统虚拟键盘";
                    _uiStateManager.UpdateStatus("系统虚拟键盘已关闭");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SystemKeyboardButton_Click] 错误: {ex}");
                _uiStateManager.UpdateStatus($"操作系统虚拟键盘时出错: {ex.Message}");
            }
        }
    }
}