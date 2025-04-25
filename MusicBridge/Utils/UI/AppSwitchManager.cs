using MusicBridge.Controllers;
using MusicBridge.Utils.Window;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MusicBridge.Utils.UI
{
    /// <summary>
    /// 管理应用切换逻辑，实现自动切换、关闭和启动
    /// </summary>
    public class AppSwitchManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;
        private readonly WindowEmbedManager _windowEmbedManager;

        
        private IMusicApp _currentController;

        /// <summary>
        /// 创建应用切换管理器实例
        /// </summary>
        public AppSwitchManager(
            Dispatcher dispatcher, 
            Action<string> updateStatus, 
            WindowEmbedManager windowEmbedManager
            )
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
            _windowEmbedManager = windowEmbedManager;
           
        }

        /// <summary>
        /// 获取当前控制器
        /// </summary>
        public IMusicApp CurrentController => _currentController;

        /// <summary>
        /// 设置当前控制器（不执行切换操作）
        /// </summary>
        public void SetCurrentController(IMusicApp controller)
        {
            _currentController = controller;
        }

        /// <summary>
        /// 切换到新应用 - 自动关闭当前应用并启动新应用
        /// </summary>
        public async Task<bool> SwitchToAppAsync(IMusicApp newController)
        {
            if (newController == null)
            {
                _updateStatus("错误：无效的应用控制器");
                return false;
            }

            try
            {
                // 如果切换到相同的应用控制器
                if (newController == _currentController)
                {
                    // 如果应用已运行且已嵌入，不执行任何操作直接返回
                    if (newController.IsRunning() && _windowEmbedManager.IsWindowEmbedded)
                    {
                        _updateStatus($"{newController.Name} 已在运行中");
                        return true;
                    }
                    // 如果应用未运行，则启动它
                    else if (!newController.IsRunning())
                    {
                        _updateStatus($"启动 {newController.Name}...");
                        return await _windowEmbedManager.LaunchAndEmbedAsync(newController);
                    }
                    // 如果应用正在运行但未嵌入，尝试重新嵌入
                    else
                    {
                        _updateStatus($"重新嵌入 {newController.Name}...");
                        nint hwnd = WinAPI.FindMainWindow(newController.ProcessName);
                        if (hwnd != nint.Zero && WinAPI.IsWindow(hwnd))
                        {
                            return _windowEmbedManager.EmbedExistingWindow(hwnd);
                        }
                        else
                        {
                            _updateStatus($"找不到 {newController.Name} 的窗口，尝试重新启动");
                            return await _windowEmbedManager.LaunchAndEmbedAsync(newController);
                        }
                    }
                }

                // 切换到不同的应用：如果有当前应用在运行，先关闭它
                if (_currentController != null && _windowEmbedManager.IsWindowEmbedded)
                {
                    await CloseCurrentAppAsync();
                }

                // 更新当前控制器
                _currentController = newController;
                _updateStatus($"切换到 {newController.Name}...");

                // 启动并嵌入新应用
                return await _windowEmbedManager.LaunchAndEmbedAsync(newController);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSwitchManager.SwitchToAppAsync] 错误: {ex}");
                _updateStatus($"切换应用时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭当前应用
        /// </summary>
        public async Task<bool> CloseCurrentAppAsync()
        {
            if (_currentController == null)
            {
                _updateStatus("没有应用可关闭");
                return false;
            }

            try
            {
                // 先分离窗口
                _windowEmbedManager.DetachEmbeddedWindow();
                
                // 如果应用正在运行，关闭它
                if (_currentController.IsRunning())
                {
                   await _currentController.CloseAppAsync();
                    _updateStatus($"已关闭 {_currentController.Name}");
                    
                    // 等待进程确实关闭
                    await Task.Delay(500);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppSwitchManager.CloseCurrentAppAsync] 错误: {ex}");
                _updateStatus($"关闭当前应用时出错: {ex.Message}");
                return false;
            }
        }
    }
}