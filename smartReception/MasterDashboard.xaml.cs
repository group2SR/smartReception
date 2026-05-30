using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace smartReception
{
    public sealed partial class MasterDashboard : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        private List<ActiveClientDisplay> _cachedActiveClients = new List<ActiveClientDisplay>();
        private List<VisitorLogDisplay> _cachedAllLogs = new List<VisitorLogDisplay>();
        private List<DeletedClientDisplay> _cachedDeletedLogs = new List<DeletedClientDisplay>();

        public MasterDashboard()
        {
            this.InitializeComponent();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            this.Loaded += async (s, e) => await FetchLatestDatabaseLogsAsync();
        }

        // â”€â”€ UI EVENT HANDLERS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // FIX: renamed to avoid overload ambiguity that caused silent compile failures
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyUnifiedUIFilter();
        }

        private void CboFloor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyUnifiedUIFilter();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (TxtSearchName != null) TxtSearchName.Text = string.Empty;
            if (CboFilterFloor != null) CboFilterFloor.SelectedIndex = 0;
            await FetchLatestDatabaseLogsAsync();
        }

        // â”€â”€ FILTER ENGINE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ApplyUnifiedUIFilter()
        {
            if (TxtSearchName == null || CboFilterFloor == null) return;

            string query = TxtSearchName.Text.Trim().ToLower();
            string floorFilter = (CboFilterFloor.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";

            if (ActiveClientsListView != null)
                ActiveClientsListView.ItemsSource = _cachedActiveClients
                    .Where(c => MatchesQuery(query, c.FullName, c.NIN) && MatchesFloor(floorFilter, c.FloorTag))
                    .ToList();

            if (AllLogsListView != null)
                AllLogsListView.ItemsSource = _cachedAllLogs
                    .Where(l => MatchesQuery(query, l.FullName, l.NIN) && MatchesFloor(floorFilter, l.FloorTag))
                    .ToList();

            if (DeletedClientsListView != null)
                DeletedClientsListView.ItemsSource = _cachedDeletedLogs
                    .Where(d => MatchesQuery(query, d.FullName, d.NIN) && MatchesFloor(floorFilter, d.FloorTag))
                    .ToList();
        }

        private static bool MatchesQuery(string q, string name, string nin)
        {
            return string.IsNullOrEmpty(q) ||
                   (name != null && name.ToLower().Contains(q)) ||
                   (nin != null && nin.ToLower().Contains(q));
        }

        private static bool MatchesFloor(string filter, string tag)
        {
            return filter == "All" || tag == filter;
        }

        // â”€â”€ DATA FETCH â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task FetchLatestDatabaseLogsAsync()
        {
            try
            {
                Task<List<ActiveClientDisplay>> activeTask = FetchActiveClientsAsync();
                Task<List<VisitorLogDisplay>> logsTask = FetchAllLogsAsync();
                Task<List<DeletedClientDisplay>> deletedTask = FetchDeletedClientsAsync();

                await Task.WhenAll(activeTask, logsTask, deletedTask);

                _cachedActiveClients = activeTask.Result;
                _cachedAllLogs = logsTask.Result;
                _cachedDeletedLogs = deletedTask.Result;

                ApplyUnifiedUIFilter();

                if (TxtStatActive != null) TxtStatActive.Text = _cachedActiveClients.Count.ToString();
                if (TxtStatLogs != null) TxtStatLogs.Text = _cachedAllLogs.Count.ToString();
                if (TxtStatDeleted != null) TxtStatDeleted.Text = _cachedDeletedLogs.Count.ToString();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Failed to load data", ex.Message);
            }
        }

        // â”€â”€ FETCH: Active clients â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<List<ActiveClientDisplay>> FetchActiveClientsAsync()
        {
            // FIX: explicit FK hints so PostgREST resolves the join unambiguously
            string url = App.SupabaseUrl + "/rest/v1/access_logs" +
                         "?select=log_id,time_in,status," +
                         "client!access_logs_client_id_fkey(" +
                         "first_name,last_name,nin,phone_number," +
                         "floors!client_floor_id_fkey(floor_id,floor_name))" +
                         "&status=eq.Signed%20In" +
                         "&order=time_in.desc";

            string json = await GetJsonAsync(url);
            if (json == null) return new List<ActiveClientDisplay>();

            var result = new List<ActiveClientDisplay>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    // FIX: single FirstIfArray unwrap (removed the erroneous second call)
                    if (!row.TryGetProperty("client", out JsonElement client) ||
                        client.ValueKind == JsonValueKind.Null)
                        continue;

                    client = FirstIfArray(client);
                    if (client.ValueKind != JsonValueKind.Object) continue;

                    if (!client.TryGetProperty("floors", out JsonElement floor) ||
                        floor.ValueKind == JsonValueKind.Null)
                        floor = default;

                    floor = FirstIfArray(floor);

                    int floorId = GetInt(floor, "floor_id");
                    string floorTag = floorId == 1 ? "1" : (floorId == 2 ? "2" : "All");

                    result.Add(new ActiveClientDisplay
                    {
                        LogId = GetInt(row, "log_id"),
                        FullName = GetStr(client, "first_name") + " " + GetStr(client, "last_name"),
                        NIN = GetStr(client, "nin") ?? "",
                        PhoneNumber = GetStr(client, "phone_number") ?? "",
                        FloorName = GetStr(floor, "floor_name") ?? "",
                        FloorTag = floorTag,
                        TimeIn = FormatTime(GetStr(row, "time_in"))
                    });
                }
            }

            return result;
        }

        // â”€â”€ FETCH: All logs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<List<VisitorLogDisplay>> FetchAllLogsAsync()
        {
            // FIX: explicit FK hints
            string url = App.SupabaseUrl + "/rest/v1/access_logs" +
                         "?select=log_id,access_date,time_in,time_out,status," +
                         "client!access_logs_client_id_fkey(" +
                         "first_name,last_name,nin," +
                         "floors!client_floor_id_fkey(floor_id,floor_name))" +
                         "&order=access_date.desc,time_in.desc";

            string json = await GetJsonAsync(url);
            if (json == null) return new List<VisitorLogDisplay>();

            var result = new List<VisitorLogDisplay>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    if (!row.TryGetProperty("client", out JsonElement client) ||
                        client.ValueKind == JsonValueKind.Null)
                        continue;

                    // FIX: unwrap client array (was missing entirely in the original)
                    client = FirstIfArray(client);
                    if (client.ValueKind != JsonValueKind.Object) continue;

                    if (!client.TryGetProperty("floors", out JsonElement floor) ||
                        floor.ValueKind == JsonValueKind.Null)
                        floor = default;

                    // FIX: unwrap floor array (was also missing)
                    floor = FirstIfArray(floor);

                    string timeIn = GetStr(row, "time_in") ?? "";
                    string timeOut = GetStr(row, "time_out") ?? "";
                    string status = GetStr(row, "status") ?? "";
                    int floorId = GetInt(floor, "floor_id");
                    string floorTag = floorId == 1 ? "1" : (floorId == 2 ? "2" : "All");

                    string timeSummary = status == "Signed In"
                        ? FormatTime(timeIn) + " - Active"
                        : FormatTime(timeIn) + " - " + FormatTime(timeOut);

                    result.Add(new VisitorLogDisplay
                    {
                        FullName = GetStr(client, "first_name") + " " + GetStr(client, "last_name"),
                        NIN = GetStr(client, "nin") ?? "",
                        FloorName = GetStr(floor, "floor_name") ?? "",
                        FloorTag = floorTag,
                        AccessDate = GetStr(row, "access_date") ?? "",
                        TimeInSummary = timeSummary,
                        Status = status == "Signed In" ? "INSIDE" : "EXITED",
                        StatusColor = status == "Signed In" ? "#2E7D32" : "#E65100"
                    });
                }
            }

            return result;
        }

        // â”€â”€ FETCH: Deleted clients â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<List<DeletedClientDisplay>> FetchDeletedClientsAsync()
        {
            // FIX: explicit FK hint for the floors join
            string url = App.SupabaseUrl + "/rest/v1/deleted_clients" +
                         "?select=first_name,last_name,nin,deleted_date,reason," +
                         "floors!deleted_clients_floor_id_fkey(floor_id,floor_name)" +
                         "&order=deleted_date.desc";

            string json = await GetJsonAsync(url);
            if (json == null) return new List<DeletedClientDisplay>();

            var result = new List<DeletedClientDisplay>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    if (!row.TryGetProperty("floors", out JsonElement floor) ||
                        floor.ValueKind == JsonValueKind.Null)
                        floor = default;

                    floor = FirstIfArray(floor);

                    int floorId = GetInt(floor, "floor_id");
                    string floorTag = floorId == 1 ? "1" : (floorId == 2 ? "2" : "All");

                    string rawDate = GetStr(row, "deleted_date") ?? "";
                    string formattedDate = DateTime.TryParse(rawDate, out DateTime dt)
                        ? dt.ToString("dd/MM/yyyy")
                        : rawDate;

                    result.Add(new DeletedClientDisplay
                    {
                        FullName = GetStr(row, "first_name") + " " + GetStr(row, "last_name"),
                        NIN = GetStr(row, "nin") ?? "",
                        FloorName = GetStr(floor, "floor_name") ?? "",
                        FloorTag = floorTag,
                        DeletedDateFormatted = formattedDate,
                        Reason = GetStr(row, "reason") ?? ""
                    });
                }
            }

            return result;
        }

        // â”€â”€ HELPERS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<string> GetJsonAsync(string url)
        {
            HttpResponseMessage response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    "Supabase request failed (" +
                    (int)response.StatusCode + " " + response.ReasonPhrase +
                    "): " + errorBody);
            }
            return await response.Content.ReadAsStringAsync();
        }

        private static JsonElement FirstIfArray(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
                foreach (JsonElement item in el.EnumerateArray())
                    return item;
            return el;
        }

        private static string FormatTime(string rawTime)
        {
            if (string.IsNullOrEmpty(rawTime)) return "";
            return TimeSpan.TryParse(rawTime, out TimeSpan t)
                ? DateTime.Today.Add(t).ToString("hh:mm tt")
                : rawTime;
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

        private async Task ShowErrorAsync(string title, string message)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private void enterclientsbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(entry));
        }

        private void backbtnMD_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            // Already on Dashboard
        }

        private void NavRegister_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(entry));
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Reports));
        }

        private void NavLogs_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SystemLogs));
        }

        private void NavReceptionists_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Settings));
        }

        private void NavLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }

        private async void BtnTimeOut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int logId)
            {
                try
                {
                    var updatePayload = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["status"] = "Signed Out",
                        ["time_out"] = DateTime.Now.ToString("HH:mm:ss")
                    };

                    string json = JsonSerializer.Serialize(updatePayload);
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), App.SupabaseUrl + "/rest/v1/access_logs?log_id=eq." + logId)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("Prefer", "return=minimal");
                    
                    var response = await _http.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        await FetchLatestDatabaseLogsAsync();
                    }
                }
                catch
                {
                    // Silently fail or ignore for now
                }
            }
        }
    }

    // â”€â”€ DISPLAY MODEL CLASSES â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public class ActiveClientDisplay
    {
        public int LogId { get; set; }
        public string FullName { get; set; }
        public string NIN { get; set; }
        public string FloorName { get; set; }
        public string FloorTag { get; set; }
        public string TimeIn { get; set; }
        public string PhoneNumber { get; set; }
    }

    public class VisitorLogDisplay
    {
        public string FullName { get; set; }
        public string NIN { get; set; }
        public string FloorName { get; set; }
        public string FloorTag { get; set; }
        public string AccessDate { get; set; }
        public string TimeInSummary { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
    }

    public class DeletedClientDisplay
    {
        public string FullName { get; set; }
        public string NIN { get; set; }
        public string FloorName { get; set; }
        public string FloorTag { get; set; }
        public string DeletedDateFormatted { get; set; }
        public string Reason { get; set; }
    }
}