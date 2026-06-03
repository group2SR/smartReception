using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Postgrest.Models;
using Postgrest.Attributes;

namespace smartReception
{
    [Table("access_logs")]
    public class AccessLog : BaseModel
    {
        [PrimaryKey("log_id", false)]
        public int LogId { get; set; }

        [Column("client_id")]
        public int ClientId { get; set; }

        [Column("access_date")]
        public string AccessDate { get; set; }

        [Column("time_in")]
        public string TimeIn { get; set; }

        [Column("time_out")]
        public string TimeOut { get; set; }

        [Column("status")]
        public string Status { get; set; }

        public string ClientName { get; set; }
        public string NIN { get; set; }
        public string Floor { get; set; }
        public string PhoneNumber { get; set; }
        public string RowType => "Client";

        public string Date
        {
            get
            {
                if (string.IsNullOrEmpty(AccessDate)) return "-";
                if (DateTime.TryParse(AccessDate, out DateTime dt))
                    return dt.ToString("dd/MM/yyyy");
                return AccessDate;
            }
        }

        public string Initials
        {
            get
            {
                if (string.IsNullOrEmpty(ClientName)) return "?";
                string[] parts = ClientName.Trim().Split(' ');
                string first = parts[0].Length > 0 ? parts[0][0].ToString() : "";
                string last = parts.Length > 1 && parts[parts.Length - 1].Length > 0
                               ? parts[parts.Length - 1][0].ToString() : "";
                return (first + last).ToUpper();
            }
        }
    }

    [Table("visitors")]
    public class VisitorLog : BaseModel
    {
        [PrimaryKey("visitor_id", false)]
        public int VisitorId { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; }

        [Column("floor_id")]
        public int FloorId { get; set; }

        [Column("visit_date")]
        public string VisitDate { get; set; }

        [Column("time_in")]
        public string TimeIn { get; set; }

        [Column("time_out")]
        public string TimeOut { get; set; }

        [Column("nin")]
        public string NIN { get; set; }

        public string RowType => "Visitor";

        public string ClientName => (FirstName + " " + LastName).Trim();

        public string Status => string.IsNullOrEmpty(TimeOut) ? "Active" : "Completed";

        public string Floor { get; set; }

        public string Date
        {
            get
            {
                if (string.IsNullOrEmpty(VisitDate)) return "-";
                return DateTime.TryParse(VisitDate, out DateTime dt)
                    ? dt.ToString("dd/MM/yyyy") : VisitDate;
            }
        }

        public string Initials
        {
            get
            {
                string name = ClientName;
                if (string.IsNullOrEmpty(name)) return "?";
                string[] parts = name.Trim().Split(' ');
                string first = parts[0].Length > 0 ? parts[0][0].ToString() : "";
                string last = parts.Length > 1 && parts[parts.Length - 1].Length > 0
                               ? parts[parts.Length - 1][0].ToString() : "";
                return (first + last).ToUpper();
            }
        }
    }

    [Table("client")]
    public class Visitor : BaseModel
    {
        [PrimaryKey("client_id", false)]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("phone_number")]
        public string PhoneNumber { get; set; }

        [Column("nin")]
        public string NIN { get; set; }

        [Column("floor_id")]
        public int FloorId { get; set; }

        public string FullName => (FirstName + " " + LastName).Trim();
    }

    public sealed partial class Access_Control : Page
    {
        private ObservableCollection<object> _allLogs = new ObservableCollection<object>();
        private ObservableCollection<object> _pagedLogs = new ObservableCollection<object>();

        private int _currentPage = 1;
        private const int PAGE_SIZE = 10;
        private int _totalEntries = 0;

        private static readonly Dictionary<int, string> _floorMap = new Dictionary<int, string>
        {
            [1] = "1st Floor",
            [2] = "2nd Floor",
            [3] = "3rd Floor",
            [4] = "4th Floor"
        };

        public Access_Control()
        {
            this.InitializeComponent();
            try { AccessLogsList.ItemsSource = _pagedLogs; } catch { }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadLogsAsync();
        }

        private async System.Threading.Tasks.Task LoadLogsAsync()
        {
            try
            {
                try
                {
                    LoadingRing.IsActive = true;
                    LoadingRing.Visibility = Visibility.Visible;
                }
                catch { }

                try { EmptyState.Visibility = Visibility.Collapsed; } catch { }

                await App.EnsureSupabaseInitializedAsync();

                if (App.SupabaseClient == null)
                    throw new Exception("Supabase client failed to initialize.");

                string search = "";
                try { search = SearchBox.Text?.Trim() ?? ""; } catch { }

                string floorVal = "";
                try
                {
                    if (FloorCombo.SelectedItem is ComboBoxItem floorItem)
                    {
                        string v = floorItem.Content?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(v) && v != "All Floors")
                            floorVal = v;
                    }
                }
                catch { }

                string dateFrom = "";
                string dateTo = "";
                try
                {
                    dateFrom = DateFrom.Date.HasValue ? DateFrom.Date.Value.ToString("yyyy-MM-dd") : "";
                    dateTo = DateTo.Date.HasValue ? DateTo.Date.Value.ToString("yyyy-MM-dd") : "";
                }
                catch { }

                Postgrest.Responses.ModeledResponse<AccessLog> clientResult;

                if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
                    clientResult = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Filter("access_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else if (!string.IsNullOrEmpty(dateFrom))
                    clientResult = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else if (!string.IsNullOrEmpty(dateTo))
                    clientResult = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else
                    clientResult = await App.SupabaseClient.From<AccessLog>()
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();

                Postgrest.Responses.ModeledResponse<VisitorLog> visitorResult;

                if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
                    visitorResult = await App.SupabaseClient.From<VisitorLog>()
                        .Filter("visit_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Filter("visit_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("visit_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else if (!string.IsNullOrEmpty(dateFrom))
                    visitorResult = await App.SupabaseClient.From<VisitorLog>()
                        .Filter("visit_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Order("visit_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else if (!string.IsNullOrEmpty(dateTo))
                    visitorResult = await App.SupabaseClient.From<VisitorLog>()
                        .Filter("visit_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("visit_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                else
                    visitorResult = await App.SupabaseClient.From<VisitorLog>()
                        .Order("visit_date", Postgrest.Constants.Ordering.Descending)
                        .Get();

                var clientsResult = await App.SupabaseClient.From<Visitor>().Get();
                var clientMap = new Dictionary<int, Visitor>();
                if (clientsResult?.Models != null)
                {
                    foreach (Visitor c in clientsResult.Models)
                        clientMap[c.Id] = c;
                }

                _allLogs.Clear();

                if (clientResult?.Models != null)
                {
                    foreach (AccessLog log in clientResult.Models)
                    {
                        if (clientMap.ContainsKey(log.ClientId))
                        {
                            Visitor client = clientMap[log.ClientId];
                            log.ClientName = client.FullName;
                            log.NIN = client.NIN ?? "-";
                            log.Floor = _floorMap.ContainsKey(client.FloorId) ? _floorMap[client.FloorId] : "-";
                            log.PhoneNumber = client.PhoneNumber ?? "---";
                        }
                        else
                        {
                            log.ClientName = "Client #" + log.ClientId;
                            log.NIN = "-";
                            log.Floor = "-";
                            log.PhoneNumber = "---";
                        }

                        if (!string.IsNullOrEmpty(search))
                        {
                            string s = search.ToLower();
                            if (!log.ClientName.ToLower().Contains(s) && !log.NIN.ToLower().Contains(s))
                                continue;
                        }

                        if (!string.IsNullOrEmpty(floorVal) && log.Floor != floorVal)
                            continue;

                        _allLogs.Add(log);
                    }
                }

                if (visitorResult?.Models != null)
                {
                    foreach (VisitorLog log in visitorResult.Models)
                    {
                        log.Floor = _floorMap.ContainsKey(log.FloorId) ? _floorMap[log.FloorId] : "-";

                        if (!string.IsNullOrEmpty(search))
                        {
                            string s = search.ToLower();
                            if (!log.ClientName.ToLower().Contains(s) && !(log.NIN ?? "").ToLower().Contains(s))
                                continue;
                        }

                        if (!string.IsNullOrEmpty(floorVal) && log.Floor != floorVal)
                            continue;

                        _allLogs.Add(log);
                    }
                }

                _totalEntries = _allLogs.Count;
                _currentPage = 1;
                RefreshPage();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Error loading logs", ex.Message);
            }
            finally
            {
                try
                {
                    LoadingRing.IsActive = false;
                    LoadingRing.Visibility = Visibility.Collapsed;
                }
                catch { }
            }
        }

        private void RefreshPage()
        {
            var all = new List<object>(_allLogs);
            _totalEntries = all.Count;

            int totalPages = _totalEntries == 0 ? 1 : (int)Math.Ceiling(_totalEntries / (double)PAGE_SIZE);

            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            int skip = (_currentPage - 1) * PAGE_SIZE;
            var page = all.Skip(skip).Take(PAGE_SIZE).ToList();

            _pagedLogs.Clear();
            foreach (object log in page)
                _pagedLogs.Add(log);

            int from = _totalEntries == 0 ? 0 : skip + 1;
            int to = Math.Min(_currentPage * PAGE_SIZE, _totalEntries);

            string paginationMsg = $"Showing {from} to {to} of {_totalEntries} entries";

            try { EntriesLabel.Text = paginationMsg; } catch { }

            try { EmptyState.Visibility = _pagedLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed; } catch { }

            BuildPaginationButtons(totalPages);
        }

        private void BuildPaginationButtons(int totalPages)
        {
            try
            {
                PaginationPanel.Children.Clear();
                AddPagerButton("‹", _currentPage > 1, _currentPage - 1);

                int start = Math.Max(1, _currentPage - 2);
                int end = Math.Min(totalPages, start + 4);

                for (int p = start; p <= end; p++)
                    AddPagerButton(p.ToString(), p != _currentPage, p);

                AddPagerButton("›", _currentPage < totalPages, _currentPage + 1);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Pagination Error: " + ex.Message);
            }
        }

        private void AddPagerButton(string label, bool enabled, int targetPage)
        {
            Button btn = new Button
            {
                Content = label,
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                IsEnabled = enabled,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Tag = targetPage
            };

            bool isActive = label == _currentPage.ToString();
            if (isActive)
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(255, 0, 102, 226));
                btn.Foreground = new SolidColorBrush(Colors.White);
                btn.FontWeight = FontWeights.Bold;
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 102, 226));
            }
            else
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240));
                btn.Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105));
            }

            btn.Click += (s, e) =>
            {
                _currentPage = (int)((Button)s).Tag;
                RefreshPage();
            };

            try { PaginationPanel.Children.Add(btn); } catch { }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private void AccessLogsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            UpdateDetailsCard(e.ClickedItem);
        }

        private void UpdateDetailsCard(object item)
        {
            try
            {
                try { CardPhone.Text = "---"; } catch { }
                try { CardFloor.Text = "---"; } catch { }
                try { CardNIN.Text = "---"; } catch { }
                try { CardDateValue.Text = "---"; } catch { }

                if (item is AccessLog clientLog)
                {
                    try { CardFloor.Text = !string.IsNullOrEmpty(clientLog.Floor) ? clientLog.Floor : "---"; } catch { }
                    try { CardNIN.Text = !string.IsNullOrEmpty(clientLog.NIN) ? clientLog.NIN : "---"; } catch { }
                    try { CardPhone.Text = !string.IsNullOrEmpty(clientLog.PhoneNumber) ? clientLog.PhoneNumber : "---"; } catch { }
                    try { CardDateLabel.Text = "Checked In Timestamp"; } catch { }
                    try
                    {
                        CardDateValue.Text = !string.IsNullOrEmpty(clientLog.TimeOut)
                            ? $"{clientLog.Date} ({clientLog.TimeIn} - {clientLog.TimeOut})"
                            : $"{clientLog.Date} @ {clientLog.TimeIn} (Active)";
                    }
                    catch { }
                }
                else if (item is VisitorLog visitorLog)
                {
                    try { CardFloor.Text = !string.IsNullOrEmpty(visitorLog.Floor) ? visitorLog.Floor : "---"; } catch { }
                    try { CardNIN.Text = !string.IsNullOrEmpty(visitorLog.NIN) ? visitorLog.NIN : "---"; } catch { }
                    try { CardPhone.Text = "N/A (Walk-in)"; } catch { }
                    try { CardDateLabel.Text = "Visitor Entrance"; } catch { }
                    try
                    {
                        CardDateValue.Text = !string.IsNullOrEmpty(visitorLog.TimeOut)
                            ? $"{visitorLog.Date} ({visitorLog.TimeIn} - {visitorLog.TimeOut})"
                            : $"{visitorLog.Date} @ {visitorLog.TimeIn} (Inside)";
                    }
                    catch { }
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task ShowMessageAsync(string title, string content)
        {
            try
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            catch { }
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(dashboard)) Frame.Navigate(typeof(dashboard));
        }

        private void NavAccessControl_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(Access_Control)) Frame.Navigate(typeof(Access_Control));
        }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(Reports)) Frame.Navigate(typeof(Reports));
        }

        private void NavLogs_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(SystemLogs)) Frame.Navigate(typeof(SystemLogs));
        }

        private void NavReceptionists_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(UsersReceptionist)) Frame.Navigate(typeof(UsersReceptionist));
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            if (this.GetType() != typeof(Settings)) Frame.Navigate(typeof(Settings));
        }

        private void NavLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }
    }
}