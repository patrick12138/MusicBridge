using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MusicBridge
{
    // 用于承载外部 Win32 窗口的 HwndHost 实现
    public class AppHost : HwndHost
    {
        private const int HOST_ID = 0x00000001;         // 宿主窗口的任意 ID
        private IntPtr _hostHwnd = IntPtr.Zero;         // 我们创建的宿主 Win32 窗口句柄
        private IntPtr _hostedAppHwnd = IntPtr.Zero;    // 被嵌入的应用窗口句柄
        private IntPtr _originalParent = IntPtr.Zero;   // 记录原始父窗口 (虽然通常恢复到桌面)
        private long _originalStyles = 0;               // 记录原始窗口样式

        // --- 新增：存储当前嵌入的控制器实例 ---
        public Controllers.IMusicAppController CurrentController { get; set; }
        // --- 新增结束 ---

        // 公开属性，获取当前嵌入的应用窗口句柄
        public IntPtr HostedAppWindowHandle => _hostedAppHwnd;

        // 构造函数
        public AppHost()
        {
            Debug.WriteLine("[AppHost] 创建实例");
        }

        // 核心方法：创建宿主 Win32 窗口 (在 WPF 布局中)
        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostHwnd = WinAPI.CreateWindowEx(
                0,                      // 无扩展样式
                "Static",               // 使用简单可靠的标准 "Static" 类
                "",                     // 无标题
                WinAPI.WS_CHILD | WinAPI.WS_VISIBLE | WinAPI.WS_CLIPCHILDREN | WinAPI.WS_CLIPSIBLINGS, // 关键样式：子窗口、可见、裁剪子/兄弟
                0, 0,                   // X, Y (相对于 hwndParent)
                (int)this.ActualWidth, (int)this.ActualHeight, // 初始宽高
                hwndParent.Handle,      // 父窗口句柄 (来自 WPF)
                (IntPtr)HOST_ID,        // 窗口 ID
                IntPtr.Zero,            // 当前进程实例句柄 (传 Zero)
                IntPtr.Zero);           // 无附加参数

            if (_hostHwnd == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AppHost] 创建宿主窗口失败，错误码: {error}");
                throw new Exception($"创建宿主窗口失败，错误码: {error}");
            }

            Debug.WriteLine($"[AppHost] 宿主窗口已创建，HWND: {_hostHwnd}");
            // 返回宿主窗口的句柄，WPF 会管理它
            return new HandleRef(this, _hostHwnd);
        }

        // 核心方法：销毁宿主 Win32 窗口 (当 AppHost 被移除或窗口关闭时)
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            Debug.WriteLine($"[AppHost] 开始销毁宿主窗口，HWND: {hwnd.Handle}");
            // 在销毁宿主前，必须恢复（分离）被嵌入的窗口！
            RestoreHostedWindow();
            // 销毁我们自己创建的宿主窗口
            if (WinAPI.DestroyWindow(hwnd.Handle))
            {
                Debug.WriteLine($"[AppHost] 宿主窗口 HWND: {hwnd.Handle} 已销毁");
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AppHost] 销毁宿主窗口 HWND: {hwnd.Handle} 失败，错误码: {error}");
            }
            _hostHwnd = IntPtr.Zero;
        }

        // 尝试将指定的外部应用窗口嵌入到宿主窗口中
        public bool EmbedWindow(IntPtr appWindowHandle)
        {
            if (_hostHwnd == IntPtr.Zero || appWindowHandle == IntPtr.Zero || !WinAPI.IsWindow(appWindowHandle))
            {
                Debug.WriteLine($"[AppHost Embed] 失败：宿主窗口({_hostHwnd}) 或目标应用窗口({appWindowHandle}) 无效。");
                return false;
            }

            // 如果已经有窗口嵌入，先恢复它
            if (_hostedAppHwnd != IntPtr.Zero && _hostedAppHwnd != appWindowHandle)
            {
                Debug.WriteLine($"[AppHost Embed] 检测到已嵌入窗口 {_hostedAppHwnd}，先执行恢复...");
                RestoreHostedWindow();
            }

            _hostedAppHwnd = appWindowHandle; // 记录新的目标句柄

            Debug.WriteLine($"[AppHost Embed] 开始嵌入窗口 HWND: {_hostedAppHwnd} 到宿主 HWND: {_hostHwnd}");

            // 1. 记录原始样式和父窗口 (用于恢复)
            _originalStyles = WinAPI.GetWindowLongPtr(_hostedAppHwnd, WinAPI.GWL_STYLE).ToInt64();
            _originalParent = GetParent(_hostedAppHwnd); // 使用 GetParent API
            Debug.WriteLine($"[AppHost Embed] 记录原始样式: 0x{_originalStyles:X}, 原始父窗口: {_originalParent}");

            // 2. 设置新的父窗口 (将外部窗口放入我们的宿主窗口)
            IntPtr previousParent = WinAPI.SetParent(_hostedAppHwnd, _hostHwnd);
            if (previousParent == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) // SetParent 失败且有错误码
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AppHost Embed] SetParent 失败，错误码: {error}。目标窗口 HWND: {_hostedAppHwnd}。可能需要管理员权限?");
                _hostedAppHwnd = IntPtr.Zero; // 嵌入失败
                return false;
            }
            Debug.WriteLine($"[AppHost Embed] SetParent 成功，旧父窗口: {previousParent}");

            // 3. 修改窗口样式 (移除边框、标题栏，添加 WS_CHILD)
            // 移除弹窗、边框、标题栏、系统菜单、最小化/最大化按钮，并强制设置为子窗口样式，防止调整和移动
            long newStyle = (_originalStyles & ~((long)WinAPI.WS_POPUP | (long)WinAPI.WS_CAPTION | (long)WinAPI.WS_BORDER | (long)WinAPI.WS_DLGFRAME | (long)WinAPI.WS_THICKFRAME | (long)WinAPI.WS_SYSMENU | (long)WinAPI.WS_MINIMIZEBOX | (long)WinAPI.WS_MAXIMIZEBOX)) | (long)WinAPI.WS_CHILD;
            Debug.WriteLine($"[AppHost Embed] 准备设置新样式: 0x{newStyle:X}");
            IntPtr previousStyle = WinAPI.SetWindowLongPtr(_hostedAppHwnd, WinAPI.GWL_STYLE, new IntPtr(newStyle));
            // 检查 SetWindowLongPtr 是否成功 (虽然它通常返回旧样式值，但在失败时可能返回0并设置LastWin32Error)
            if (previousStyle == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            {
                 int error = Marshal.GetLastWin32Error();
                 Debug.WriteLine($"[AppHost Embed] SetWindowLongPtr 可能失败，错误码: {error}。旧样式值: 0x{_originalStyles:X}");
                 // 这里不直接返回 false，因为有时即使有错误，样式也可能部分应用
            }
            else
            {
                 Debug.WriteLine($"[AppHost Embed] SetWindowLongPtr 调用完成，返回的旧样式值指针: {previousStyle} (不代表错误)");
            }


            // 4. 强制应用样式更改并调整大小/位置
            // 使用 SetWindowPos 替代 MoveWindow，因为它更灵活，并且可以同时发送 SWP_FRAMECHANGED
            Debug.WriteLine($"[AppHost Embed] 准备调用 SetWindowPos 调整位置和大小并应用框架更改...");
            bool posChanged = WinAPI.SetWindowPos(_hostedAppHwnd, IntPtr.Zero, // 不改变 Z 顺序
                0, 0, // 新位置 (x, y)
                (int)this.ActualWidth, (int)this.ActualHeight, // 新大小 (width, height)
                WinAPI.SWP_FRAMECHANGED | WinAPI.SWP_NOACTIVATE | WinAPI.SWP_NOZORDER); // 标志：应用框架更改，不激活，不改变 Z 顺序

            if (!posChanged)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[AppHost Embed] SetWindowPos 调整大小/位置失败，错误码: {error}");
                // 即使失败也继续，有时窗口仍然会被放置
            }
            else
            {
                 Debug.WriteLine($"[AppHost Embed] SetWindowPos 调整大小/位置成功。");
            }

            // 再次调用 SetWindowPos 确保样式生效 (有时需要多次调用或特定组合)
            // 这次只发送 FRAMECHANGED，不改变位置大小
            // Debug.WriteLine($"[AppHost Embed] 再次调用 SetWindowPos 仅应用框架更改...");
            // WinAPI.SetWindowPos(_hostedAppHwnd, IntPtr.Zero, 0, 0, 0, 0,
            //     WinAPI.SWP_NOMOVE | WinAPI.SWP_NOSIZE | WinAPI.SWP_NOZORDER | WinAPI.SWP_FRAMECHANGED | WinAPI.SWP_NOACTIVATE);


            Debug.WriteLine($"[AppHost Embed] 窗口 HWND: {_hostedAppHwnd} 嵌入完成。");
            // 注意：CurrentController 需要在调用 EmbedWindow 的地方设置
            return true;
        }

        // 恢复被嵌入窗口到其原始状态（分离）
        public void RestoreHostedWindow()
        {
            if (_hostedAppHwnd == IntPtr.Zero || !WinAPI.IsWindow(_hostedAppHwnd))
            {
                Debug.WriteLine($"[AppHost Restore] 无需恢复，目标窗口句柄 ({_hostedAppHwnd}) 无效或已为 Zero。");
                _hostedAppHwnd = IntPtr.Zero; // 确保重置
                return;
            }

            Debug.WriteLine($"[AppHost Restore] 开始恢复窗口 HWND: {_hostedAppHwnd}");

            // 1. 恢复父窗口 (通常恢复到桌面，即 IntPtr.Zero)
            IntPtr newParent = IntPtr.Zero; // 或者使用 _originalParent (如果记录可靠)
            Debug.WriteLine($"[AppHost Restore] 设置父窗口回: {newParent}");
            WinAPI.SetParent(_hostedAppHwnd, newParent);

            // 2. 恢复原始样式
            Debug.WriteLine($"[AppHost Restore] 恢复原始样式: 0x{_originalStyles:X}");
            WinAPI.SetWindowLongPtr(_hostedAppHwnd, WinAPI.GWL_STYLE, new IntPtr(_originalStyles));

            // 3. 强制应用样式更改
            WinAPI.SetWindowPos(_hostedAppHwnd, IntPtr.Zero, 0, 0, 0, 0,
                WinAPI.SWP_NOMOVE | WinAPI.SWP_NOSIZE | WinAPI.SWP_NOZORDER | WinAPI.SWP_FRAMECHANGED | WinAPI.SWP_SHOWWINDOW);

            Debug.WriteLine($"[AppHost Restore] 窗口 HWND: {_hostedAppHwnd} 恢复完成。");
            _hostedAppHwnd = IntPtr.Zero; // 清除记录
            _originalStyles = 0;
            _originalParent = IntPtr.Zero;
            CurrentController = null; // --- 新增：清除控制器引用 ---
        }

        // 当 HwndHost (即此控件) 大小改变时，调整嵌入窗口的大小
        public void ResizeEmbeddedWindow()
        {
            if (_hostHwnd != IntPtr.Zero && _hostedAppHwnd != IntPtr.Zero && WinAPI.IsWindow(_hostedAppHwnd))
            {
                // 获取当前DPI信息
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    // 获取DPI缩放因子
                    double dpiX = source.CompositionTarget.TransformToDevice.M11;
                    double dpiY = source.CompositionTarget.TransformToDevice.M22;
                    
                    // 使用DPI调整后的尺寸
                    int width = (int)(this.ActualWidth * dpiX);
                    int height = (int)(this.ActualHeight * dpiY);
                    
                    Debug.WriteLine($"[AppHost Resize] 调整窗口，应用DPI缩放: {dpiX}x{dpiY}，原始尺寸: {ActualWidth}x{ActualHeight}，调整后: {width}x{height}");
                    
                    // 将嵌入窗口的大小设置为考虑DPI缩放后的尺寸
                    bool moved = WinAPI.MoveWindow(_hostedAppHwnd, 0, 0, width, height, true);
                    Debug.WriteLine($"[AppHost Resize] 调整嵌入窗口 {_hostedAppHwnd} 大小为 {width}x{height}，结果: {moved}");
                }
                else
                {
                    // 退回到原来的实现（无DPI感知）
                    bool moved = WinAPI.MoveWindow(_hostedAppHwnd, 0, 0, (int)this.ActualWidth, (int)this.ActualHeight, true);
                    Debug.WriteLine($"[AppHost Resize] 调整嵌入窗口 {_hostedAppHwnd} 大小为 {ActualWidth}x{ActualHeight}，结果: {moved}");
                }
            }
        }

        // 处理窗口消息 (可选，但 WM_SIZE 很重要)
        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 只处理我们自己创建的宿主窗口的消息
            if (hwnd == _hostHwnd)
            {
                switch (msg)
                {
                    case WinAPI.WM_SIZE: // 当宿主窗口大小改变
                        // 调整内部嵌入的窗口大小以匹配
                        ResizeEmbeddedWindow();
                        handled = true; // 消息已处理
                        break;
                    
                    // 添加键盘消息转发 - 增强版
                    case WinAPI.WM_KEYDOWN:
                    case WinAPI.WM_KEYUP:
                    case WinAPI.WM_SYSKEYDOWN:
                    case WinAPI.WM_SYSKEYUP:
                    case WinAPI.WM_CHAR:
                    case WinAPI.WM_SYSCHAR:
                    case 0x0281: // WM_IME_SETCONTEXT
                    case 0x0282: // WM_IME_NOTIFY
                    case 0x0283: // WM_IME_CONTROL
                    case 0x0284: // WM_IME_COMPOSITIONFULL
                    case 0x0285: // WM_IME_SELECT
                    case 0x0286: // WM_IME_CHAR
                    case 0x0288: // WM_IME_REQUEST
                    case 0x010D: // WM_IME_STARTCOMPOSITION
                    case 0x010E: // WM_IME_ENDCOMPOSITION
                    case 0x010F: // WM_IME_COMPOSITION
                        // 如果有嵌入窗口且有效，则转发键盘和输入法消息
                        if (_hostedAppHwnd != IntPtr.Zero && WinAPI.IsWindow(_hostedAppHwnd))
                        {
                            // --- 修改：根据控制器类型选择转发方式 ---
                            if (CurrentController is Controllers.NeteaseMusicController)
                            {
                                // 网易云：仅使用 SendMessage
                                IntPtr result = WinAPI.SendMessage(_hostedAppHwnd, msg, wParam, lParam);
                                Debug.WriteLine($"[AppHost KeyEvent][Netease] SendMessage 0x{msg:X4} to {_hostedAppHwnd}, Result: {result}");
                            }
                            else
                            {
                                // 其他应用：使用 PostMessage + SendMessage (旧方式)
                                WinAPI.PostMessage(_hostedAppHwnd, msg, wParam, lParam);
                                IntPtr result = WinAPI.SendMessage(_hostedAppHwnd, msg, wParam, lParam);
                                Debug.WriteLine($"[AppHost KeyEvent][Other] Post+SendMessage 0x{msg:X4} to {_hostedAppHwnd}, Result: {result}");
                            }
                            // --- 修改结束 ---

                            // 调试输出
                            if (msg == WinAPI.WM_CHAR)
                            {
                                char c = (char)wParam.ToInt32();
                                Debug.WriteLine($"[AppHost KeyEvent] 转发字符输入 '{c}' (0x{wParam.ToInt32():X4}) 到窗口 {_hostedAppHwnd}");
                            }
                            else
                            {
                                Debug.WriteLine($"[AppHost KeyEvent] 转发消息 0x{msg:X4} wParam=0x{wParam.ToInt32():X4} 到窗口 {_hostedAppHwnd}");
                            }
                            
                            // 标记为已处理，避免WPF再处理一遍
                            handled = true;
                        }
                        break;
                    
                    // 处理焦点获取
                    case WinAPI.WM_SETFOCUS:
                        // 当宿主获得焦点时，将焦点传递给嵌入窗口
                        if (_hostedAppHwnd != IntPtr.Zero && WinAPI.IsWindow(_hostedAppHwnd))
                        {
                            WinAPI.SetFocus(_hostedAppHwnd);
                            Debug.WriteLine($"[AppHost Focus] 将焦点设置到嵌入窗口 {_hostedAppHwnd}");
                            handled = true;
                        }
                        break;
                        
                    // 处理鼠标点击，确保点击时自动获取焦点
                    case WinAPI.WM_LBUTTONDOWN:
                    case WinAPI.WM_RBUTTONDOWN:
                    case WinAPI.WM_MBUTTONDOWN:
                        if (_hostedAppHwnd != IntPtr.Zero && WinAPI.IsWindow(_hostedAppHwnd))
                        {
                            WinAPI.SetFocus(_hostedAppHwnd);
                            // 不标记为已处理，让消息继续传递
                        }
                        break;
                    case WinAPI.WM_NCHITTEST: // 禁止调整大小和移动，所有区域都当作客户区处理
                        handled = true;
                        return new IntPtr(WinAPI.HTCLIENT);
                    case WinAPI.WM_SYSCOMMAND: // 拦截系统移动和调整大小命令
                        int sysCmd = wParam.ToInt32() & 0xFFF0;
                        if (sysCmd == WinAPI.SC_MOVE || sysCmd == WinAPI.SC_SIZE)
                        {
                            handled = true; // 禁止执行移动和调整大小
                        }
                        break;
                }
            }
            // 调用基类处理其他消息
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        // 获取父窗口句柄
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);
    }
}
