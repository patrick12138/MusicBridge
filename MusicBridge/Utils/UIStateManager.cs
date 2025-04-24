using MusicBridge.Controllers;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 管理应用的UI状态
    /// </summary>
    public class UIStateManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly TextBlock _statusTextBlock;
        private readonly TextBlock _currentSongTextBlock;
        private readonly Button _launchAndEmbedButton; // 可能为 null，因为已移除此按钮
        private readonly Button _detachButton;
        private Button _reEmbedButton; // 移除readonly修饰符，允许后续修改
        private readonly Button _closeAppButton;
        private readonly Button _playPauseButton;
        private readonly Button _nextButton;
        private readonly Button _previousButton;
        private readonly Button _volumeUpButton;
        private readonly Button _volumeDownButton;
        private readonly Button _muteButton;
        private readonly FrameworkElement _operationOverlay;
        private readonly AppHost _appHost;
        
        // 记录应用状态，用于重新嵌入功能
        private bool _isControllerRunning = false;
        private bool _isDetached = false;
        
        /// <summary>
        /// 创建 UIStateManager 实例
        /// </summary>
        public UIStateManager(
            Dispatcher dispatcher,
            TextBlock statusTextBlock,
            TextBlock currentSongTextBlock,
            Button launchAndEmbedButton, // 可能为 null
            Button detachButton,
            Button closeAppButton,
            Button playPauseButton,
            Button nextButton,
            Button previousButton,
            Button volumeUpButton,
            Button volumeDownButton,
            Button muteButton,
            FrameworkElement operationOverlay,
            AppHost appHost)
        {
            _dispatcher = dispatcher;
            _statusTextBlock = statusTextBlock;
            _currentSongTextBlock = currentSongTextBlock;
            _launchAndEmbedButton = launchAndEmbedButton;
            _detachButton = detachButton;
            _closeAppButton = closeAppButton;
            _playPauseButton = playPauseButton;
            _nextButton = nextButton;
            _previousButton = previousButton;
            _volumeUpButton = volumeUpButton;
            _volumeDownButton = volumeDownButton;
            _muteButton = muteButton;
            _operationOverlay = operationOverlay;
            _appHost = appHost;
            
            // 查找重新嵌入按钮（通过 MainWindow 中的 FindName 查找）
            if (_detachButton != null && _detachButton.Parent is UIElement parent)
            {
                var window = Window.GetWindow(parent);
                if (window != null)
                {
                    _reEmbedButton = window.FindName("ReEmbedButton") as Button;
                }
            }
        }
        
        /// <summary>
        /// 设置重新嵌入按钮引用
        /// </summary>
        public void SetReEmbedButton(Button reEmbedButton)
        {
            _reEmbedButton = reEmbedButton;
        }
        
        /// <summary>
        /// 更新状态消息
        /// </summary>
        public void UpdateStatus(string status)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.InvokeAsync(() => UpdateStatus(status));
                return;
            }
            
            if (_statusTextBlock != null) 
            {
                _statusTextBlock.Text = $"状态: {status}";
            }
            
            Debug.WriteLine($"[状态更新] {status}");
        }
        
        /// <summary>
        /// 根据应用状态更新UI
        /// </summary>
        public async Task UpdateUIState(
            IMusicAppController controller, 
            bool isRunning, 
            bool isEmbedded, 
            string currentSong)
        {
            // 保存状态用于重新嵌入功能
            _isControllerRunning = isRunning;
            _isDetached = isRunning && !isEmbedded;
            
            await _dispatcher.InvokeAsync(() =>
            {
                // 更新歌曲信息
                _currentSongTextBlock.Text = $"歌曲: {currentSong}";
                
                // 更新嵌入区域的显示状态
                if (isEmbedded)
                {
                    // 已嵌入：隐藏操作区叠加层，显示嵌入窗口
                    _operationOverlay.Visibility = Visibility.Collapsed;
                    _appHost.Visibility = Visibility.Visible;
                }
                else
                {
                    // 未嵌入：显示操作区叠加层，隐藏嵌入窗口
                    _operationOverlay.Visibility = Visibility.Visible;
                    _appHost.Visibility = Visibility.Collapsed;
                }
                
                // 设置交互按钮状态
                if (_launchAndEmbedButton != null)
                {
                    _launchAndEmbedButton.IsEnabled = controller != null && !isEmbedded && controller.ExecutablePath != null;
                }
                
                _detachButton.IsEnabled = isEmbedded;
                
                // 设置重新嵌入按钮状态 - 应用运行但未嵌入时启用
                if (_reEmbedButton != null)
                {
                    _reEmbedButton.IsEnabled = _isDetached;
                }
                
                // 设置媒体控制按钮状态
                SetMediaButtonsEnabled(isRunning);
                
                // 设置关闭按钮状态 - 应用运行时启用
                _closeAppButton.IsEnabled = isRunning;
            });
        }
        
        /// <summary>
        /// 当没有选择控制器时更新UI状态
        /// </summary>
        public async Task UpdateUIStateForNoController()
        {
            // 重置状态
            _isControllerRunning = false;
            _isDetached = false;
            
            await _dispatcher.InvokeAsync(() =>
            {
                _currentSongTextBlock.Text = "歌曲: N/A";
                UpdateStatus("请选择播放器");
                
                // 禁用所有交互按钮
                if (_launchAndEmbedButton != null)
                {
                    _launchAndEmbedButton.IsEnabled = false;
                }
                
                _detachButton.IsEnabled = false;
                
                if (_reEmbedButton != null)
                {
                    _reEmbedButton.IsEnabled = false;
                }
                
                _closeAppButton.IsEnabled = false;
                SetMediaButtonsEnabled(false);
                
                // 显示操作区叠加层
                _operationOverlay.Visibility = Visibility.Visible;
                _appHost.Visibility = Visibility.Collapsed;
            });
        }
        
        /// <summary>
        /// 当发生错误时更新UI状态
        /// </summary>
        public async Task UpdateUIStateForError()
        {
            await _dispatcher.InvokeAsync(() =>
            {
                // 出错时显示操作区叠加层，隐藏嵌入窗口
                _operationOverlay.Visibility = Visibility.Visible;
                _appHost.Visibility = Visibility.Collapsed;
                
                // 禁用交互按钮
                if (_launchAndEmbedButton != null)
                {
                    _launchAndEmbedButton.IsEnabled = false;
                }
                
                _detachButton.IsEnabled = false;
                
                if (_reEmbedButton != null)
                {
                    _reEmbedButton.IsEnabled = false;
                }
                
                _closeAppButton.IsEnabled = false;
                SetMediaButtonsEnabled(false);
            });
        }
        
        /// <summary>
        /// 检查是否应用已运行但处于分离状态（可重新嵌入）
        /// </summary>
        public bool CanReEmbed()
        {
            return _isControllerRunning && _isDetached;
        }
        
        /// <summary>
        /// 设置媒体控制按钮的启用状态
        /// </summary>
        private void SetMediaButtonsEnabled(bool isEnabled)
        {
            if (!_dispatcher.CheckAccess()) 
            { 
                _dispatcher.InvokeAsync(() => SetMediaButtonsEnabled(isEnabled)); 
                return; 
            }

            _playPauseButton.IsEnabled = isEnabled;
            _nextButton.IsEnabled = isEnabled;
            _previousButton.IsEnabled = isEnabled;
            _volumeUpButton.IsEnabled = isEnabled;
            _volumeDownButton.IsEnabled = isEnabled;
            _muteButton.IsEnabled = isEnabled;
        }
    }
}