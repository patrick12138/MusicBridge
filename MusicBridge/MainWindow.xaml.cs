using MusicBridge.Controllers;
using System.Diagnostics;
using System.IO;
using System.Text; // 引入 StringBuilder
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

                // ***新增：初始化时隐藏 AppHostControl***
                AppHostControl.Visibility = Visibility.Collapsed;
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

            // 额外检查：如果记录了嵌入句柄，但该窗口已失效或父窗口不再是 AppHost，则自动分离
            if (_embeddedWindowHandle != IntPtr.Zero)
            {
                bool isValid = WinAPI.IsWindow(_embeddedWindowHandle);
                // 可选：更严格的检查，需要 AppHostControl 暴露其 Handle
                // bool isParentCorrect = isValid && AppHostControl.Handle != IntPtr.Zero && WinAPI.GetParent(_embeddedWindowHandle) == AppHostControl.Handle;
                // if (!isValid || !isParentCorrect)

                if (!isValid) // 简化检查：仅检查窗口是否有效
                {
                    Debug.WriteLine($"[定时器] 检测到嵌入窗口句柄 {_embeddedWindowHandle} 已失效，自动分离。");
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow); // 确保在 UI 线程分离
                }
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
            SetInteractionButtonsEnabled(false, false, false); // 禁用交互按钮

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
                    // ***新增：嵌入前确保 AppHost 可见***
                    AppHostControl.Visibility = Visibility.Visible;
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
                    // ***新增：嵌入失败时隐藏 AppHost***
                    AppHostControl.Visibility = Visibility.Collapsed;
                    UpdateStatus($"嵌入 {currentController.Name} 失败。");
                    MessageBox.Show($"嵌入 {currentController.Name} 失败。\n可能原因：\n- 权限不足 (尝试以管理员运行本程序)\n- 目标应用窗口结构不兼容\n- 目标应用有反嵌入机制", "嵌入失败", MessageBoxButton.OK);
                }
            }
            else
            {
                // ***新增：找不到窗口时确保 AppHost 隐藏***
                AppHostControl.Visibility = Visibility.Collapsed;
                UpdateStatus($"未能找到 {currentController.Name} 的主窗口，无法嵌入。");
            }

            // 最终刷新状态并恢复按钮
            await RefreshMusicAppStatusAsync();
        }

        // “分离窗口”按钮点击
        private void DetachButton_Click(object sender, RoutedEventArgs e)
        {
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            DetachEmbeddedWindow(); // 调用包含隐藏逻辑的分离方法
            UpdateStatus("窗口已分离。");
            // 刷新状态以更新按钮可用性
            RefreshMusicAppStatusAsync(); // 注意：这里是异步方法，但事件处理程序是同步的。如果 Refresh 需要很长时间，考虑改为 async void 或 Task.Run
        }

        // 封装的分离逻辑 (可在 UI 线程调用)
        private void DetachEmbeddedWindow()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(DetachEmbeddedWindow); // 同步调用确保执行完毕
                return;
            }
            AppHostControl?.RestoreHostedWindow(); // AppHost 负责恢复窗口
            _embeddedWindowHandle = IntPtr.Zero; // 清除记录
            // ***新增：分离后隐藏 AppHostControl***
            AppHostControl.Visibility = Visibility.Collapsed;
            Debug.WriteLine("嵌入窗口已分离，AppHost 已隐藏。");
        }

        // “关闭嵌入的应用”按钮点击
        private async void CloseEmbeddedAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentController == null) return;
            if (_embeddedWindowHandle == IntPtr.Zero) { UpdateStatus("没有窗口被嵌入。"); return; }

            var result = MessageBox.Show($"确定要关闭嵌入的 {currentController.Name} 吗？", "确认关闭", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            UpdateStatus($"正在关闭 {currentController.Name}...");
            SetInteractionButtonsEnabled(false, false, false); // 禁用交互按钮

            // 先分离，再关闭
            DetachEmbeddedWindow();
            await Task.Delay(200); // 短暂等待分离完成

            await currentController.CloseAppAsync(); // 调用控制器的关闭逻辑
            await Task.Delay(1000); // 等待进程关闭

            // 最终刷新状态
            await RefreshMusicAppStatusAsync();
        }

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

        // 刷新音乐应用状态 (异步)
        private async Task RefreshMusicAppStatusAsync()
        {
            if (currentController == null)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentSongTextBlock.Text = "歌曲: N/A";
                    UpdateStatus("请选择播放器");
                    SetInteractionButtonsEnabled(false, false, false);
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
                    // ***修改：调用包含隐藏逻辑的分离方法***
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    isEmbedded = false; // 更新本地状态
                    status += "已停止 (自动分离)";
                }
                // ***新增：如果记录为嵌入，但窗口句柄失效，也执行分离***
                else if (isEmbedded && !WinAPI.IsWindow(_embeddedWindowHandle))
                {
                    Debug.WriteLine($"[刷新状态] 检测到嵌入窗口句柄 {_embeddedWindowHandle} 已失效，执行分离。");
                    await Dispatcher.InvokeAsync(DetachEmbeddedWindow);
                    isEmbedded = false;
                    status += "窗口失效 (自动分离)";
                }
                else if (isRunning)
                {
                    status += isEmbedded ? "已嵌入" : "运行中 (未嵌入)";
                    // 获取歌曲信息，传递当前有效的窗口句柄
                    IntPtr songTargetHwnd = isEmbedded ? _embeddedWindowHandle : WinAPI.FindMainWindow(currentController.ProcessName);
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
                    // ***修改：交互按钮的启用逻辑现在也依赖 isEmbedded 状态***
                    SetInteractionButtonsEnabled(currentController != null, isEmbedded, isRunning);
                    SetMediaButtonsEnabled(isRunning); // 媒体控制按钮根据是否运行启用

                    // ***新增：根据是否嵌入决定 AppHost 的可见性（双重保险）***
                    // AppHostControl.Visibility = isEmbedded ? Visibility.Visible : Visibility.Collapsed;
                    // 注意：上面的显式设置可能与 DetachEmbeddedWindow 中的隐藏冲突或冗余，
                    // 依赖 DetachEmbeddedWindow 和 EmbedWindow 中的设置通常足够。
                    // 如果仍有问题，可以取消注释上面这行。
                });
            }
            catch (Exception ex)
            {
                UpdateStatus($"刷新 {currentController.Name} 状态时出错: {ex.Message}");
                Debug.WriteLine($"[RefreshMusicAppStatusAsync] Error: {ex}");
                // 出错时保守处理，禁用大部分按钮
                await Dispatcher.InvokeAsync(() =>
                {
                    // 出错时也尝试隐藏 AppHost
                    AppHostControl.Visibility = Visibility.Collapsed;
                    SetInteractionButtonsEnabled(false, false, false); // 禁用所有交互
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
        // ***修改：增加 isEmbedded 和 isRunning 参数***
        private void SetInteractionButtonsEnabled(bool controllerSelected, bool isEmbedded, bool isRunning)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => SetInteractionButtonsEnabled(controllerSelected, isEmbedded, isRunning)); return; }

            // 启动/嵌入按钮：需要选中控制器，当前未嵌入，且控制器路径有效
            bool canLaunchEmbed = controllerSelected && !isEmbedded && currentController?.ExecutablePath != null;
            LaunchAndEmbedButton.IsEnabled = canLaunchEmbed;

            // 分离按钮：需要选中控制器且当前已嵌入
            bool canDetach = controllerSelected && isEmbedded;
            DetachButton.IsEnabled = canDetach;

            // 关闭嵌入应用按钮：需要选中控制器且当前已嵌入
            bool canCloseEmbedded = controllerSelected && isEmbedded;
            CloseEmbeddedAppButton.IsEnabled = canCloseEmbedded;

            // 下拉框通常总是可用，但在嵌入时可以考虑禁用，防止误操作切换
            MusicAppComboBox.IsEnabled = !isEmbedded; // 嵌入时禁用切换
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

        // “搜索”按钮点击事件 (修改为发送到焦点控件)
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_embeddedWindowHandle == IntPtr.Zero || !WinAPI.IsWindow(_embeddedWindowHandle))
            {
                UpdateStatus("错误：没有有效的嵌入窗口。");
                return;
            }

            string searchText = SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                UpdateStatus("请输入搜索内容。");
                return;
            }

            // 1. 获取当前拥有键盘焦点的控件句柄
            IntPtr focusedHandle = WinAPI.GetFocus();

            if (focusedHandle == IntPtr.Zero)
            {
                UpdateStatus("错误：无法获取当前焦点控件。");
                return;
            }

            // 2. 验证焦点控件是否属于嵌入窗口
            if (!IsDescendant(_embeddedWindowHandle, focusedHandle))
            {
                UpdateStatus("错误：当前焦点不在嵌入的应用窗口内。请先点击嵌入应用的搜索框。");
                MessageBox.Show("请先用鼠标点击嵌入应用（如QQ音乐）内的搜索框，使其获得焦点，然后再点击本程序的“搜索”按钮。", "操作提示", MessageBoxButton.OK);
                return;
            }

            UpdateStatus($"焦点控件 {focusedHandle} 属于嵌入窗口，正在发送文本 '{searchText}'...");

            // 3. 使用 WM_SETTEXT 发送文本到焦点控件
            // 注意：WM_SETTEXT 可能不适用于所有控件类型，特别是自定义绘制的控件。
            // 如果 WM_SETTEXT 无效，备选方案是模拟键盘输入 (SendInput)，但这更复杂。
            IntPtr setResult = WinAPI.SendMessage(focusedHandle, WinAPI.WM_SETTEXT, IntPtr.Zero, searchText);
            Debug.WriteLine($"WM_SETTEXT 发送结果: {setResult}");
            // 短暂等待文本设置生效
            System.Threading.Thread.Sleep(100);

            // 4. 模拟按下回车键 (Enter) 到焦点控件
            WinAPI.SendMessage(focusedHandle, WinAPI.WM_KEYDOWN, (IntPtr)WinAPI.VK_RETURN, IntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            WinAPI.SendMessage(focusedHandle, WinAPI.WM_KEYUP, (IntPtr)WinAPI.VK_RETURN, IntPtr.Zero);

            UpdateStatus($"已向焦点控件发送 '{searchText}' 并模拟回车。");
        }

        // 辅助方法：检查 handleToCheck 是否是 parentHandle 的子孙控件
        private bool IsDescendant(IntPtr parentHandle, IntPtr handleToCheck)
        {
            if (parentHandle == IntPtr.Zero || handleToCheck == IntPtr.Zero)
                return false;
            // 如果句柄相同，也算（虽然不太可能，焦点通常在子控件上）
            if (parentHandle == handleToCheck) 
                return true; 

            IntPtr currentParent = handleToCheck;
            while (currentParent != IntPtr.Zero)
            {
                currentParent = WinAPI.GetParent(currentParent);
                if (currentParent == parentHandle)
                {
                    return true; // 找到了祖先是嵌入窗口
                }
            }
            return false; // 循环结束没找到
        }
    }
}