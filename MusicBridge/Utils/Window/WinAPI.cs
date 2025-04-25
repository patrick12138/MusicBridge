using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicBridge.Utils.Window
{
    public static class WinAPI
    {
        // --- Windows 消息常量 ---
        public const int WM_APPCOMMAND = 0x0319;    // 用于向窗口发送媒体控制命令的Windows消息
        public const int WM_CLOSE = 0x0010;       // 关闭窗口消息
        public const int WM_DESTROY = 0x0002;     // 销毁窗口消息
        public const int WM_SIZE = 0x0005;        // 窗口大小改变消息
        public const int WM_SETTEXT = 0x000C;     // 用于设置窗口文本的消息
        public const int WM_KEYDOWN = 0x0100;     // 键盘按下消息
        public const int WM_KEYUP = 0x0101;       // 键盘抬起消息
        public const int WM_SYSKEYDOWN = 0x0104;  // 系统键(如Alt)按下消息
        public const int WM_SYSKEYUP = 0x0105;    // 系统键(如Alt)抬起消息
        public const int WM_CHAR = 0x0102;        // 字符输入消息
        public const int WM_SYSCHAR = 0x0106;     // 系统字符输入消息
        public const int WM_SETFOCUS = 0x0007;    // 窗口获得焦点消息
        public const int WM_KILLFOCUS = 0x0008;   // 窗口失去焦点消息
        public const int WM_LBUTTONDOWN = 0x0201; // 鼠标左键按下消息
        public const int WM_RBUTTONDOWN = 0x0204; // 鼠标右键按下消息
        public const int WM_MBUTTONDOWN = 0x0207; // 鼠标中键按下消息
        
        // 添加窗口移动相关消息
        public const int WM_NCLBUTTONDOWN = 0x00A1; // 非客户区鼠标左键按下（如标题栏、边框）
        public const int WM_NCRBUTTONDOWN = 0x00A4; // 非客户区鼠标右键按下
        public const int WM_NCHITTEST = 0x0084;     // 用于处理窗口点击区域测试
        public const int WM_MOVING = 0x0216;        // 窗口正在移动
        public const int WM_MOVE = 0x0003;          // 窗口已经移动
        public const int WM_ENTERSIZEMOVE = 0x0231; // 窗口开始移动或改变大小
        public const int WM_EXITSIZEMOVE = 0x0232;  // 窗口结束移动或改变大小
        public const int WM_SYSCOMMAND = 0x0112;    // 系统命令消息（移动、大小等）
        
        // 系统命令常量
        public const int SC_MOVE = 0xF010;          // 移动窗口系统命令
        public const int SC_SIZE = 0xF000;          // 调整窗口大小系统命令

        // --- 媒体控制按键虚拟键码 ---
        public const byte VK_MEDIA_NEXT_TRACK = 0xB0;  // 多媒体下一曲键
        public const byte VK_MEDIA_PREV_TRACK = 0xB1;  // 多媒体上一曲键
        public const byte VK_MEDIA_STOP = 0xB2;        // 多媒体停止键
        public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;  // 多媒体播放/暂停键

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
        public const uint WS_BORDER = 0x00800000;      // 边框
        public const uint WS_DLGFRAME = 0x00400000;    // 对话框边框
        public const uint WS_THICKFRAME = 0x00040000;  // 可调整大小的边框 (Sizing Border)
        public const uint WS_MINIMIZEBOX = 0x00020000; // 最小化按钮
        public const uint WS_MAXIMIZEBOX = 0x00010000; // 最大化按钮
        public const uint WS_SYSMENU = 0x00080000;     // 系统菜单按钮

        // --- 扩展窗口样式常量 (WS_EX_*) ---
        public const uint WS_EX_DLGMODALFRAME = 0x00000001;  // 双边框窗口
        public const uint WS_EX_TOPMOST = 0x00000008;       // 总在最前
        public const uint WS_EX_ACCEPTFILES = 0x00000010;   // 接受拖放文件
        public const uint WS_EX_TRANSPARENT = 0x00000020;   // 透明窗口
        public const uint WS_EX_MDICHILD = 0x00000040;      // MDI子窗口
        public const uint WS_EX_TOOLWINDOW = 0x00000080;    // 工具窗口
        public const uint WS_EX_WINDOWEDGE = 0x00000100;    // 窗口边缘
        public const uint WS_EX_CLIENTEDGE = 0x00000200;    // 客户区边缘
        public const uint WS_EX_CONTEXTHELP = 0x00000400;   // 上下文帮助按钮
        public const uint WS_EX_RIGHT = 0x00001000;         // 右对齐
        public const uint WS_EX_RTLREADING = 0x00002000;    // 从右到左读取顺序
        public const uint WS_EX_LEFTSCROLLBAR = 0x00004000; // 将滚动条放在左侧
        public const uint WS_EX_CONTROLPARENT = 0x00010000; // 允许用户使用Tab键导航
        public const uint WS_EX_STATICEDGE = 0x00020000;    // 三维边框
        public const uint WS_EX_APPWINDOW = 0x00040000;     // 在任务栏上显示窗口
        public const uint WS_EX_LAYERED = 0x00080000;       // 分层窗口
        public const uint WS_EX_NOINHERITLAYOUT = 0x00100000; // 不继承布局
        public const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000; // 不重定向位图
        public const uint WS_EX_LAYOUTRTL = 0x00400000;     // 从右到左布局
        public const uint WS_EX_COMPOSITED = 0x02000000;    // 复合窗口
        public const uint WS_EX_NOACTIVATE = 0x08000000;    // 不激活窗口

        // --- NCHITTEST 返回值 ---
        public const int HTCLIENT = 1;      // 在客户区域
        public const int HTCAPTION = 2;     // 在标题栏
        public const int HTSYSMENU = 3;     // 在系统菜单
        public const int HTGROWBOX = 4;     // 在大小调整框
        public const int HTSIZE = 4;        // 与HTGROWBOX相同
        public const int HTMENU = 5;        // 在菜单
        public const int HTHSCROLL = 6;     // 在水平滚动条
        public const int HTVSCROLL = 7;     // 在垂直滚动条
        public const int HTMINBUTTON = 8;   // 在最小化按钮
        public const int HTMAXBUTTON = 9;   // 在最大化按钮
        public const int HTLEFT = 10;       // 在左边框
        public const int HTRIGHT = 11;      // 在右边框
        public const int HTTOP = 12;        // 在上边框
        public const int HTTOPLEFT = 13;    // 在左上角
        public const int HTTOPRIGHT = 14;   // 在右上角
        public const int HTBOTTOM = 15;     // 在下边框
        public const int HTBOTTOMLEFT = 16; // 在左下角
        public const int HTBOTTOMRIGHT = 17;// 在右下角
        public const int HTBORDER = 18;     // 在边框

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


        // --- 键盘相关常量 ---
        // 虚拟键码常量 (Virtual-Key Codes)
        public const ushort VK_LBUTTON = 0x01;   // 鼠标左键
        public const ushort VK_RBUTTON = 0x02;   // 鼠标右键
        public const ushort VK_CANCEL = 0x03;    // Control-break processing
        public const ushort VK_MBUTTON = 0x04;   // 鼠标中键
        
        public const ushort VK_BACK = 0x08;      // BACKSPACE 键
        public const ushort VK_TAB = 0x09;       // TAB 键
        
        // public const ushort VK_RETURN = 0x0D;    // ENTER 键
        
        public const ushort VK_SHIFT = 0x10;     // SHIFT 键
        public const ushort VK_CONTROL = 0x11;   // CTRL 键
        public const ushort VK_MENU = 0x12;      // ALT 键
        public const ushort VK_PAUSE = 0x13;     // PAUSE 键
        public const ushort VK_CAPITAL = 0x14;   // CAPS LOCK 键
        
        public const ushort VK_ESCAPE = 0x1B;    // ESC 键
        public const ushort VK_SPACE = 0x20;     // SPACEBAR 键
        public const ushort VK_PRIOR = 0x21;     // PAGE UP 键
        public const ushort VK_NEXT = 0x22;      // PAGE DOWN 键
        public const ushort VK_END = 0x23;       // END 键
        public const ushort VK_HOME = 0x24;      // HOME 键
        public const ushort VK_LEFT = 0x25;      // LEFT ARROW 键
        public const ushort VK_UP = 0x26;        // UP ARROW 键
        public const ushort VK_RIGHT = 0x27;     // RIGHT ARROW 键
        public const ushort VK_DOWN = 0x28;      // DOWN ARROW 键
        
        public const ushort VK_PRINT = 0x2A;     // PRINT 键
        public const ushort VK_SNAPSHOT = 0x2C;  // PRINT SCREEN 键
        public const ushort VK_INSERT = 0x2D;    // INS 键
        public const byte VK_DELETE = 0x2E;    // DEL 键
        
        // 字母键 (A-Z)
        public const ushort VK_A = 0x41;
        public const ushort VK_B = 0x42;
        public const ushort VK_C = 0x43;
        public const ushort VK_D = 0x44;
        public const ushort VK_E = 0x45;
        public const ushort VK_F = 0x46;
        public const ushort VK_G = 0x47;
        public const ushort VK_H = 0x48;
        public const ushort VK_I = 0x49;
        public const ushort VK_J = 0x4A;
        public const ushort VK_K = 0x4B;
        public const ushort VK_L = 0x4C;
        public const ushort VK_M = 0x4D;
        public const ushort VK_N = 0x4E;
        public const ushort VK_O = 0x4F;
        public const ushort VK_P = 0x50;
        public const ushort VK_Q = 0x51;
        public const ushort VK_R = 0x52;
        public const ushort VK_S = 0x53;
        public const ushort VK_T = 0x54;
        public const ushort VK_U = 0x55;
        public const ushort VK_V = 0x56;
        public const ushort VK_W = 0x57;
        public const ushort VK_X = 0x58;
        public const ushort VK_Y = 0x59;
        public const ushort VK_Z = 0x5A;

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
            public nint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo;
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
        public static extern nint GetMessageExtraInfo();

        // --- Windows API 函数导入 ---
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint SendMessageW(nint hWnd, int Msg, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] // 使用 CharSet.Auto 自动处理 ANSI/Unicode
        public static extern nint SendMessage(nint hWnd, int Msg, nint wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam); // lParam 作为 LPWStr 发送 Unicode 字符串

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern nint SendMessage(nint hWnd, int Msg, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

        public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(nint hWnd); // 检查窗口句柄是否有效

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        // --- 用于嵌入窗口的 API ---
        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint SetParent(nint hWndChild, nint hWndNewParent); // 设置父窗口

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint); // 移动并调整窗口大小

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags); // 设置窗口位置、大小和Z顺序

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern nint GetWindowLongPtr32(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

        // 自动选择32/64位 GetWindowLongPtr
        public static nint GetWindowLongPtr(nint hWnd, int nIndex)
        {
            return nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

        // 自动选择32/64位 SetWindowLongPtr
        public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
        {
            if (nint.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new nint(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(
           uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
           int x, int y, int nWidth, int nHeight,
           nint hWndParent, nint hMenu, nint hInstance, nint lpParam); // 创建窗口 (用于 HwndHost)

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(nint hwnd); // 销毁窗口 (用于 HwndHost)

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        public static extern nint SetFocus(nint hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(nint hwndParent, EnumWindowsProc lpEnumFunc, nint lParam);
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

        // 获取当前拥有键盘焦点的窗口句柄
        [DllImport("user32.dll")]
        public static extern nint GetFocus();

        // 获取指定窗口的父窗口句柄
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern nint GetParent(nint hWnd);

        /// <summary>
        /// 异步模拟单个按键的按下和抬起。
        /// </summary>
        /// <param name="vkCode">要模拟的虚拟键码。</param>
        public static async Task SendKeyPressAsync(byte vkCode)
        {
            keybd_event(vkCode, 0, KEYEVENTF_EXTENDEDKEY, nuint.Zero); // 按下
            await Task.Delay(30); // 模拟短暂按住
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, nuint.Zero); // 抬起
        }

        /// <summary>
        /// 异步模拟组合键 (例如 Ctrl+Right)。
        /// </summary>
        /// <param name="modifierVkCode">修饰键的虚拟键码 (例如 VK_CONTROL)。</param>
        /// <param name="vkCode">普通键的虚拟键码 (例如 VK_RIGHT)。</param>
        public static async Task SendCombinedKeyPressAsync(byte modifierVkCode, byte vkCode)
        {
            keybd_event(modifierVkCode, 0, KEYEVENTF_EXTENDEDKEY, nuint.Zero); // 按下修饰键
            await Task.Delay(30);
            keybd_event(vkCode, 0, KEYEVENTF_EXTENDEDKEY, nuint.Zero);         // 按下普通键
            await Task.Delay(30);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, nuint.Zero); // 抬起普通键
            await Task.Delay(30);
            keybd_event(modifierVkCode, 0, KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY, nuint.Zero); // 抬起修饰键
        }

        // --- 新增：使用 SendInput 模拟组合键 --- 
        public static async Task SimulateKeyPressWithModifiers(List<ushort> modifierKeys, ushort primaryKey)
        {
            List<INPUT> inputs = new List<INPUT>();
            nint extraInfo = GetMessageExtraInfo();

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
        public static nint FindMainWindow(string processName)
        {
            nint foundHwnd = nint.Zero;
            int maxTitleLen = 0;
            List<nint> potentialHwnds = new List<nint>();

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
            }, nint.Zero);

            if (foundHwnd != nint.Zero) return foundHwnd;
            if (potentialHwnds.Count == 1) return potentialHwnds[0];
            return nint.Zero;
        }

        // 将消息发送到指定窗口的消息队列，然后立即返回（异步）
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(nint hWnd, int Msg, nint wParam, nint lParam);
        
        // 将虚拟键码映射到扫描码
        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // --- 鼠标相关常量 ---
        public const uint INPUT_MOUSE = 0;
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        
        // --- 系统信息常量 ---
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        
        // --- 窗口和客户区矩形相关 ---
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(nint hWnd, ref RECT lpRect);
        
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(nint hWnd, ref RECT lpRect);
        
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);
        
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        // 添加 SetForegroundWindow 函数声明
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(nint hWnd);
        
        // 向指定窗口发送按键序列
        public static void SendKeys(nint hWnd, string keys)
        {
            // 确保窗口处于前台
            SetForegroundWindow(hWnd);
            Thread.Sleep(100); // 给系统一点时间响应
           
        }

        // 添加 ClickWindowAt 方法 - 点击窗口的指定位置
        public static void ClickWindowAt(nint hWnd, int x, int y)
        {
            try
            {
                // 确保窗口处于前台
                SetForegroundWindow(hWnd);
                Thread.Sleep(50);

                // 获取窗口客户区域在屏幕上的坐标
                RECT rect = new RECT();
                GetClientRect(hWnd, ref rect);
                
                POINT point = new POINT { X = 0, Y = 0 };
                ClientToScreen(hWnd, ref point);
                
                // 计算实际要点击的屏幕坐标
                int screenX = point.X + x;
                int screenY = point.Y + y;
                
                // 获取屏幕分辨率
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);
                
                // 将坐标转换为规范化坐标 (0-65535)
                int normalizedX = screenX * 65535 / screenWidth;
                int normalizedY = screenY * 65535 / screenHeight;
                
                // 准备输入
                INPUT[] inputs = new INPUT[3];
                
                // 1. 移动鼠标到目标位置
                inputs[0] = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new INPUT_UNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = normalizedX,
                            dy = normalizedY,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 2. 鼠标左键按下
                inputs[1] = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new INPUT_UNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTDOWN,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 3. 鼠标左键抬起
                inputs[2] = new INPUT
                {
                    type = INPUT_MOUSE,
                    u = new INPUT_UNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTUP,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };
                
                // 发送输入
                uint result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                if (result == 0)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"[ClickWindowAt] SendInput 失败，错误代码: {errorCode}");
                }
                else
                {
                    Debug.WriteLine($"[ClickWindowAt] 成功点击位置 ({x}, {y})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClickWindowAt] 异常: {ex}");
            }
        }

        // 启用或禁用窗口
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnableWindow(nint hWnd, bool bEnable);
    }
}