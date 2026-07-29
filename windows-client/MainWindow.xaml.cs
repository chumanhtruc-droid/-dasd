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

        private bool isExpanded = false;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
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
            base.OnClosed(e);
        }

        private void CleanupMonitor()
        {
            if (monitorProcess != null && !monitorProcess.HasExited)
            {
                try { monitorProcess.Kill(); monitorProcess.WaitForExit(1000); } catch { }
            }
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
                    Dispatcher.Invoke(() => CollapseWindow());
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
                                                    System.IO.Compression.ZipFileExtensions.ExtractToDirectory(archive, tempPath, true);
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
