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
            // 计算新样式：保留原始样式，移除弹窗/边框/标题栏，强制添加 Child 样式
            long newStyle = (_originalStyles & ~((long)WinAPI.WS_POPUP | (long)WinAPI.WS_CAPTION | (long)WinAPI.WS_BORDER | (long)WinAPI.WS_DLGFRAME | (long)WinAPI.WS_THICKFRAME)) | (long)WinAPI.WS_CHILD;
            Debug.WriteLine($"[AppHost Embed] 设置新样式: 0x{newStyle:X}");
            WinAPI.SetWindowLongPtr(_hostedAppHwnd, WinAPI.GWL_STYLE, new IntPtr(newStyle));

            // 4. 强制应用样式更改并调整大小/位置
            // 移动窗口到宿主客户区的 (0,0)，并设置为宿主的大小
            bool moved = WinAPI.MoveWindow(_hostedAppHwnd, 0, 0, (int)this.ActualWidth, (int)this.ActualHeight, true);
            Debug.WriteLine($"[AppHost Embed] MoveWindow 结果: {moved}");

            // 再次调用 SetWindowPos 确保样式生效
            WinAPI.SetWindowPos(_hostedAppHwnd, IntPtr.Zero, 0, 0, 0, 0,
                WinAPI.SWP_NOMOVE | WinAPI.SWP_NOSIZE | WinAPI.SWP_NOZORDER | WinAPI.SWP_FRAMECHANGED | WinAPI.SWP_NOACTIVATE);

            Debug.WriteLine($"[AppHost Embed] 窗口 HWND: {_hostedAppHwnd} 嵌入完成。");
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
        }

        // 当 HwndHost (即此控件) 大小改变时，调整嵌入窗口的大小
        public void ResizeEmbeddedWindow()
        {
            if (_hostHwnd != IntPtr.Zero && _hostedAppHwnd != IntPtr.Zero && WinAPI.IsWindow(_hostedAppHwnd))
            {
                // 将嵌入窗口的大小设置为宿主窗口的当前客户区大小
                bool moved = WinAPI.MoveWindow(_hostedAppHwnd, 0, 0, (int)this.ActualWidth, (int)this.ActualHeight, true);
                Debug.WriteLine($"[AppHost Resize] 调整嵌入窗口 {_hostedAppHwnd} 大小为 {ActualWidth}x{ActualHeight}，结果: {moved}");
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
                        // 可以添加其他消息处理，例如焦点管理
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
