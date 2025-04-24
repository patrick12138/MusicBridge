using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MusicBridge.Utils
{
    /// <summary>
    /// 管理搜索功能的类
    /// </summary>
    public class SearchManager
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _updateStatus;
        
        /// <summary>
        /// 创建SearchManager实例
        /// </summary>
        public SearchManager(Dispatcher dispatcher, Action<string> updateStatus)
        {
            _dispatcher = dispatcher;
            _updateStatus = updateStatus;
        }
        
        /// <summary>
        /// 执行搜索操作（通过WPF界面搜索框）
        /// </summary>
        public async Task PerformSearch(IntPtr embeddedWindowHandle, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return;
                
            // 检查是否有嵌入的窗口
            if (embeddedWindowHandle == IntPtr.Zero || !WinAPI.IsWindow(embeddedWindowHandle))
            {
                _updateStatus("错误：没有有效的嵌入窗口。请先启动并嵌入音乐应用。");
                return;
            }
            
            _updateStatus($"正在搜索: {searchText}...");
            
            try
            {
                // 尝试通过输入焦点自动输入文本到搜索框
                await SimulateSearchInput(embeddedWindowHandle, searchText);
            }
            catch (Exception ex)
            {
                _updateStatus($"搜索操作失败: {ex.Message}");
                Debug.WriteLine($"[搜索失败] {ex}");
            }
        }
        
        /// <summary>
        /// 执行直接搜索操作（无需通过WPF界面搜索框）
        /// </summary>
        public async Task PerformDirectSearch(IntPtr embeddedWindowHandle, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return;
                
            // 检查是否有嵌入的窗口
            if (embeddedWindowHandle == IntPtr.Zero || !WinAPI.IsWindow(embeddedWindowHandle))
            {
                _updateStatus("错误：没有有效的嵌入窗口。请先启动并嵌入音乐应用。");
                return;
            }
            
            _updateStatus($"正在直接搜索: {searchText}...");
            
            try
            {
                // 尝试直接模拟点击搜索框和输入文本
                bool searchBoxClicked = await TryClickSearchBox(embeddedWindowHandle);
                
                if (!searchBoxClicked)
                {
                    _updateStatus("未能找到搜索框，请尝试手动点击搜索框后再搜索");
                    return;
                }
                
                // 等待一段时间，让搜索框获取焦点
                await Task.Delay(300);
                
                // 清除当前文本
                await WinAPI.SimulateKeyPressWithModifiers(new List<ushort> { WinAPI.VK_CONTROL }, WinAPI.VK_A);
                await Task.Delay(100);
                await WinAPI.SendKeyPressAsync(WinAPI.VK_DELETE);
                await Task.Delay(100);
                
                // 粘贴搜索文本
                try
                {
                    // 保存当前剪贴板内容
                    IDataObject oldClipboard = System.Windows.Clipboard.GetDataObject();
                    
                    // 设置新的剪贴板内容
                    System.Windows.Clipboard.SetText(searchText);
                    await Task.Delay(100);
                    
                    // 发送粘贴命令
                    await WinAPI.SimulateKeyPressWithModifiers(new List<ushort> { WinAPI.VK_CONTROL }, WinAPI.VK_V);
                    await Task.Delay(200);
                    
                    // 发送回车键
                    await WinAPI.SendKeyPressAsync(WinAPI.VK_RETURN);
                    
                    // 恢复原剪贴板内容
                    if (oldClipboard != null)
                    {
                        try { System.Windows.Clipboard.SetDataObject(oldClipboard, true); } catch { }
                    }
                    
                    _updateStatus($"已直接搜索: {searchText}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[粘贴搜索文本失败] {ex.Message}");
                    
                    // 如果粘贴失败，尝试模拟键盘输入
                    await SimulateTypingText(searchText);
                    await Task.Delay(100);
                    await WinAPI.SendKeyPressAsync(WinAPI.VK_RETURN);
                    
                    _updateStatus($"已通过模拟键盘输入搜索: {searchText}");
                }
            }
            catch (Exception ex)
            {
                _updateStatus($"直接搜索操作失败: {ex.Message}");
                Debug.WriteLine($"[直接搜索失败] {ex}");
            }
        }

        /// <summary>
        /// Simulates search input in the embedded window
        /// </summary>
        private async Task SimulateSearchInput(IntPtr hwnd, string searchText)
        {
            if (hwnd == IntPtr.Zero || !WinAPI.IsWindow(hwnd))
                return;
                
            // 步骤1: 尝试定位搜索框并点击
            bool searchBoxClicked = await TryClickSearchBox(hwnd);
            if (!searchBoxClicked)
            {
                _updateStatus("未能找到搜索框，请尝试手动点击搜索框后再搜索");
                return;
            }
            
            // 步骤2: 输入搜索文本
            await Task.Delay(300); // 给搜索框获取焦点留出时间
            
            // 清空当前文本 (Ctrl+A 然后删除)
            await WinAPI.SimulateKeyPressWithModifiers(new List<ushort> { WinAPI.VK_CONTROL }, WinAPI.VK_A);
            await Task.Delay(100);
            await WinAPI.SendKeyPressAsync(WinAPI.VK_DELETE);
            await Task.Delay(100);
            
            // 获取当前焦点窗口
            IntPtr focusedHwnd = WinAPI.GetFocus();
            if (focusedHwnd == IntPtr.Zero)
            {
                _updateStatus("未能获取输入焦点");
                return;
            }
            
            // 方法1: 尝试使用剪贴板粘贴文本
            try
            {
                // 保存当前剪贴板内容
                IDataObject oldClipboard = System.Windows.Clipboard.GetDataObject();
                
                // 设置新的剪贴板内容
                System.Windows.Clipboard.SetText(searchText);
                await Task.Delay(100);
                
                // 发送粘贴命令 (Ctrl+V)
                await WinAPI.SimulateKeyPressWithModifiers(new List<ushort> { WinAPI.VK_CONTROL }, WinAPI.VK_V);
                await Task.Delay(200);
                
                // 发送回车键
                await WinAPI.SendKeyPressAsync(WinAPI.VK_RETURN);
                
                // 尝试恢复原剪贴板内容
                if (oldClipboard != null)
                {
                    try { System.Windows.Clipboard.SetDataObject(oldClipboard, true); } catch { }
                }
                
                _updateStatus($"已搜索: {searchText}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[粘贴搜索文本失败] {ex.Message}");
                
                // 方法2: 如果粘贴失败，尝试模拟键盘输入
                await SimulateTypingText(searchText);
                await Task.Delay(100);
                await WinAPI.SendKeyPressAsync(WinAPI.VK_RETURN);
                
                _updateStatus($"已通过模拟键盘输入搜索: {searchText}");
            }
        }
        
        /// <summary>
        /// Tries to click on the search box in the embedded window
        /// </summary>
        private async Task<bool> TryClickSearchBox(IntPtr hwnd)
        {
            // 不同播放器的搜索框位置不同，这里采用一些估计的位置
            // 第一种情况: 搜索框通常在窗口上方的右侧
            WinAPI.RECT windowRect = new WinAPI.RECT();
            if (!WinAPI.GetWindowRect(hwnd, ref windowRect))
                return false;
                
            int windowWidth = windowRect.Right - windowRect.Left;
            int windowHeight = windowRect.Bottom - windowRect.Top;
            
            // 估计搜索框位置 (窗口右上角区域)
            int searchBoxX = windowWidth * 3 / 4; // 窗口宽度的3/4处
            int searchBoxY = windowHeight / 10;   // 窗口高度的1/10处
            
            // 将相对坐标转换为屏幕坐标
            WinAPI.POINT clientPoint = new WinAPI.POINT { X = searchBoxX, Y = searchBoxY };
            WinAPI.ClientToScreen(hwnd, ref clientPoint);
            
            // 模拟点击搜索框
            Debug.WriteLine($"尝试点击搜索框位置: ({clientPoint.X}, {clientPoint.Y})");
            
            // 使用WinAPI模拟鼠标点击
            WinAPI.INPUT[] inputs = new WinAPI.INPUT[3];
            
            // 鼠标移动
            inputs[0].type = WinAPI.INPUT_MOUSE;
            inputs[0].u.mi.dx = clientPoint.X * (65535 / WinAPI.GetSystemMetrics(WinAPI.SM_CXSCREEN));
            inputs[0].u.mi.dy = clientPoint.Y * (65535 / WinAPI.GetSystemMetrics(WinAPI.SM_CYSCREEN));
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = WinAPI.MOUSEEVENTF_MOVE | WinAPI.MOUSEEVENTF_ABSOLUTE;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = WinAPI.GetMessageExtraInfo();
            
            // 鼠标按下
            inputs[1].type = WinAPI.INPUT_MOUSE;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;
            inputs[1].u.mi.mouseData = 0;
            inputs[1].u.mi.dwFlags = WinAPI.MOUSEEVENTF_LEFTDOWN;
            inputs[1].u.mi.time = 0;
            inputs[1].u.mi.dwExtraInfo = WinAPI.GetMessageExtraInfo();
            
            // 鼠标抬起
            inputs[2].type = WinAPI.INPUT_MOUSE;
            inputs[2].u.mi.dx = 0;
            inputs[2].u.mi.dy = 0;
            inputs[2].u.mi.mouseData = 0;
            inputs[2].u.mi.dwFlags = WinAPI.MOUSEEVENTF_LEFTUP;
            inputs[2].u.mi.time = 0;
            inputs[2].u.mi.dwExtraInfo = WinAPI.GetMessageExtraInfo();
            
            uint result = WinAPI.SendInput(3, inputs, Marshal.SizeOf(typeof(WinAPI.INPUT)));
            Debug.WriteLine($"SendInput结果: {result}");
            
            // 等待一会儿，让搜索框获取焦点
            await Task.Delay(500);
            
            // 点击成功
            return result > 0;
        }
        
        /// <summary>
        /// Simulates typing text character by character
        /// </summary>
        private async Task SimulateTypingText(string text)
        {
            foreach (char c in text)
            {
                // 将字符转换为虚拟键码
                ushort vk = GetVirtualKeyCode(c);
                if (vk != 0)
                {
                    // 检查是否需要按Shift键(大写字母或特殊符号)
                    bool needShift = char.IsUpper(c) || IsShiftCharacter(c);
                    
                    if (needShift)
                    {
                        await WinAPI.SimulateKeyPressWithModifiers(new List<ushort> { WinAPI.VK_SHIFT }, vk);
                    }
                    else
                    {
                        // 普通按键
                        WinAPI.INPUT[] inputs = new WinAPI.INPUT[2];
                        
                        // 按键按下
                        inputs[0].type = WinAPI.INPUT_KEYBOARD;
                        inputs[0].u.ki.wVk = vk;
                        inputs[0].u.ki.dwFlags = WinAPI.KEYEVENTF_KEYDOWN;
                        inputs[0].u.ki.time = 0;
                        inputs[0].u.ki.dwExtraInfo = WinAPI.GetMessageExtraInfo();
                        
                        // 按键抬起
                        inputs[1].type = WinAPI.INPUT_KEYBOARD;
                        inputs[1].u.ki.wVk = vk;
                        inputs[1].u.ki.dwFlags = WinAPI.KEYEVENTF_KEYUP;
                        inputs[1].u.ki.time = 0;
                        inputs[1].u.ki.dwExtraInfo = WinAPI.GetMessageExtraInfo();
                        
                        WinAPI.SendInput(2, inputs, Marshal.SizeOf(typeof(WinAPI.INPUT)));
                    }
                    
                    // 输入间隔，让输入看起来更自然
                    await Task.Delay(30);
                }
            }
        }
        
        /// <summary>
        /// Gets the virtual key code for a character
        /// </summary>
        private ushort GetVirtualKeyCode(char c)
        {
            // 数字键
            if (c >= '0' && c <= '9')
                return (ushort)(c - '0' + 0x30);
                
            // 字母键
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                return (ushort)(char.ToUpper(c));
                
            // 空格
            if (c == ' ')
                return WinAPI.VK_SPACE;
                
            // 其他常见符号，这里只列出一部分，可以根据需要扩展
            switch (c)
            {
                case '.': return (ushort)190; // VK_OEM_PERIOD
                case ',': return (ushort)188; // VK_OEM_COMMA
                case '-': return (ushort)189; // VK_OEM_MINUS
                case '+': return (ushort)187; // VK_OEM_PLUS
                case '=': return (ushort)187; // VK_OEM_PLUS (不按Shift)
                case ';': return (ushort)186; // VK_OEM_1
                case ':': return (ushort)186; // VK_OEM_1 (按Shift)
                case '/': return (ushort)191; // VK_OEM_2
                case '?': return (ushort)191; // VK_OEM_2 (按Shift)
                case '\'': return (ushort)222; // VK_OEM_7
                case '"': return (ushort)222; // VK_OEM_7 (按Shift)
                case '[': return (ushort)219; // VK_OEM_4
                case ']': return (ushort)221; // VK_OEM_6
                case '\\': return (ushort)220; // VK_OEM_5
                case '!': return (ushort)0x31; // 1 (按Shift)
                case '@': return (ushort)0x32; // 2 (按Shift)
                case '#': return (ushort)0x33; // 3 (按Shift)
                case '$': return (ushort)0x34; // 4 (按Shift)
                case '%': return (ushort)0x35; // 5 (按Shift)
                case '^': return (ushort)0x36; // 6 (按Shift)
                case '&': return (ushort)0x37; // 7 (按Shift)
                case '*': return (ushort)0x38; // 8 (按Shift)
                case '(': return (ushort)0x39; // 9 (按Shift)
                case ')': return (ushort)0x30; // 0 (按Shift)
                default: return 0; // 未知字符
            }
        }
        
        /// <summary>
        /// Determines if a character requires the Shift key
        /// </summary>
        private bool IsShiftCharacter(char c)
        {
            string shiftChars = "~!@#$%^&*()_+{}|:\"<>?";
            return shiftChars.Contains(c);
        }
    }
}