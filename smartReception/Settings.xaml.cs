using System;
using System.Collections.Generic;
using System.Linq;
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

        // ── LOAD admin account + check cap ────────────────────────────────────

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

                // Check admin count and update the Create Admin card accordingly
                await RefreshAdminCapAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Failed to load admin account: " + ex.Message);
            }
        }

        // ── CHECK admin count and toggle Create Admin card ────────────────────

        private async Task RefreshAdminCapAsync()
        {
            try
            {
                string url = App.SupabaseUrl +
                             "/rest/v1/system_users" +
                             "?select=user_id" +
                             "&role_id=eq." + AdminRoleId;

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Prefer", "count=exact");
                HttpResponseMessage response = await _http.SendAsync(request);

                int adminCount = 0;

                // Supabase returns the count in the Content-Range header: "0-N/TOTAL"
                if (response.Headers.TryGetValues("Content-Range", out IEnumerable<string> values))
                {
                    string contentRange = values.FirstOrDefault() ?? "";
                    int slashIdx = contentRange.IndexOf('/');
                    if (slashIdx >= 0 && int.TryParse(contentRange.Substring(slashIdx + 1), out int total))
                        adminCount = total;
                }

                bool atCap = adminCount >= 2;

                AdminCapBanner.Visibility = atCap ? Visibility.Visible : Visibility.Collapsed;
                CreateAdminForm.Visibility = atCap ? Visibility.Collapsed : Visibility.Visible;
                BtnCreateAdmin.IsEnabled = !atCap;
            }
            catch
            {
                // Non-fatal — leave the form visible so the user can still attempt a create
            }
        }

        // ── SAVE admin account details ─────────────────────────────────────────

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

        // ── CHANGE PASSWORD ────────────────────────────────────────────────────

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

        // ── CREATE NEW ADMIN ACCOUNT ───────────────────────────────────────────

        private async void BtnCreateAdmin_Click(object sender, RoutedEventArgs e)
        {
            string firstName = TxtNewAdminFirstName.Text.Trim();
            string lastName = TxtNewAdminLastName.Text.Trim();
            string username = TxtNewAdminUsername.Text.Trim();
            string password = PwdNewAdmin.Password;

            // Basic validation
            if (string.IsNullOrEmpty(firstName))
            {
                await ShowMessageAsync("First Name is required.");
                return;
            }
            if (string.IsNullOrEmpty(username))
            {
                await ShowMessageAsync("Username is required.");
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                await ShowMessageAsync("Password is required.");
                return;
            }
            if (password.Length < 6)
            {
                await ShowMessageAsync("Password must be at least 6 characters.");
                return;
            }

            // Double-check the cap before inserting (server-side guard)
            try
            {
                string countUrl = App.SupabaseUrl +
                                  "/rest/v1/system_users" +
                                  "?select=user_id" +
                                  "&role_id=eq." + AdminRoleId;

                var countReq = new HttpRequestMessage(HttpMethod.Get, countUrl);
                countReq.Headers.Add("Prefer", "count=exact");
                HttpResponseMessage countResp = await _http.SendAsync(countReq);

                int adminCount = 0;
                if (countResp.Headers.TryGetValues("Content-Range", out IEnumerable<string> vals))
                {
                    string cr = vals.FirstOrDefault() ?? "";
                    int si = cr.IndexOf('/');
                    if (si >= 0 && int.TryParse(cr.Substring(si + 1), out int total))
                        adminCount = total;
                }

                if (adminCount >= 2)
                {
                    await ShowMessageAsync("Cannot create account: maximum of 2 admin accounts already reached.");
                    await RefreshAdminCapAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Could not verify admin count: " + ex.Message);
                return;
            }

            // Check the username is not already taken across ALL users (not just admins)
            try
            {
                string checkUrl = App.SupabaseUrl +
                                  "/rest/v1/system_users" +
                                  "?select=user_id" +
                                  "&username=eq." + Uri.EscapeDataString(username);

                HttpResponseMessage checkResp = await _http.GetAsync(checkUrl);
                string checkJson = await checkResp.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(checkJson))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                        doc.RootElement.GetArrayLength() > 0)
                    {
                        await ShowMessageAsync($"Username \"{username}\" is already taken. Please choose a different one.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Username check failed: " + ex.Message);
                return;
            }

            // Insert the new admin account
            try
            {
                string body = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["first_name"] = firstName,
                    ["last_name"] = lastName,
                    ["username"] = username,
                    ["password_hash"] = HashPassword(password),
                    ["role_id"] = AdminRoleId
                });

                var request = new HttpRequestMessage(HttpMethod.Post,
                    App.SupabaseUrl + "/rest/v1/system_users")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Prefer", "return=minimal");

                HttpResponseMessage response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    throw new Exception(await response.Content.ReadAsStringAsync());

                // Clear the create form
                TxtNewAdminFirstName.Text = "";
                TxtNewAdminLastName.Text = "";
                TxtNewAdminUsername.Text = "";
                PwdNewAdmin.Password = "";

                await ShowMessageAsync($"Admin account \"{username}\" created successfully. They can now sign in from the login page.");

                // Refresh cap banner — will hide the form if we're now at 2
                await RefreshAdminCapAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Failed to create admin account: " + ex.Message);
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────────

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

        // ── NAVIGATION ─────────────────────────────────────────────────────────

        private void logoutbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }

        private void settingsbackbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(dashboard)) return;
            Frame.Navigate(typeof(dashboard));
        }

        private void NavAccessControl_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Access_Control)) return;
            Frame.Navigate(typeof(Access_Control));
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Reports)) return;
            Frame.Navigate(typeof(Reports));
        }

        private void NavLogs_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(SystemLogs)) return;
            Frame.Navigate(typeof(SystemLogs));
        }

        private void NavReceptionists_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(UsersReceptionist)) return;
            Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Settings)) return;
            Frame.Navigate(typeof(Settings));
        }

        private void NavLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }
    }
}