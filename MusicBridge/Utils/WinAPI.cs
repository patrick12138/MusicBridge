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
        public const int WM_SETTEXT = 0x000C; // 用于设置窗口文本的消息
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;

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
        public const int VK_RETURN = 0x0D; // 回车键


        // 普通键盘按键的虚拟键码
        public const byte VK_SPACE = 0x20;
        public const byte VK_LEFT = 0x25;
        public const byte VK_RIGHT = 0x27;
        public const byte VK_UP = 0x26;
        public const byte VK_DOWN = 0x28;
        public const byte VK_CONTROL = 0x11;
        public const byte VK_MENU = 0x12;    // Alt键
        public const byte VK_SHIFT = 0x10;
        public const byte VK_LWIN = 0x5B;    // 左 Windows 键
        public const byte VK_RWIN = 0x5C;    // 右 Windows 键
        public const byte VK_P = 0x50;       // P 键
                                             // --- 键盘事件标志 (用于 keybd_event 函数) ---
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;  // 指示扩展键 (例如箭头键、功能键等)
        public const uint KEYEVENTF_KEYUP = 0x0002;        // 指示按键释放

        // --- SendInput 相关结构体和常量 ---
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT_UNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        public struct INPUT
        {
            public uint type; // 0 = MOUSE, 1 = KEYBOARD, 2 = HARDWARE
            public INPUT_UNION u;
        }

        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYDOWN = 0x0000;
        public const uint KEYEVENTF_SCANCODE = 0x0008;
        public const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetMessageExtraInfo();

        // --- Windows API 函数导入 ---
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] // 使用 CharSet.Auto 自动处理 ANSI/Unicode
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam); // lParam 作为 LPWStr 发送 Unicode 字符串

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // 获取当前拥有键盘焦点的窗口句柄
        [DllImport("user32.dll")]
        public static extern IntPtr GetFocus();

        // 获取指定窗口的父窗口句柄
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        /// <summary>
        /// 异步释放常用的修饰键 (Ctrl, Alt, Shift, Win)。
        /// </summary>
        public static async Task ReleaseAllModifierKeysAsync()
        {
            byte[] modifierKeys = { VK_CONTROL, VK_MENU, VK_SHIFT, VK_LWIN, VK_RWIN };
            foreach (byte key in modifierKeys)
            {
                // 发送抬起事件，以防万一按键被卡住
                keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            await Task.Delay(30); // 短暂等待
        }

        /// <summary>
        /// 异步模拟单个按键的按下和抬起。
        /// </summary>
        /// <param name="vkCode">要模拟的虚拟键码。</param>
        public static async Task SendKeyPressAsync(byte vkCode)
        {
            keybd_event(vkCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero); // 按下
            await Task.Delay(30); // 模拟短暂按住
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero); // 抬起
        }

        /// <summary>
        /// 异步模拟组合键 (例如 Ctrl+Right)。
        /// </summary>
        /// <param name="modifierVkCode">修饰键的虚拟键码 (例如 VK_CONTROL)。</param>
        /// <param name="vkCode">普通键的虚拟键码 (例如 VK_RIGHT)。</param>
        public static async Task SendCombinedKeyPressAsync(byte modifierVkCode, byte vkCode)
        {
            keybd_event(modifierVkCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero); // 按下修饰键
            await Task.Delay(30);
            keybd_event(vkCode, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);         // 按下普通键
            await Task.Delay(30);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero); // 抬起普通键
            await Task.Delay(30);
            keybd_event(modifierVkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero); // 抬起修饰键
        }

        // --- 新增：使用 SendInput 模拟组合键 --- 
        public static async Task SimulateKeyPressWithModifiers(List<ushort> modifierKeys, ushort primaryKey)
        {
            List<INPUT> inputs = new List<INPUT>();
            IntPtr extraInfo = GetMessageExtraInfo();

            // 1. 按下所有修饰键
            foreach (var modKey in modifierKeys)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUT_UNION { ki = new KEYBDINPUT { wVk = modKey, dwFlags = KEYEVENTF_KEYDOWN, dwExtraInfo = extraInfo } }
                });
            }

            // 2. 按下主键
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION { ki = new KEYBDINPUT { wVk = primaryKey, dwFlags = KEYEVENTF_KEYDOWN, dwExtraInfo = extraInfo } }
            });

            // 3. 释放主键
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION { ki = new KEYBDINPUT { wVk = primaryKey, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = extraInfo } }
            });

            // 4. 释放所有修饰键 (按相反顺序释放可能更稳妥)
            modifierKeys.Reverse(); // 反转列表
            foreach (var modKey in modifierKeys)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUT_UNION { ki = new KEYBDINPUT { wVk = modKey, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = extraInfo } }
                });
            }

            // 发送输入
            uint result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (result == 0)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[SimulateKeyPressWithModifiers] SendInput failed with error code: {errorCode}");
                // 可以考虑抛出异常或返回 false
            }
            else
            {
                Debug.WriteLine($"[SimulateKeyPressWithModifiers] SendInput succeeded for primary key {primaryKey} with {modifierKeys.Count} modifiers.");
            }
            // SendInput 是同步的，但我们保持 async Task 签名以防未来需要延迟
            await Task.CompletedTask; 
        }

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