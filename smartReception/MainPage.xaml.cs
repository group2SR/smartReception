using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace smartReception
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Suppress transition for a "snappy" professional feel
            Frame.Navigate(typeof(reception), null, new SuppressNavigationTransitionInfo());
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // 1. Change cursor to Hand (Fixes the need for the XAML Cursor attribute)
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 0);

            // 2. Visual feedback: Subtle Opacity and Background shift
            if (sender is Grid card)
            {
                card.Opacity = 0.9;
                card.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249));
                card.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // 1. Reset cursor to Arrow
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

            // 2. Reset visual feedback
            if (sender is Grid card)
            {
                card.Opacity = 1.0;
                card.Background = new SolidColorBrush(Windows.UI.Colors.White);
                card.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 226, 232, 240));
            }
        }

        private static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                var sb = new System.Text.StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private async void adminloginbtn_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                comment.Text = "Username and password are required.";
                return;
            }

            comment.Text = "Authenticating...";
            adminloginbtn.IsEnabled = false;

            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Add("Accept", "application/json");

                    string hashedPwd = HashPassword(password);
                    string url = $"{App.SupabaseUrl}/rest/v1/system_users?username=eq.{username}&password_hash=eq.{hashedPwd}&role_id=eq.1&select=is_blocked";

                    var response = await http.GetAsync(url);
                    string json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        comment.Text = "Authentication error. Please try again.";
                        return;
                    }

                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.GetArrayLength() > 0)
                        {
                            var user = doc.RootElement[0];
                            if (user.TryGetProperty("is_blocked", out var blockedProp) && blockedProp.GetBoolean())
                            {
                                comment.Text = "Your admin account is blocked.";
                            }
                            else
                            {
                                Frame.Navigate(typeof(dashboard));
                            }
                        }
                        else
                        {
                            comment.Text = "Invalid admin username or password.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                comment.Text = "Error: " + ex.Message;
            }
            finally
            {
                adminloginbtn.IsEnabled = true;
            }
        }
    }
}