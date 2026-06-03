using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;

namespace smartReception
{
    public sealed partial class SystemLogs : Page
    {
        // ── Tabs ──────────────────────────────────────────────────────────────
        private bool _showingSessions = true;

        // ── Pagination ────────────────────────────────────────────────────────
        private const int PageSize = 20;
        private int _currentPage = 1;
        private int _totalCount = 0;

        // ── Raw data ──────────────────────────────────────────────────────────
        private List<SystemLogModel> _allLogs = new List<SystemLogModel>();
        private List<ReceptionistSession> _allSessions = new List<ReceptionistSession>();

        // Sentinel: DatePicker is "empty" when its year == 1600 (UWP default MinYear)
        private static readonly DateTimeOffset _dpEmpty =
            new DateTimeOffset(1600, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public SystemLogs()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Force "All Activities" (index 0) so no accidental activity filter on load
            ActivityFilter.SelectedIndex = 0;

            // Reset date pickers to their UWP default (year 1600 = unset sentinel)
            StartDatePicker.Date = _dpEmpty;
            EndDatePicker.Date = _dpEmpty;

            await LoadLogsAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper: treat the UWP DatePicker sentinel (year <= 1600) as "not set"
        // ─────────────────────────────────────────────────────────────────────
        private static DateTimeOffset? DatePickerValue(DatePicker dp)
        {
            // UWP DatePicker default / MinYear is 1600; treat anything that old as empty
            if (dp.Date.Year <= 1600) return null;
            return dp.Date;
        }

        // ── Fetch from Supabase ───────────────────────────────────────────────
        private async Task LoadLogsAsync(
            string actorSearch = null,
            string activity = null,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            ShowStatus("⏳ Loading logs…", false);

            try
            {
                await App.EnsureSupabaseInitializedAsync();

                var sb = new StringBuilder();
                sb.Append(App.SupabaseUrl);
                sb.Append("/rest/v1/system_logs");
                sb.Append("?select=system_log_id,user_id,activity,description,log_date,");
                sb.Append("system_users(first_name,last_name,roles(role_name))");

                // Activity filter — skip when null or "All Activities"
                if (!string.IsNullOrEmpty(activity) && activity != "All Activities")
                {
                    sb.Append("&activity=eq.");
                    sb.Append(Uri.EscapeDataString(activity));
                }

                // Date range filters
                if (startDate.HasValue)
                {
                    sb.Append("&log_date=gte.");
                    sb.Append(startDate.Value.UtcDateTime.ToString("O"));
                }
                if (endDate.HasValue)
                {
                    // Cover the full end day up to 23:59:59.999
                    DateTime endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    sb.Append("&log_date=lte.");
                    sb.Append(endOfDay.ToUniversalTime().ToString("O"));
                }

                // Ascending so Login always comes before Logout for pairing
                sb.Append("&order=log_date.asc");

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
                    http.DefaultRequestHeaders.Add("Authorization", "Bearer " + App.SupabaseAnonKey);

                    var response = await http.GetAsync(sb.ToString());
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        throw new Exception("Supabase error " +
                            (int)response.StatusCode + ": " + json);

                    var parsed = JArray.Parse(json);
                    var rawLogs = new List<SystemLogModel>();

                    foreach (var item in parsed)
                    {
                        var userToken = item["system_users"];
                        string actor = "User #" + item["user_id"];
                        string role = "—";

                        if (userToken != null && userToken.Type != JTokenType.Null)
                        {
                            string first = userToken["first_name"]?.ToString() ?? "";
                            string last = userToken["last_name"]?.ToString() ?? "";
                            actor = (first + " " + last).Trim();
                            if (string.IsNullOrWhiteSpace(actor))
                                actor = "User #" + item["user_id"];

                            var roleToken = userToken["roles"];
                            if (roleToken != null && roleToken.Type != JTokenType.Null)
                                role = roleToken["role_name"]?.ToString() ?? "—";
                        }

                        DateTimeOffset logDate = DateTimeOffset.MinValue;
                        string rawDate = item["log_date"]?.ToString();
                        if (!string.IsNullOrEmpty(rawDate))
                            DateTimeOffset.TryParse(rawDate, null,
                                System.Globalization.DateTimeStyles.RoundtripKind,
                                out logDate);

                        rawLogs.Add(new SystemLogModel
                        {
                            SystemLogId = item["system_log_id"]?.ToObject<int>() ?? 0,
                            UserId = item["user_id"]?.ToObject<int>() ?? 0,
                            Actor = actor,
                            Role = role,
                            Activity = item["activity"]?.ToString() ?? "—",
                            Description = item["description"]?.ToString() ?? "—",
                            LogDate = logDate
                        });
                    }

                    // Client-side actor name search
                    if (!string.IsNullOrWhiteSpace(actorSearch))
                    {
                        string q = actorSearch.Trim().ToLowerInvariant();
                        rawLogs = rawLogs
                            .Where(l => l.Actor.ToLowerInvariant().Contains(q))
                            .ToList();
                    }

                    // rawLogs = ascending (needed for correct session pairing)
                    // _allLogs = descending (newest first for All Events tab)
                    _allLogs = Enumerable.Reverse(rawLogs).ToList();
                    _allSessions = BuildSessions(rawLogs);

                    UpdateStats();
                    _currentPage = 1;
                    ApplyPage();
                    HideStatus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus("❌ " + ex.Message, true);
            }
        }

        // ── Pair Login → Logout rows into sessions ────────────────────────────
        private List<ReceptionistSession> BuildSessions(List<SystemLogModel> logs)
        {
            var sessions = new List<ReceptionistSession>();

            foreach (var userGroup in logs.GroupBy(l => l.UserId))
            {
                string name = userGroup.First().Actor;
                string role = userGroup.First().Role;

                foreach (var dayGroup in userGroup
                    .GroupBy(l => l.LogDate.LocalDateTime.Date))
                {
                    string dateStr = dayGroup.Key.ToString("dd MMM yyyy");

                    var logins = dayGroup
                        .Where(l => l.Activity == "Login")
                        .OrderBy(l => l.LogDate).ToList();
                    var logouts = dayGroup
                        .Where(l => l.Activity == "Logout")
                        .OrderBy(l => l.LogDate).ToList();

                    if (logins.Count == 0) continue;

                    // logoutIdx resets per day — fixes cross-day pairing bug
                    int logoutIdx = 0;

                    foreach (var login in logins)
                    {
                        SystemLogModel matchedLogout = null;
                        for (int i = logoutIdx; i < logouts.Count; i++)
                        {
                            if (logouts[i].LogDate > login.LogDate)
                            {
                                matchedLogout = logouts[i];
                                logoutIdx = i + 1;
                                break;
                            }
                        }

                        string timeIn = login.LogDate.LocalDateTime.ToString("HH:mm:ss");
                        string timeOut = null;
                        string status = "Active";
                        string duration = "In progress";

                        if (matchedLogout != null)
                        {
                            timeOut = matchedLogout.LogDate.LocalDateTime.ToString("HH:mm:ss");
                            status = "Completed";
                            duration = FormatDuration(matchedLogout.LogDate - login.LogDate);
                        }

                        sessions.Add(new ReceptionistSession
                        {
                            ReceptionistName = name,
                            Role = role,
                            Date = dateStr,
                            TimeIn = timeIn,
                            TimeOut = timeOut,
                            Duration = duration,
                            Status = status
                        });
                    }
                }
            }

            return sessions
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.TimeIn)
                .ToList();
        }

        private string FormatDuration(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "< 1 min";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " min";
            if (span.TotalHours < 24) return (int)span.TotalHours + "h " + span.Minutes + "m";
            return (int)span.TotalDays + "d " + span.Hours + "h";
        }



        // ── Stats bar ─────────────────────────────────────────────────────────
        private void UpdateStats()
        {
            StatTotalSessions.Text = _allSessions.Count.ToString();
            StatActiveNow.Text = _allSessions.Count(s => s.Status == "Active").ToString();

            var completed = _allSessions.Where(s => s.Status == "Completed").ToList();
            if (completed.Count > 0)
            {
                double totalMins = 0;
                int counted = 0;
                foreach (var s in completed)
                {
                    double m = ParseDurationToMinutes(s.Duration);
                    if (m >= 0) { totalMins += m; counted++; }
                }
                StatAvgDuration.Text = counted > 0
                    ? FormatDuration(TimeSpan.FromMinutes(totalMins / counted))
                    : "—";
            }
            else
            {
                StatAvgDuration.Text = "—";
            }

            string today = DateTime.Today.ToString("dd MMM yyyy");
            StatTodayLogins.Text =
                _allSessions.Count(s => s.Date == today).ToString();
        }

        private double ParseDurationToMinutes(string d)
        {
            if (string.IsNullOrEmpty(d) || d == "In progress") return -1;
            if (d.StartsWith("<")) return 0.5;
            if (d.EndsWith("min"))
            {
                double v;
                return double.TryParse(d.Replace("min", "").Trim(), out v) ? v : -1;
            }
            if (d.Contains("h"))
            {
                var p = d.Split('h');
                double h; double.TryParse(p[0].Trim(), out h);
                double m = 0;
                if (p.Length > 1) double.TryParse(p[1].Replace("m", "").Trim(), out m);
                return h * 60 + m;
            }
            if (d.Contains("d"))
            {
                var p = d.Split('d');
                double day; double.TryParse(p[0].Trim(), out day);
                double h = 0;
                if (p.Length > 1) double.TryParse(p[1].Replace("h", "").Trim(), out h);
                return day * 1440 + h * 60;
            }
            return -1;
        }

        // ── Pagination ────────────────────────────────────────────────────────
        private void ApplyPage()
        {
            if (_showingSessions)
            {
                SessionsListView.ItemsSource = _allSessions
                    .Skip((_currentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();
                _totalCount = _allSessions.Count;
                SessionsEmpty.Visibility =
                    _totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                LogsListView.ItemsSource = _allLogs
                    .Skip((_currentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();
                _totalCount = _allLogs.Count;
                EventsEmpty.Visibility =
                    _totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            int totalPages = Math.Max(1,
                (int)Math.Ceiling(_totalCount / (double)PageSize));

            PaginationLabel.Text = _totalCount == 0
                ? "No entries found"
                : "Showing " + ((_currentPage - 1) * PageSize + 1) +
                  "–" + Math.Min(_currentPage * PageSize, _totalCount) +
                  " of " + _totalCount + " entries";

            PageBtn.Content = _currentPage.ToString();
            PrevBtn.IsEnabled = _currentPage > 1;
            NextBtn.IsEnabled = _currentPage < totalPages;
        }

        // ── Tab switching ─────────────────────────────────────────────────────
        private void TabSessions_Click(object sender, RoutedEventArgs e)
        {
            _showingSessions = true;
            SessionsPanel.Visibility = Visibility.Visible;
            EventsPanel.Visibility = Visibility.Collapsed;
            TabSessions.Style = (Style)Resources["ActiveTabButtonStyle"];
            TabAllEvents.Style = (Style)Resources["TabButtonStyle"];
            _currentPage = 1;
            ApplyPage();
        }

        private void TabAllEvents_Click(object sender, RoutedEventArgs e)
        {
            _showingSessions = false;
            SessionsPanel.Visibility = Visibility.Collapsed;
            EventsPanel.Visibility = Visibility.Visible;
            TabSessions.Style = (Style)Resources["TabButtonStyle"];
            TabAllEvents.Style = (Style)Resources["ActiveTabButtonStyle"];
            _currentPage = 1;
            ApplyPage();
        }

        // ── Filter ────────────────────────────────────────────────────────────
        private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            string actor = SearchBox.Text?.Trim();
            string activity = null;

            // Only pass an activity when the user picked something other than index 0
            if (ActivityFilter.SelectedIndex > 0)
            {
                var sel = ActivityFilter.SelectedItem as ComboBoxItem;
                activity = sel?.Content?.ToString();
            }

            // Use the sentinel helper — returns null when picker is at default year
            DateTimeOffset? start = DatePickerValue(StartDatePicker);
            DateTimeOffset? end = DatePickerValue(EndDatePicker);

            await LoadLogsAsync(actor, activity, start, end);
        }

        // ── Pagination buttons ────────────────────────────────────────────────
        private void PrevBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; ApplyPage(); }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_totalCount / (double)PageSize);
            if (_currentPage < totalPages) { _currentPage++; ApplyPage(); }
        }

        // ── CSV Export ────────────────────────────────────────────────────────
        private async void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "SystemLogs_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmm")
            };
            picker.FileTypeChoices.Add("CSV File", new List<string> { ".csv" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var csv = new StringBuilder();

            if (_showingSessions)
            {
                csv.AppendLine("Receptionist,Role,Date,Sign In,Sign Out,Duration,Status");
                foreach (var s in _allSessions)
                    csv.AppendLine(
                        $"\"{s.ReceptionistName}\",\"{s.Role}\",\"{s.Date}\"," +
                        $"\"{s.TimeIn}\",\"{s.TimeOut ?? "Active"}\"," +
                        $"\"{s.Duration}\",\"{s.Status}\"");
            }
            else
            {
                csv.AppendLine("Log ID,Actor,Role,Activity,Description,Date & Time");
                foreach (var log in _allLogs)
                {
                    string desc = log.Description?.Replace("\"", "\"\"") ?? "";
                    csv.AppendLine(
                        $"{log.SystemLogId},\"{log.Actor}\",\"{log.Role}\"," +
                        $"\"{log.Activity}\",\"{desc}\",\"{log.FormattedDate}\"");
                }
            }

            await FileIO.WriteTextAsync(file, csv.ToString(),
                Windows.Storage.Streams.UnicodeEncoding.Utf8);

            ShowStatus("✅ Exported to " + file.Name, false);
        }

        // ── Status banner ─────────────────────────────────────────────────────
        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusBanner.Background = isError
                ? new SolidColorBrush(Color.FromArgb(255, 254, 242, 242))
                : new SolidColorBrush(Color.FromArgb(255, 239, 246, 255));
            StatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromArgb(255, 185, 28, 28))
                : new SolidColorBrush(Color.FromArgb(255, 29, 78, 216));
            StatusBanner.Visibility = Visibility.Visible;
        }

        private void HideStatus() => StatusBanner.Visibility = Visibility.Collapsed;

        // ── Navigation ────────────────────────────────────────────────────────
        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(dashboard))
                Frame.Navigate(typeof(dashboard));
        }
        private void NavAccessControl_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(Access_Control))
                Frame.Navigate(typeof(Access_Control));
        }
        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(Reports))
                Frame.Navigate(typeof(Reports));
        }
        private void NavLogs_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(SystemLogs))
                Frame.Navigate(typeof(SystemLogs));
        }
        private void NavReceptionists_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(UsersReceptionist))
                Frame.Navigate(typeof(UsersReceptionist));
        }
        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            if (GetType() != typeof(Settings))
                Frame.Navigate(typeof(Settings));
        }
        private void NavLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }
    }
}