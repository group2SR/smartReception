using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    // ── DISPLAY MODEL ──────────────────────────────────────────────────────

    public class SystemUserDisplay
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public bool IsBlocked { get; set; }
        public string CreatedDate { get; set; }

        // Computed display helpers
        public string StatusLabel => IsBlocked ? "Blocked" : "Active";
        public string BlockLabel => IsBlocked ? "Unblock" : "Block";
        public string StatusBadgeBg => IsBlocked ? "#FEF2F2" : "#E6F4EA";
        public string StatusBadgeBorder => IsBlocked ? "#DC2626" : "#34A853";
        public string StatusBadgeText => IsBlocked ? "#DC2626" : "#137333";
    }

    // ── PAGE ───────────────────────────────────────────────────────────────

    public sealed partial class UsersReceptionist : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        private readonly ObservableCollection<SystemUserDisplay> _allUsers =
            new ObservableCollection<SystemUserDisplay>();

        private readonly ObservableCollection<SystemUserDisplay> _filtered =
            new ObservableCollection<SystemUserDisplay>();

        // role_id for Receptionist in your roles table
        private const int ReceptionistRoleId = 2;

        private int? _editingUserId = null;

        public UsersReceptionist()
        {
            this.InitializeComponent();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            UsersListView.ItemsSource = _filtered;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadUsersAsync();
        }

        // ── LOAD ───────────────────────────────────────────────────────────

        private async Task LoadUsersAsync()
        {
            try
            {
                // Fetch only Receptionist accounts (role_id = 2)
                string url = App.SupabaseUrl +
                             "/rest/v1/system_users" +
                             "?select=user_id,first_name,last_name,username,is_blocked,created_date," +
                             "roles!system_users_role_id_fkey(role_name)" +
                             "&role_id=eq." + ReceptionistRoleId +
                             "&order=user_id.desc";

                HttpResponseMessage response = await _http.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                _allUsers.Clear();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    foreach (JsonElement row in doc.RootElement.EnumerateArray())
                    {
                        // Unwrap roles join
                        string roleName = "Receptionist";
                        if (row.TryGetProperty("roles", out JsonElement roles) &&
                            roles.ValueKind != JsonValueKind.Null)
                        {
                            JsonElement r = roles.ValueKind == JsonValueKind.Array
                                ? (roles.GetArrayLength() > 0 ? roles[0] : default)
                                : roles;
                            roleName = GetStr(r, "role_name") ?? "Receptionist";
                        }

                        bool isBlocked = false;
                        if (row.TryGetProperty("is_blocked", out JsonElement blockedEl) &&
                            blockedEl.ValueKind == JsonValueKind.True)
                            isBlocked = true;

                        string rawDate = GetStr(row, "created_date") ?? "";
                        string formattedDate = DateTime.TryParse(rawDate, out DateTime dt)
                            ? dt.ToString("dd/MM/yyyy hh:mm tt")
                            : rawDate;

                        _allUsers.Add(new SystemUserDisplay
                        {
                            UserId = GetInt(row, "user_id"),
                            FullName = (GetStr(row, "first_name") + " " + GetStr(row, "last_name")).Trim(),
                            Username = GetStr(row, "username") ?? "",
                            RoleName = roleName,
                            IsBlocked = isBlocked,
                            CreatedDate = formattedDate
                        });
                    }
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Load error: " + ex.Message);
            }
        }

        // ── FILTER ─────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = TxtSearch?.Text?.Trim().ToLower() ?? "";
            _filtered.Clear();

            foreach (var u in _allUsers)
            {
                if (string.IsNullOrEmpty(q) ||
                    u.FullName.ToLower().Contains(q) ||
                    u.Username.ToLower().Contains(q))
                {
                    _filtered.Add(u);
                }
            }

            TxtEntryCount.Text = "Showing " + _filtered.Count + " of " + _allUsers.Count + " entries";
        }

        // ── SAVE (INSERT or UPDATE) ────────────────────────────────────────

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string firstName = TxtFirstName.Text.Trim();
            string lastName = TxtLastName.Text.Trim();
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password.Trim();
            bool isBlocked = (CboStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "true";

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
            if (!_editingUserId.HasValue && string.IsNullOrEmpty(password))
            {
                await ShowMessageAsync("Password is required for new accounts.");
                return;
            }

            try
            {
                if (_editingUserId.HasValue)
                {
                    // UPDATE — only update password if a new one was entered
                    var payload = new Dictionary<string, object>
                    {
                        ["first_name"] = firstName,
                        ["last_name"] = lastName,
                        ["username"] = username,
                        ["is_blocked"] = isBlocked
                    };
                    if (!string.IsNullOrEmpty(password))
                        payload["password_hash"] = HashPassword(password);

                    await PatchAsync(
                        "/rest/v1/system_users?user_id=eq." + _editingUserId.Value,
                        payload);

                    await ShowMessageAsync("Receptionist updated successfully.");
                }
                else
                {
                    // INSERT
                    var payload = new Dictionary<string, object>
                    {
                        ["first_name"] = firstName,
                        ["last_name"] = lastName,
                        ["username"] = username,
                        ["password_hash"] = HashPassword(password),
                        ["role_id"] = ReceptionistRoleId,
                        ["is_blocked"] = isBlocked
                    };

                    await PostAsync("/rest/v1/system_users", payload);
                    await ShowMessageAsync("Receptionist account created successfully.");
                }

                ClearForm();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Save failed: " + ex.Message);
            }
        }

        // ── EDIT ───────────────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                SystemUserDisplay user = null;
                foreach (var u in _allUsers)
                    if (u.UserId == id) { user = u; break; }
                if (user == null) return;

                string[] parts = user.FullName.Split(new[] { ' ' }, 2);
                TxtFirstName.Text = parts[0];
                TxtLastName.Text = parts.Length > 1 ? parts[1] : "";
                TxtUsername.Text = user.Username;
                TxtPassword.Password = "";   // leave blank — only update if re-entered
                CboStatus.SelectedIndex = user.IsBlocked ? 1 : 0;

                TxtFormTitle.Text = "Edit Receptionist";
                BtnSave.Content = "Update";
                _editingUserId = id;
            }
        }

        // ── BLOCK / UNBLOCK ────────────────────────────────────────────────

        private async void BtnBlock_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                SystemUserDisplay user = null;
                foreach (var u in _allUsers)
                    if (u.UserId == id) { user = u; break; }
                if (user == null) return;

                bool newBlockedState = !user.IsBlocked;
                string action = newBlockedState ? "block" : "unblock";

                try
                {
                    await PatchAsync(
                        "/rest/v1/system_users?user_id=eq." + id,
                        new Dictionary<string, object> { ["is_blocked"] = newBlockedState });

                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    string message = ex.Message;

                    if (message.Contains("23505") || message.Contains("username_key"))
                        message = "That username is already taken. Please choose a different one.";

                    await ShowMessageAsync("Save failed: " + message);
                }
            }
        }

        // ── DELETE ─────────────────────────────────────────────────────────

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                try
                {
                    string url = App.SupabaseUrl + "/rest/v1/system_users?user_id=eq." + id;
                    HttpResponseMessage response = await _http.DeleteAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        throw new Exception(err);
                    }

                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Delete failed: " + ex.Message);
                }
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────

        private void ClearForm()
        {
            TxtFirstName.Text = "";
            TxtLastName.Text = "";
            TxtUsername.Text = "";
            TxtPassword.Password = "";
            CboStatus.SelectedIndex = 0;
            TxtFormTitle.Text = "Add Receptionist";
            BtnSave.Content = "Save";
            _editingUserId = null;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        // SHA-256 hash — matches what you should verify on login
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

        private async Task PostAsync(string path, Dictionary<string, object> payload)
        {
            string body = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, App.SupabaseUrl + path)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Prefer", "return=minimal");
            HttpResponseMessage response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new Exception(await response.Content.ReadAsStringAsync());
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

        private void Settingspagbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Settings));
        }

        private void userreceptionsbackbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SystemLogs));
        }

        private void NavDashboard_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(dashboard)) return;
            Frame.Navigate(typeof(dashboard));
        }

        private void NavAccessControl_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Access_Control)) return;
            Frame.Navigate(typeof(Access_Control));
        }

        private void NavReports_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Reports)) return;
            Frame.Navigate(typeof(Reports));
        }

        private void NavLogs_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(SystemLogs)) return;
            Frame.Navigate(typeof(SystemLogs));
        }

        private void NavReceptionists_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(UsersReceptionist)) return;
            Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavSettings_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (this.GetType() == typeof(Settings)) return;
            Frame.Navigate(typeof(Settings));
        }

        private void NavLogout_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }
    }
}