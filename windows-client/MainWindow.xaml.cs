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

namespace WindowsClient
{
    public partial class MainWindow : Window
    {
        private SocketIOClient.SocketIO client;
        private string currentKey = "";
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Set window position to bottom right corner
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
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
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Lỗi kết nối hoặc Key không hợp lệ.");
                });
            });

            client.On("new_message", response =>
            {
                var data = response.GetValue<JsonObject>();
                if (data["sender"]?.ToString() != "USER1")
                {
                    // Show small notification on Windows
                    // In full implementation, use Windows Community Toolkit ToastNotificationManager
                    Dispatcher.Invoke(() =>
                    {
                        // Temporary visual cue
                        StatusDot.Fill = new SolidColorBrush(Colors.Yellow);
                        Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() => StatusDot.Fill = new SolidColorBrush(Colors.Green)));
                    });
                }
            });

            await client.ConnectAsync();
        }

        private async void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                await client.DisconnectAsync();
                client.Dispose();
                client = null;
            }

            LoginPanel.Visibility = Visibility.Visible;
            ActionPanel.Visibility = Visibility.Collapsed;
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.Connected) return;
            
            BtnCapture.IsEnabled = false;

            try
            {
                // Hide window temporarily
                this.Opacity = 0;
                await Task.Delay(200);

                // Capture screen
                Bitmap bitmap = new Bitmap((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }
                
                this.Opacity = 1;

                // Compress and Upload to API
                using (MemoryStream ms = new MemoryStream())
                {
                    // Save as jpeg to reduce size
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
                                // Send socket message
                                await client.EmitAsync("send_message", new { content = url, type = "IMAGE" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi chụp ảnh: " + ex.Message);
                this.Opacity = 1;
            }
            finally
            {
                BtnCapture.IsEnabled = true;
            }
        }
    }
}
