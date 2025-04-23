using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicBridge
{
    public static class WinAPI
    {
        // --- Windows 消息常量 ---
        public const int WM_APPCOMMAND = 0x0319;    // 用于向窗口发送媒体控制命令的Windows消息
        public const int WM_CLOSE = 0x0010;       // 关闭窗口消息
        public const int WM_DESTROY = 0x0002;     // 销毁窗口消息
        public const int WM_SIZE = 0x0005;        // 窗口大小改变消息

        // --- APPCOMMAND 常量 (用于 WM_APPCOMMAND 消息) ---
        public const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14 << 16;
        public const int APPCOMMAND_MEDIA_NEXTTRACK = 11 << 16;
        public const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12 << 16;
        public const int APPCOMMAND_VOLUME_MUTE = 8 << 16;
        public const int APPCOMMAND_VOLUME_DOWN = 9 << 16;
        public const int APPCOMMAND_VOLUME_UP = 10 << 16;

        // --- Window Styles (用于修改窗口样式) ---
        public const uint WS_CHILD = 0x40000000;       // 子窗口样式
        public const uint WS_VISIBLE = 0x10000000;     // 可见样式
        public const uint WS_CLIPSIBLINGS = 0x04000000; // 裁剪兄弟窗口区域
        public const uint WS_CLIPCHILDREN = 0x02000000; // 裁剪子窗口区域
        public const uint WS_POPUP = 0x80000000;       // 弹出窗口样式
        public const uint WS_CAPTION = 0x00C00000;     // 标题栏
        public const uint WS_BORDER = 0x00800000;     // 边框
        public const uint WS_DLGFRAME = 0x00400000;     // 对话框边框
        public const uint WS_THICKFRAME = 0x00040000;     // 可调整大小的边框 (Sizing Border)

        // --- Get/SetWindowLongPtr 索引 ---
        public const int GWL_STYLE = -16;             // 获取/设置窗口样式
        public const int GWL_EXSTYLE = -20;           // 获取/设置扩展窗口样式
        public const int GWLP_HWNDPARENT = -8;        // 获取/设置父窗口句柄 (仅用于 SetWindowLongPtr)

        // --- SetWindowPos Flags (用于调整窗口位置和状态) ---
        public const uint SWP_NOSIZE = 0x0001;         // 忽略 cx, cy 参数，保持大小
        public const uint SWP_NOMOVE = 0x0002;         // 忽略 X, Y 参数，保持位置
        public const uint SWP_NOZORDER = 0x0004;       // 保持 Z 顺序
        public const uint SWP_FRAMECHANGED = 0x0020;   // 应用 SetWindowLongPtr 后的样式更改，强制重绘边框
        public const uint SWP_SHOWWINDOW = 0x0040;     // 显示窗口
        public const uint SWP_NOACTIVATE = 0x0010;     // 不激活窗口

        // --- ShowWindow 命令 ---
        public const int SW_RESTORE = 9;              // 还原窗口

        // --- Windows API 函数导入 ---
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd); // 检查窗口句柄是否有效

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // --- 用于嵌入窗口的 API ---
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent); // 设置父窗口

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint); // 移动并调整窗口大小

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags); // 设置窗口位置、大小和Z顺序

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        // 自动选择32/64位 GetWindowLongPtr
        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 自动选择32/64位 SetWindowLongPtr
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
           uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
           int x, int y, int nWidth, int nHeight,
           IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam); // 创建窗口 (用于 HwndHost)

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hwnd); // 销毁窗口 (用于 HwndHost)

        // 查找主窗口方法 (保持不变，用于初始查找)
        public static IntPtr FindMainWindow(string processName)
        {
            IntPtr foundHwnd = IntPtr.Zero;
            int maxTitleLen = 0;
            List<IntPtr> potentialHwnds = new List<IntPtr>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return true;

                try
                {
                    using (Process proc = Process.GetProcessById((int)pid))
                    {
                        if (proc.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                        {
                            potentialHwnds.Add(hWnd);
                            int currentTitleLen = GetWindowTextLength(hWnd);
                            if (currentTitleLen > maxTitleLen)
                            {
                                maxTitleLen = currentTitleLen;
                                foundHwnd = hWnd;
                            }
                        }
                    }
                }
                catch { /* 忽略查找过程中的进程退出等错误 */ }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != IntPtr.Zero) return foundHwnd;
            if (potentialHwnds.Count == 1) return potentialHwnds[0];
            return IntPtr.Zero;
        }
    }
}