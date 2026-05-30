using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace smartReception
{
    public sealed partial class Settings : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        // role_id = 1 is Administrator
        private const int AdminRoleId = 1;
        private int _adminUserId = -1;

        // Named controls we need — add x:Name to your XAML to match these
        // TxtAdminFullName, TxtAdminUsername, TxtAdminEmail
        // PwdCurrent, PwdNew, PwdConfirm

        public Settings()
        {
            this.InitializeComponent();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadAdminAsync();
        }

        // ── LOAD admin account ─────────────────────────────────────────────

        private async Task LoadAdminAsync()
        {
            try
            {
                string url = App.SupabaseUrl +
                             "/rest/v1/system_users" +
                             "?select=user_id,first_name,last_name,username" +
                             "&role_id=eq." + AdminRoleId +
                             "&order=user_id.asc&limit=1";

                HttpResponseMessage response = await _http.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        JsonElement row = root[0];
                        _adminUserId = GetInt(row, "user_id");

                        string firstName = GetStr(row, "first_name") ?? "";
                        string lastName = GetStr(row, "last_name") ?? "";
                        string username = GetStr(row, "username") ?? "";

                        TxtAdminFullName.Text = (firstName + " " + lastName).Trim();
                        TxtAdminUsername.Text = username;
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Failed to load admin account: " + ex.Message);
            }
        }

        // ── SAVE admin account details ─────────────────────────────────────

        private async void BtnSaveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_adminUserId < 0)
            {
                await ShowMessageAsync("Admin account not loaded yet.");
                return;
            }

            string fullName = TxtAdminFullName.Text.Trim();
            string username = TxtAdminUsername.Text.Trim();

            if (string.IsNullOrEmpty(fullName))
            {
                await ShowMessageAsync("Full Name is required.");
                return;
            }
            if (string.IsNullOrEmpty(username))
            {
                await ShowMessageAsync("Username is required.");
                return;
            }

            // Split full name into first / last
            string[] parts = fullName.Split(new[] { ' ' }, 2);
            string firstName = parts[0];
            string lastName = parts.Length > 1 ? parts[1] : "";

            try
            {
                await PatchAsync(
                    "/rest/v1/system_users?user_id=eq." + _adminUserId,
                    new Dictionary<string, object>
                    {
                        ["first_name"] = firstName,
                        ["last_name"] = lastName,
                        ["username"] = username
                    });

                await ShowMessageAsync("Account details saved successfully.");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Save failed: " + ex.Message);
            }
        }

        // ── CHANGE PASSWORD ────────────────────────────────────────────────

        private async void BtnUpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            if (_adminUserId < 0)
            {
                await ShowMessageAsync("Admin account not loaded yet.");
                return;
            }

            string current = PwdCurrent.Password;
            string newPwd = PwdNew.Password;
            string confirm = PwdConfirm.Password;

            if (string.IsNullOrEmpty(current))
            {
                await ShowMessageAsync("Please enter your current password.");
                return;
            }
            if (string.IsNullOrEmpty(newPwd))
            {
                await ShowMessageAsync("Please enter a new password.");
                return;
            }
            if (newPwd != confirm)
            {
                await ShowMessageAsync("New password and confirmation do not match.");
                return;
            }
            if (newPwd.Length < 6)
            {
                await ShowMessageAsync("New password must be at least 6 characters.");
                return;
            }

            try
            {
                // Verify current password against DB
                string url = App.SupabaseUrl +
                             "/rest/v1/system_users" +
                             "?select=password_hash" +
                             "&user_id=eq." + _adminUserId;

                HttpResponseMessage response = await _http.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                string storedHash = "";
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                        storedHash = GetStr(root[0], "password_hash") ?? "";
                }

                if (HashPassword(current) != storedHash)
                {
                    await ShowMessageAsync("Current password is incorrect.");
                    return;
                }

                // Save new hashed password
                await PatchAsync(
                    "/rest/v1/system_users?user_id=eq." + _adminUserId,
                    new Dictionary<string, object>
                    {
                        ["password_hash"] = HashPassword(newPwd)
                    });

                PwdCurrent.Password = "";
                PwdNew.Password = "";
                PwdConfirm.Password = "";

                await ShowMessageAsync("Password updated successfully.");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Password update failed: " + ex.Message);
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────

        private static string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private async Task PatchAsync(string path, Dictionary<string, object> payload)
        {
            string body = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), App.SupabaseUrl + path)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Prefer", "return=minimal");
            HttpResponseMessage response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception(await response.Content.ReadAsStringAsync());
        }

        private async Task ShowMessageAsync(string message)
        {
            await new MessageDialog(message, "Smart Reception").ShowAsync();
        }

        private static string GetStr(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out JsonElement v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() : null;
        }

        private static int GetInt(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : 0;
        }

        // ── NAVIGATION ─────────────────────────────────────────────────────

        private void logoutbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }

        private void settingsbackbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(UsersReceptionist));
        }
    }
}