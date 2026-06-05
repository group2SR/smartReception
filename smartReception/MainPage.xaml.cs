using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
            Frame.Navigate(typeof(reception), null, new SuppressNavigationTransitionInfo());
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Hand, 0);

            if (sender is Grid card)
            {
                card.Opacity = 0.9;
                card.Background = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 241, 245, 249));
                card.BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 16, 185, 129));
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor =
                new CoreCursor(CoreCursorType.Arrow, 0);

            if (sender is Grid card)
            {
                card.Opacity = 1.0;
                card.Background = new SolidColorBrush(Windows.UI.Colors.White);
                card.BorderBrush = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 226, 232, 240));
            }
        }

        private static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
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
                await App.EnsureSupabaseInitializedAsync();

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Add("Accept", "application/json");

                    string hashedPwd = HashPassword(password);

                    string url = $"{App.SupabaseUrl}/rest/v1/system_users" +
                                 $"?username=eq.{username}" +
                                 $"&password_hash=eq.{hashedPwd}" +
                                 $"&role_id=eq.1" +
                                 $"&select=user_id,first_name,last_name,is_blocked";

                    var response = await http.GetAsync(url);
                    string json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        comment.Text = "Authentication error. Please try again.";
                        return;
                    }

                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.GetArrayLength() > 0)
                        {
                            var user = doc.RootElement[0];

                            if (user.TryGetProperty("is_blocked", out var blockedProp)
                                && blockedProp.GetBoolean())
                            {
                                comment.Text = "Your admin account is blocked.";
                                return;
                            }

                            // ── Store session ──────────────────────────────
                            int userId = user.GetProperty("user_id").GetInt32();
                            string firstName = user.GetProperty("first_name").GetString() ?? "";
                            string lastName = user.GetProperty("last_name").GetString() ?? "";
                            string fullName = (firstName + " " + lastName).Trim();

                            App.CurrentUserId = userId;
                            App.CurrentUserFullName = fullName;
                            App.CurrentUserRole = "admin"; // ← SET ROLE

                            // ── Write Login log ────────────────────────────
                            await InsertSystemLogAsync(http, userId, "Login", fullName + " signed in");

                            Frame.Navigate(typeof(dashboard));
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

        private static async Task InsertSystemLogAsync(
            HttpClient http, int userId, string activity, string description)
        {
            try
            {
                string url = App.SupabaseUrl + "/rest/v1/system_logs";
                var body = System.Text.Json.JsonSerializer.Serialize(new
                {
                    user_id = userId,
                    activity = activity,
                    description = description,
                    log_date = DateTimeOffset.UtcNow.ToString("O")
                });

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Prefer", "return=minimal");
                await http.SendAsync(request);
            }
            catch { }
        }
    }
}