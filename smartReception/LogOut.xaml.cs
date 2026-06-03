using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace smartReception
{
    public sealed partial class LogOut : Page
    {
        public LogOut()
        {
            this.InitializeComponent();
        }

        private void cancelbtn_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private async void logoutbtn_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog logoutDialog = new ContentDialog
            {
                Title = "Logout",
                Content = "Are you sure you want to logout?",
                PrimaryButtonText = "Logout",
                CloseButtonText = "Cancel"
            };

            ContentDialogResult result = await logoutDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // ── Write Logout log before leaving ────────────────────────
                if (App.CurrentUserId > 0)
                {
                    await InsertSystemLogAsync(
                        App.CurrentUserId,
                        "Logout",
                        App.CurrentUserFullName + " signed out");
                }

                // ── Clear session ──────────────────────────────────────────
                App.CurrentUserId = 0;
                App.CurrentUserFullName = string.Empty;

                Frame.Navigate(typeof(MainPage));
            }
        }

        // ── Log writer ─────────────────────────────────────────────────────
        private static async Task InsertSystemLogAsync(
            int userId,
            string activity,
            string description)
        {
            try
            {
                await App.EnsureSupabaseInitializedAsync();

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);

                    string url = App.SupabaseUrl + "/rest/v1/system_logs";
                    var body = JsonSerializer.Serialize(new
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
            }
            catch
            {
                // Never crash the app over a log write failure
            }
        }
    }
}