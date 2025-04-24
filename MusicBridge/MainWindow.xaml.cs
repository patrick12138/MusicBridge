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
        private IMusicAppController? currentController; // 当前选中的控制器
        private readonly List<IMusicAppController> controllers = new List<IMusicAppController>(); // 所有控制器
        private readonly DispatcherTimer statusTimer = new DispatcherTimer(); // 状态刷新定时器
        private int _selectedAppIndex = -1; // 当前选中的应用索引
        private int _selectedAppTypeIndex = -1; // 当前选中的应用类型索引

        // 辅助类实例
        private readonly WindowEmbedManager _windowEmbedManager;
        private readonly MediaPlayerHandler _mediaPlayerHandler;
        private readonly SearchManager _searchManager;
        private readonly UIStateManager _uiStateManager;

        // 构造函数
        public MainWindow()
        {
            InitializeComponent(); // 初始化 XAML 组件

            // 初始化辅助类
            _uiStateManager = new UIStateManager(
                Dispatcher,
                CurrentStatusTextBlock,
                CurrentSongTextBlock,
                LaunchAndEmbedButton,
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
            // 路径查找完成后，重新刷新一次状态，可能启动按钮现在可用了
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

        // "启动并嵌入"按钮点击
        private async void LaunchAndEmbedButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) 
            { 
                _uiStateManager.UpdateStatus("请先选择播放器"); 
                return; 
            }

            SetInteractionButtonsEnabled(false, false, false); // 禁用交互按钮

            // 执行启动和嵌入操作
            bool success = await _windowEmbedManager.LaunchAndEmbedAsync(currentController);
            
            // 完成后刷新状态
            await RefreshMusicAppStatusAsync();
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

        // 设置交互按钮状态
        private void SetInteractionButtonsEnabled(bool controllerSelected, bool isEmbedded, bool isRunning)
        {
            if (!Dispatcher.CheckAccess()) 
            { 
                Dispatcher.InvokeAsync(() => SetInteractionButtonsEnabled(controllerSelected, isEmbedded, isRunning)); 
                return; 
            }

            // 启动/嵌入按钮：需要选中控制器，当前未嵌入，且控制器路径有效
            bool canLaunchEmbed = controllerSelected && !isEmbedded && currentController?.ExecutablePath != null;
            LaunchAndEmbedButton.IsEnabled = canLaunchEmbed;

            // 分离按钮：需要选中控制器且当前已嵌入
            bool canDetach = controllerSelected && isEmbedded;
            DetachButton.IsEnabled = canDetach;

            // 关闭应用按钮状态
            CloseAppButton.IsEnabled = isRunning;
        }

        // 已移除的方法，保留为空方法以避免XAML绑定错误
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 此方法保留为空，以防XAML中仍有事件绑定引用它
        }
        
        // 已移除的方法，保留为空方法以避免XAML绑定错误
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // 直接调用OpenKeyboardButton_Click，实现相同功能
            OpenKeyboardButton_Click(sender, e);
        }

        // 打开虚拟键盘按钮点击事件
        private void OpenKeyboardButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果没有嵌入的窗口，提示用户
            if (!_windowEmbedManager.IsWindowEmbedded)
            {
                _uiStateManager.UpdateStatus("错误：没有有效的嵌入窗口。请先启动并嵌入音乐应用。");
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

        // AppIcon_MouseDown事件 - 处理音乐应用图标点击
        private void AppIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && int.TryParse(element.Tag?.ToString(), out int appIndex))
            {
                // 保存当前选择的应用类型索引
                _selectedAppTypeIndex = appIndex;
                
                // 更新当前控制器
                currentController = controllers[appIndex];
                
                // 更新UI状态
                _uiStateManager.UpdateStatus($"已选择 {GetAppNameByIndex(appIndex)}，请点击启动按钮");
                
                // 刷新状态以更新按钮
                RefreshMusicAppStatusAsync();
            }
        }
        
        // 根据索引获取应用名称
        private string GetAppNameByIndex(int index)
        {
            switch (index)
            {
                case 0: return "QQ音乐";
                case 1: return "网易云音乐";
                case 2: return "酷狗音乐";
                default: return "未知应用";
            }
        }
        
        // CloseAppButton_Click事件 - 处理关闭当前音乐应用按钮点击
        private void CloseAppButton_Click(object sender, RoutedEventArgs e)
        {
            // 关闭当前嵌入的应用
            if (_windowEmbedManager.IsWindowEmbedded)
            {
                _mediaPlayerHandler.CloseEmbeddedApp(_windowEmbedManager.EmbeddedWindowHandle);
                _windowEmbedManager.DetachEmbeddedWindow();
            }
            
            // 重置当前控制器
            currentController = null;
            
            // 更新UI状态
            _uiStateManager.UpdateUIStateForNoController();
            
            // 更新状态
            _uiStateManager.UpdateStatus("已关闭音乐应用");
        }
    }
}