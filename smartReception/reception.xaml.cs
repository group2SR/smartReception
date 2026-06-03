using smartReception;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace smartReception
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// public class ReceptionistLoginResult
    public class ReceptionistLoginResult
    {
        public int user_id { get; set; }
        public string username { get; set; }
        public bool is_blocked { get; set; }
    } // Closes ReceptionistLoginResult
    public sealed partial class reception : Page
    {
    private static readonly HttpClient _http = new HttpClient();
    public reception()
        {
            this.InitializeComponent();
        // Clear out any placeholder text on page load
        comment.Text = "";
    }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage),null, new SuppressNavigationTransitionInfo());
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

        // Basic Validation
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            comment.Text = "Please enter both name and password.";
            return;
        }

        comment.Text = "Checking credentials...";

        try
        {
            // Setup standard authentication headers used by your team
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            // Hash the text using SHA-256 to match the database records
            string hashedPassword = HashLoginPassword(password);

            // Build query path: look for a match on username, password hash, and receptionist role (role_id = 2)
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

            // Read results array
            var records = JsonSerializer.Deserialize<List<ReceptionistLoginResult>>(json);

            if (records != null && records.Count > 0)
            {
                var user = records[0];

                // Enforce administration block actions
                if (user.is_blocked)
                {
                    comment.Text = "Access Denied. Account is blocked.";
                    return;
                }

                comment.Text = "Login successful!";

                // Navigate to your actual Receptionist landing workspace page
                // (Make sure to change 'ReceptionistDashboardPage' to match your actual page class name)
                Frame.Navigate(typeof(MasterDashboard));
            }
            else
            {
                comment.Text = "Invalid credentials. Try again.";
            }
        }
        catch (Exception ex)
        {
            comment.Text = "Connection error. Please try again later.";
            System.Diagnostics.Debug.WriteLine($"Login Error Details: {ex.Message}");
        }
    }

    // SHA-256 matching method
    private static string HashLoginPassword(string password)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

}
        
 }

