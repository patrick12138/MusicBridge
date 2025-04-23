using MusicBridge.Controllers;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicBridge
{
    public partial class MainWindow : Window
    {
        private IMusicAppController? currentController; // 当前选中的控制器
        private readonly List<IMusicAppController> controllers = new List<IMusicAppController>(); // 所有控制器
        private readonly DispatcherTimer statusTimer = new DispatcherTimer(); // 状态刷新定时器
        private IntPtr _embeddedWindowHandle = IntPtr.Zero; // 当前嵌入的窗口句柄

        // 构造函数
        public MainWindow()
        {
            InitializeComponent(); // 初始化 XAML 组件

            // --- 初始化控制器列表和下拉框 ---
            try
            {
                controllers.Add(new QQMusicController());
                controllers.Add(new NeteaseMusicController());
                controllers.Add(new KugouMusicController());

                foreach (var controller in controllers)
                {
                    MusicAppComboBox.Items.Add(new ComboBoxItem { Content = controller.Name });
                }
                if (MusicAppComboBox.Items.Count > 0)
                {
                    MusicAppComboBox.SelectedIndex = 0; // 默认选中第一个
                }
                else
                {
                    UpdateStatus("错误：未找到任何音乐播放器控制器。");
                }

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
            AppHostControl?.RestoreHostedWindow();
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
            if (_embeddedWindowHandle != IntPtr.Zero && !WinAPI.IsWindow(_embeddedWindowHandle))
            {
                Debug.WriteLine($"[定时器] 检测到嵌入窗口句柄 {_embeddedWindowHandle} 已失效，自动分离。");
                await Dispatcher.InvokeAsync(DetachEmbeddedWindow); // 确保在 UI 线程分离
            }
        }

        // 下拉框选择变化
        private async void MusicAppComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MusicAppComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
            {
                string appName = selectedItem.Content.ToString()!;
                var selectedController = controllers.FirstOrDefault(c => c.Name == appName);

                if (selectedController != null)
                {
                    // 如果切换控制器时有窗口嵌入，先分离
                    if (_embeddedWindowHandle != IntPtr.Zero)
                    {
                        DetachEmbeddedWindow();
                    }

                    currentController = selectedController;
                    UpdateStatus($"已切换到: {currentController.Name}");

                    // 确保新控制器的路径已查找 (如果需要)
                    if (currentController.ExecutablePath == null)
                    {
                        await currentController.FindExecutablePathAsync();
                    }
                    await RefreshMusicAppStatusAsync(); // 刷新新控制器的状态
                }
            }
        }

        // --- 按钮事件处理 ---

        // “启动并嵌入”按钮点击
        private async void LaunchAndEmbedButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) { UpdateStatus("请先选择播放器"); return; }

            // 如果已嵌入，不执行任何操作（或提示先分离）
            if (_embeddedWindowHandle != IntPtr.Zero)
            {
                UpdateStatus($"{currentController.Name} 已嵌入，请先分离。");
                return;
            }

            UpdateStatus($"正在启动 {currentController.Name} ...");
            SetInteractionButtonsEnabled(false); // 禁用交互按钮

            // 1. 启动进程 (如果未运行)
            if (!currentController.IsRunning())
            {
                await currentController.LaunchAsync();
                await Task.Delay(5000); // 等待较长时间让窗口创建
            }
            else
            {
                // 如果已运行，确保窗口不是最小化
                IntPtr existingHwnd = WinAPI.FindMainWindow(currentController.ProcessName);
                if (existingHwnd != IntPtr.Zero && WinAPI.IsIconic(existingHwnd))
                {
                    WinAPI.ShowWindow(existingHwnd, WinAPI.SW_RESTORE);
                    await Task.Delay(500); // 等待窗口恢复
                }
                UpdateStatus($"{currentController.Name} 已在运行，尝试查找窗口...");
            }


            // 2. 查找主窗口句柄 (尝试多次)
            IntPtr targetHwnd = IntPtr.Zero;
            for (int i = 0; i < 5; i++) // 尝试 5 次
            {
                targetHwnd = WinAPI.FindMainWindow(currentController.ProcessName);
                if (targetHwnd != IntPtr.Zero) break; // 找到即退出循环
                Debug.WriteLine($"第 {i + 1} 次查找 {currentController.Name} 窗口失败，等待 1 秒后重试...");
                await Task.Delay(1000);
            }

            // 3. 尝试嵌入
            if (targetHwnd != IntPtr.Zero)
            {
                UpdateStatus($"找到窗口 {targetHwnd}，正在嵌入...");
                bool success = false;
                // AppHost 操作需要回到 UI 线程
                await Dispatcher.InvokeAsync(() =>
                {
                    success = AppHostControl.EmbedWindow(targetHwnd);
                });

                if (success)
                {
                    _embeddedWindowHandle = targetHwnd; // 记录嵌入的句柄
                    UpdateStatus($"{currentController.Name} 已嵌入。");
                }
                else
                {
                    _embeddedWindowHandle = IntPtr.Zero;
                    UpdateStatus($"嵌入 {currentController.Name} 失败。");
                    MessageBox.Show($"嵌入 {currentController.Name} 失败。\n可能原因：\n- 权限不足 (尝试以管理员运行本程序)\n- 目标应用窗口结构不兼容\n- 目标应用有反嵌入机制", "嵌入失败", MessageBoxButton.OK);
                }
            }
            else
            {
                UpdateStatus($"未能找到 {currentController.Name} 的主窗口，无法嵌入。");
            }

            // 最终刷新状态并恢复按钮
            await RefreshMusicAppStatusAsync();
        }

        // “分离窗口”按钮点击
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            DetachEmbeddedWindow();
            UpdateStatus("窗口已分离。");
            // 刷新状态以更新按钮可用性
            RefreshMusicAppStatusAsync();
        }

        // 封装的分离逻辑 (可在 UI 线程调用)
        private void DetachEmbeddedWindow()
        {
            AppHostControl?.RestoreHostedWindow(); // AppHost 负责恢复窗口
            _embeddedWindowHandle = IntPtr.Zero; // 清除记录
            Debug.WriteLine("嵌入窗口已分离。");
        }

        // “关闭嵌入的应用”按钮点击
        private async void CloseEmbeddedAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) return;
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            var result = MessageBox.Show($"确定要关闭嵌入的 {currentController.Name} 吗？", "确认关闭", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            UpdateStatus($"正在关闭 {currentController.Name}...");
            SetInteractionButtonsEnabled(false); // 禁用交互按钮

            // 先分离，再关闭
            DetachEmbeddedWindow();
            await Task.Delay(200); // 短暂等待分离完成

            await currentController.CloseAppAsync(); // 调用控制器的关闭逻辑
            await Task.Delay(1000); // 等待进程关闭

            // 最终刷新状态
            await RefreshMusicAppStatusAsync();
        }


        // --- 媒体控制按钮 ---
        // 封装发送命令逻辑
        private async Task SendMediaCommandAsync(MediaCommand command)
        {
            if (currentController == null) return;

            // 确定目标窗口：优先使用嵌入的窗口句柄，否则查找主窗口
            IntPtr targetHwnd = _embeddedWindowHandle != IntPtr.Zero && WinAPI.IsWindow(_embeddedWindowHandle)
                                ? _embeddedWindowHandle
                                : WinAPI.FindMainWindow(currentController.ProcessName);

            if (targetHwnd == IntPtr.Zero || !WinAPI.IsWindow(targetHwnd))
            {
                UpdateStatus($"无法找到 {currentController.Name} 的窗口来发送命令 {command}。");
                // 检查应用是否还在运行
                if (!currentController.IsRunning() && _embeddedWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine($"[SendMediaCommand] 应用已停止，但仍记录为嵌入，执行分离。");
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                }
                return;
            }

            try
            {
                // 直接调用控制器的 SendCommandAsync，传递目标 HWND
                await currentController.SendCommandAsync(targetHwnd, command);
                // 命令发送后，稍作等待并刷新状态（特别是歌名）
                await Task.Delay(150);
                await RefreshMusicAppStatusAsync();
            }
            catch (Exception ex)
            {
                UpdateStatus($"发送命令 {command} 时出错: {ex.Message}");
                Debug.WriteLine($"[SendMediaCommandAsync] Error: {ex}");
            }
        }

        // 按钮事件绑定到 SendMediaCommandAsync
        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PlayPause);
        private async void NextButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.NextTrack);
        private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.PreviousTrack);
        private async void VolumeUpButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeUp);
        private async void VolumeDownButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeDown);
        private async void MuteButton_Click(object sender, RoutedEventArgs e) => await SendMediaCommandAsync(MediaCommand.VolumeMute);


        // --- 状态刷新和 UI 更新 ---

        // 刷新音乐应用状态 (异步)
        private async Task RefreshMusicAppStatusAsync()
        {
            if (currentController == null)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentSongTextBlock.Text = "歌曲: N/A";
                    UpdateStatus("请选择播放器");
                    SetInteractionButtonsEnabled(false);
                    SetMediaButtonsEnabled(false);
                });
                return;
            }

            bool isRunning = false;
            bool isEmbedded = _embeddedWindowHandle != IntPtr.Zero && WinAPI.IsWindow(_embeddedWindowHandle);
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
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    isEmbedded = false; // 更新本地状态
                    status += "已停止 (自动分离)";
                }
                else if (isRunning)
                {
                    status += isEmbedded ? "已嵌入" : "运行中 (未嵌入)";
                    // 获取歌曲信息，传递当前有效的窗口句柄
                    IntPtr songTargetHwnd = isEmbedded ? _embeddedWindowHandle : WinAPI.FindMainWindow(currentController.ProcessName);
                    song = currentController.GetCurrentSong(songTargetHwnd);
                }
                else // 未运行
                {
                    status += "未运行";
                    // 确保如果未运行，则嵌入句柄也清空
                    if (_embeddedWindowHandle != IntPtr.Zero)
                    {
                        await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    }
                }

                // 在 UI 线程更新界面
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentSongTextBlock.Text = $"歌曲: {song}";
                    UpdateStatus(status);
                    SetInteractionButtonsEnabled(currentController != null); // 基本交互按钮（启动、分离、关闭）根据控制器和状态启用
                    SetMediaButtonsEnabled(isRunning); // 媒体控制按钮根据是否运行启用
                });
            }
            catch (Exception ex)
            {
                UpdateStatus($"刷新 {currentController.Name} 状态时出错: {ex.Message}");
                Debug.WriteLine($"[RefreshMusicAppStatusAsync] Error: {ex}");
                // 出错时保守处理，禁用大部分按钮
                await Dispatcher.InvokeAsync(() =>
                {
                    SetInteractionButtonsEnabled(false);
                    SetMediaButtonsEnabled(false);
                    MusicAppComboBox.IsEnabled = true; // 允许切换
                });
            }
        }

        // 更新状态栏文本 (确保在UI线程)
        private void UpdateStatus(string status)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => UpdateStatus(status));
                return;
            }
            if (CurrentStatusTextBlock != null) CurrentStatusTextBlock.Text = $"状态: {status}";
            Debug.WriteLine($"[状态更新] {status}");
        }

        // 设置交互按钮（启动/嵌入、分离、关闭）的启用状态
        private void SetInteractionButtonsEnabled(bool controllerSelected)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => SetInteractionButtonsEnabled(controllerSelected)); return; }

            bool canLaunchEmbed = controllerSelected && _embeddedWindowHandle == IntPtr.Zero && currentController?.ExecutablePath != null;
            LaunchAndEmbedButton.IsEnabled = canLaunchEmbed;

            bool canDetach = controllerSelected && _embeddedWindowHandle != IntPtr.Zero;
            DetachButton.IsEnabled = canDetach;

            bool canCloseEmbedded = controllerSelected && _embeddedWindowHandle != IntPtr.Zero;
            CloseEmbeddedAppButton.IsEnabled = canCloseEmbedded;

            // 下拉框通常总是可用
            MusicAppComboBox.IsEnabled = true;
        }

        // 设置媒体控制按钮的启用状态
        private void SetMediaButtonsEnabled(bool isRunning)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => SetMediaButtonsEnabled(isRunning)); return; }

            PlayPauseButton.IsEnabled = isRunning;
            NextButton.IsEnabled = isRunning;
            PreviousButton.IsEnabled = isRunning;
            VolumeUpButton.IsEnabled = isRunning;
            VolumeDownButton.IsEnabled = isRunning;
            MuteButton.IsEnabled = isRunning;
        }
    }
}