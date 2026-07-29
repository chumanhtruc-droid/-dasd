using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SocketIOClient;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Controls;

namespace WindowsClient
{
    public partial class MainWindow : Window
    {
        private SocketIOClient.SocketIO client;
        private string currentKey = "";
        private System.Diagnostics.Process monitorProcess = null;
        
        // P/Invoke for Global Hotkey
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int HOTKEY_CAPTURE = 9000;
        private const int HOTKEY_HIDE = 9001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_X = 0x58;
        private const uint VK_Z = 0x5A;

        // --- Ghost Typing ---
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_OEM_6 = 0xDD; // ']' key
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private static readonly UIntPtr INJECTED_FLAG = new UIntPtr(0x12345678);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool isGhostTyping = false;
        private string pasteBuffer = "";
        private int pasteIndex = 0;

        private bool isExpanded = false;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
            
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);
            
            RegisterHotKey(handle, HOTKEY_CAPTURE, MOD_CONTROL, VK_X);
            RegisterHotKey(handle, HOTKEY_HIDE, MOD_CONTROL, VK_Z);
        }

        protected override void OnClosed(EventArgs e)
        {
            CleanupMonitor();

            IntPtr handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_CAPTURE);
            UnregisterHotKey(handle, HOTKEY_HIDE);
            UnhookWindowsHookEx(_hookID);
            base.OnClosed(e);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                KBDLLHOOKSTRUCT kbStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Bỏ qua phím do phần mềm tự sinh ra (injected)
                if (kbStruct.dwExtraInfo == INJECTED_FLAG || (kbStruct.flags & 0x10) != 0)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (vkCode == VK_OEM_6) // Phím ']'
                {
                    isGhostTyping = !isGhostTyping;
                    Dispatcher.Invoke(() => {
                        AddMessage(isGhostTyping ? "Hệ thống: Chế độ ma ĐÃ BẬT" : "Hệ thống: Chế độ ma ĐÃ TẮT");
                    });
                    return (IntPtr)1; // Chặn phím ']' không cho hiện ra
                }

                if (isGhostTyping)
                {
                    // Chặn phím bấm và xuất chữ từ pasteBuffer
                    if (!string.IsNullOrEmpty(pasteBuffer) && pasteIndex < pasteBuffer.Length)
                    {
                        char nextChar = pasteBuffer[pasteIndex];
                        pasteIndex++;
                        
                        // Tắt chế độ ma nếu hết chữ
                        if (pasteIndex >= pasteBuffer.Length)
                        {
                            isGhostTyping = false;
                            Dispatcher.Invoke(() => {
                                AddMessage("Hệ thống: Đã gõ xong, Chế độ ma ĐÃ TẮT");
                            });
                        }
                        
                        // Inject ký tự qua SendInput
                        SendUnicodeChar(nextChar);
                    }
                    return (IntPtr)1; // Luôn chặn phím gõ gốc
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void SendUnicodeChar(char c)
        {
            INPUT[] inputs = new INPUT[2];
            
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = 0;
            inputs[0].u.ki.wScan = (ushort)c;
            inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = INJECTED_FLAG;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = 0;
            inputs[1].u.ki.wScan = (ushort)c;
            inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].u.ki.time = 0;
            inputs[1].u.ki.dwExtraInfo = INJECTED_FLAG;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void CleanupMonitor()
        {
            try
            {
                if (monitorProcess != null && !monitorProcess.HasExited)
                {
                    monitorProcess.Kill();
                    monitorProcess.WaitForExit(1000);
                }
            }
            catch { }

            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("WinStatMonitor");
                foreach (var p in processes)
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
            try
            {
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinStatCore");
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }
            } catch { }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_CAPTURE)
                {
                    if (client != null && client.Connected)
                    {
                        Dispatcher.Invoke(() => Capture_Click(null, null));
                    }
                    handled = true;
                }
                else if (wParam.ToInt32() == HOTKEY_HIDE)
                {
                    Dispatcher.Invoke(() => 
                    {
                        if (this.Visibility == Visibility.Visible)
                        {
                            this.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            this.Visibility = Visibility.Visible;
                            this.Activate();
                        }
                    });
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            currentKey = KeyInput.Text.Trim();
            if (string.IsNullOrEmpty(currentKey)) return;

            MessageList.Items.Clear();

            client = new SocketIOClient.SocketIO("https://dasd-1z1t.onrender.com");

            client.OnConnected += async (sender, e) =>
            {
                await client.EmitAsync("join_room", new { keyString = currentKey, sender = "USER1" });
            };

            client.On("joined", response =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoginPanel.Visibility = Visibility.Collapsed;
                    ActionPanel.Visibility = Visibility.Visible;
                    StatusDot.Fill = new SolidColorBrush(Colors.Green);
                    
                    try
                    {
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinStatCore");
                        string exePath = System.IO.Path.Combine(tempPath, "WinStatMonitor.exe");
                        if (!System.IO.File.Exists(exePath))
                        {
                            if (!System.IO.Directory.Exists(tempPath))
                            {
                                System.IO.Directory.CreateDirectory(tempPath);
                            }
                            
                            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WindowsClient.Release.dat"))
                            {
                                if (stream != null)
                                {
                                    using (var ms = new System.IO.MemoryStream())
                                    {
                                        stream.CopyTo(ms);
                                        byte[] encryptedData = ms.ToArray();
                                        byte[] iv = new byte[16];
                                        System.Array.Copy(encryptedData, 0, iv, 0, 16);
                                        
                                        byte[] cipherText = new byte[encryptedData.Length - 16];
                                        System.Array.Copy(encryptedData, 16, cipherText, 0, cipherText.Length);
                                        
                                        byte[] key = System.Text.Encoding.UTF8.GetBytes("dierB7/jHhmwY/Q4BPmAiCngMcHXoz00");
                                        
                                        using (var aes = System.Security.Cryptography.Aes.Create())
                                        {
                                            aes.Key = key;
                                            aes.IV = iv;
                                            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                                            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                                            
                                            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                                            using (var msDecrypt = new System.IO.MemoryStream())
                                            {
                                                using (var cryptoStream = new System.Security.Cryptography.CryptoStream(new System.IO.MemoryStream(cipherText), decryptor, System.Security.Cryptography.CryptoStreamMode.Read))
                                                {
                                                    cryptoStream.CopyTo(msDecrypt);
                                                }
                                                msDecrypt.Position = 0;
                                                using (var archive = new System.IO.Compression.ZipArchive(msDecrypt))
                                                {
                                                    foreach (var entry in archive.Entries)
                                                    {
                                                        string destinationPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(tempPath, entry.FullName));
                                                        if (destinationPath.StartsWith(tempPath, System.StringComparison.Ordinal))
                                                        {
                                                            try
                                                            {
                                                                if (string.IsNullOrEmpty(entry.Name)) {
                                                                    System.IO.Directory.CreateDirectory(destinationPath);
                                                                } else {
                                                                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath));
                                                                    System.IO.Compression.ZipFileExtensions.ExtractToFile(entry, destinationPath, true);
                                                                }
                                                            }
                                                            catch { }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (System.IO.File.Exists(exePath))
                        {
                            if (monitorProcess == null || monitorProcess.HasExited)
                            {
                                monitorProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() {
                                    FileName = exePath,
                                    WorkingDirectory = tempPath
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lỗi khi mở WinStatMonitor: " + ex.Message);
                    }
                });
            });

            client.On("user_status", response =>
            {
                try
                {
                    var data = response.GetValue<JsonObject>();
                    var status = data["status"]?.ToString();
                    var senderMsg = data["sender"]?.ToString();
                    
                    if (senderMsg != "USER1")
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusDot.Fill = new SolidColorBrush(status == "ONLINE" ? Colors.Green : Colors.Red);
                        });
                    }
                } catch { }
            });

            client.On("error", response =>
            {
                Dispatcher.Invoke(() => MessageBox.Show("Lỗi kết nối hoặc Key không hợp lệ."));
            });

            client.On("new_message", response =>
            {
                var data = response.GetValue<JsonObject>();
                if (data["sender"]?.ToString() != "USER1")
                {
                    var content = data["content"]?.ToString();
                    var type = data["type"]?.ToString();
                    Dispatcher.Invoke(() =>
                    {
                        if (type == "TEXT")
                        {
                            AddMessage("Nhà: " + content);
                            ExpandWindow();
                        }
                        else if (type == "PASTE_TEXT")
                        {
                            pasteBuffer = content ?? "";
                            pasteIndex = 0;
                            AddMessage("Hệ thống: Đã nhận nội dung Past. Bấm phím ']' để gõ ẩn.");
                        }
                    });
                }
            });

            await client.ConnectAsync();
        }
        
        private void AddMessage(string text)
        {
            MessageList.Items.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            MessageList.SelectedIndex = MessageList.Items.Count - 1;
            MessageList.ScrollIntoView(MessageList.SelectedItem);
        }

        private void ExpandWindow()
        {
            if (isExpanded) return;
            isExpanded = true;
            this.Width = 200;
            this.Height = 85;
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
            ChatPanel.Visibility = Visibility.Visible;
        }

        private void CollapseWindow()
        {
            if (!isExpanded) return;
            isExpanded = false;
            ChatPanel.Visibility = Visibility.Collapsed;
            this.Width = 140;
            this.Height = 40;
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Oem3 || e.Key == Key.OemTilde) // Backtick ` key
            {
                ExpandWindow();
                ChatInput.Visibility = Visibility.Visible;
                ChatInput.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CollapseWindow();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (MessageList.SelectedIndex > 0)
                    MessageList.SelectedIndex--;
                MessageList.ScrollIntoView(MessageList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (MessageList.SelectedIndex < MessageList.Items.Count - 1)
                    MessageList.SelectedIndex++;
                MessageList.ScrollIntoView(MessageList.SelectedItem);
                e.Handled = true;
            }
        }

        private async void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string text = ChatInput.Text.Trim();
                if (!string.IsNullOrEmpty(text) && client != null && client.Connected)
                {
                    await client.EmitAsync("send_message", new { content = text, type = "TEXT" });
                    AddMessage("Bạn: " + text);
                    ChatInput.Text = "";
                    ChatInput.Visibility = Visibility.Collapsed;
                }
                e.Handled = true;
            }
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            CleanupMonitor();

            if (client != null)
            {
                await client.DisconnectAsync();
                client.Dispose();
                client = null;
            }

            CollapseWindow();
            LoginPanel.Visibility = Visibility.Visible;
            ActionPanel.Visibility = Visibility.Collapsed;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            CleanupMonitor();
            Application.Current.Shutdown();
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.Connected) return;
            
            if (BtnCapture != null) BtnCapture.IsEnabled = false;

            try
            {
                this.Opacity = 0;
                await Task.Delay(200);

                Bitmap bitmap = new Bitmap((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }
                
                this.Opacity = 1;

                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Jpeg);
                    byte[] imageBytes = ms.ToArray();
                    
                    using (HttpClient httpClient = new HttpClient())
                    {
                        var form = new MultipartFormDataContent();
                        form.Add(new ByteArrayContent(imageBytes), "file", "screenshot.jpg");

                        var response = await httpClient.PostAsync("https://dasd-1z1t.onrender.com/api/upload", form);
                        if (response.IsSuccessStatusCode)
                        {
                            var resultStr = await response.Content.ReadAsStringAsync();
                            var result = JsonNode.Parse(resultStr);
                            var url = result["url"]?.ToString();
                            
                            if (!string.IsNullOrEmpty(url))
                            {
                                await client.EmitAsync("send_message", new { content = url, type = "IMAGE" });
                            }
                        }
                        else
                        {
                            var err = await response.Content.ReadAsStringAsync();
                            Dispatcher.Invoke(() => MessageBox.Show("Lỗi Upload: " + response.StatusCode + " - " + err));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Lỗi chụp ảnh: " + ex.Message));
                this.Opacity = 1;
            }
            finally
            {
                if (BtnCapture != null) BtnCapture.IsEnabled = true;
            }
        }
    }
}
