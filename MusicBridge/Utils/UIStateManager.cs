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
    /// Manages the UI state for the application
    /// </summary>
    public class UIStateManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly TextBlock _statusTextBlock;
        private readonly TextBlock _currentSongTextBlock;
        private readonly Button _launchAndEmbedButton;
        private readonly Button _detachButton;
        private readonly Button _closeAppButton;
        private readonly Button _playPauseButton;
        private readonly Button _nextButton;
        private readonly Button _previousButton;
        private readonly Button _volumeUpButton;
        private readonly Button _volumeDownButton;
        private readonly Button _muteButton;
        private readonly FrameworkElement _operationOverlay;
        private readonly AppHost _appHost;
        
        /// <summary>
        /// Creates a new instance of UIStateManager
        /// </summary>
        public UIStateManager(
            Dispatcher dispatcher,
            TextBlock statusTextBlock,
            TextBlock currentSongTextBlock,
            Button launchAndEmbedButton,
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
        }
        
        /// <summary>
        /// Updates the status message
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
        /// Updates the UI state based on application state
        /// </summary>
        public async Task UpdateUIState(
            IMusicAppController controller, 
            bool isRunning, 
            bool isEmbedded, 
            string currentSong)
        {
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
                _launchAndEmbedButton.IsEnabled = controller != null && !isEmbedded && controller.ExecutablePath != null;
                _detachButton.IsEnabled = isEmbedded;
                
                // 设置媒体控制按钮状态
                SetMediaButtonsEnabled(isRunning);
                
                // 设置关闭按钮状态 - 应用运行时启用
                _closeAppButton.IsEnabled = isRunning;
            });
        }
        
        /// <summary>
        /// Updates the UI state when no controller is selected
        /// </summary>
        public async Task UpdateUIStateForNoController()
        {
            await _dispatcher.InvokeAsync(() =>
            {
                _currentSongTextBlock.Text = "歌曲: N/A";
                UpdateStatus("请选择播放器");
                
                // 禁用所有交互按钮
                _launchAndEmbedButton.IsEnabled = false;
                _detachButton.IsEnabled = false;
                _closeAppButton.IsEnabled = false;
                SetMediaButtonsEnabled(false);
                
                // 显示操作区叠加层
                _operationOverlay.Visibility = Visibility.Visible;
                _appHost.Visibility = Visibility.Collapsed;
            });
        }
        
        /// <summary>
        /// Updates the UI state when an error occurs
        /// </summary>
        public async Task UpdateUIStateForError()
        {
            await _dispatcher.InvokeAsync(() =>
            {
                // 出错时显示操作区叠加层，隐藏嵌入窗口
                _operationOverlay.Visibility = Visibility.Visible;
                _appHost.Visibility = Visibility.Collapsed;
                
                // 禁用交互按钮
                _launchAndEmbedButton.IsEnabled = false;
                _detachButton.IsEnabled = false;
                _closeAppButton.IsEnabled = false;
                SetMediaButtonsEnabled(false);
            });
        }
        
        /// <summary>
        /// Sets the enabled state of media control buttons
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