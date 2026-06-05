using smartReception;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace smartReception
{
    public class ReceptionistLoginResult
    {
        public int user_id { get; set; }
        public string username { get; set; }
        public bool is_blocked { get; set; }
    }

    public sealed partial class reception : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        public reception()
        {
            this.InitializeComponent();
            comment.Text = "";
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).Opacity = 0.7;
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).Opacity = 1;
        }

        private async void ReceptionistLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = Txtusername.Text.Trim();
            string password = TxtPassword.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                comment.Text = "Please enter both username and password.";
                return;
            }

            comment.Text = "Checking credentials...";

            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
                _http.DefaultRequestHeaders.Add("Accept", "application/json");

                string hashedPassword = HashLoginPassword(password);

                string queryUrl = App.SupabaseUrl +
                                  "/rest/v1/system_users" +
                                  $"?username=eq.{Uri.EscapeDataString(username)}" +
                                  $"&password_hash=eq.{hashedPassword}" +
                                  "&role_id=eq.2" +
                                  "&select=user_id,username,is_blocked";

                HttpResponseMessage response = await _http.GetAsync(queryUrl);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Server communication failure.");

                var records = JsonSerializer.Deserialize<List<ReceptionistLoginResult>>(json);

                if (records != null && records.Count > 0)
                {
                    var user = records[0];

                    if (user.is_blocked)
                    {
                        comment.Text = "Access Denied. Account is blocked.";
                        return;
                    }

                    // ── Store session ──────────────────────────────────────
                    App.CurrentUserId = user.user_id;
                    App.CurrentUserFullName = user.username;
                    App.CurrentUserRole = "receptionist"; // ← SET ROLE

                    comment.Text = "Login successful!";

                    // Receptionists land on entry — never MasterDashboard
                    Frame.Navigate(typeof(entry));
                }
                else
                {
                    comment.Text = "Invalid credentials. Try again.";
                }
            }
            catch (Exception ex)
            {
                comment.Text = "Connection error. Please try again later.";
                System.Diagnostics.Debug.WriteLine($"Login Error: {ex.Message}");
            }
        }

        private static string HashLoginPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}