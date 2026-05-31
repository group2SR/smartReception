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

        // ── Joined from client table — populated manually after fetch ──────
        public string ClientName { get; set; }
        public string NIN { get; set; }
        public string Floor { get; set; }

        // Computed display properties — not mapped to DB
        public string Date
        {
            get
            {
                if (string.IsNullOrEmpty(AccessDate)) return "-";
                DateTime dt;
                if (DateTime.TryParse(AccessDate, out dt))
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

    // ── Matches your client table exactly ─────────────────────────────────
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

        public string FullName
        {
            get { return (FirstName + " " + LastName).Trim(); }
        }
    }

    public sealed partial class Access_Control : Page
    {
        private ObservableCollection<AccessLog> _allLogs = new ObservableCollection<AccessLog>();
        private ObservableCollection<AccessLog> _pagedLogs = new ObservableCollection<AccessLog>();

        private int _currentPage = 1;
        private const int PAGE_SIZE = 10;
        private int _totalEntries = 0;

        public Access_Control()
        {
            this.InitializeComponent();
            AccessLogsList.ItemsSource = _pagedLogs;
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
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;

                await App.EnsureSupabaseInitializedAsync();

                if (App.SupabaseClient == null)
                    throw new Exception("Supabase client failed to initialize.");

                // ── Read filter values ─────────────────────────────────────
                string search = SearchBox.Text != null ? SearchBox.Text.Trim() : "";
                string floorVal = "";

                if (FloorCombo.SelectedItem is ComboBoxItem floorItem)
                {
                    string v = floorItem.Content != null ? floorItem.Content.ToString() : "";
                    if (!string.IsNullOrEmpty(v) && v != "All Floors")
                        floorVal = v;
                }

                string dateFrom = DateFrom.Date.HasValue
                    ? DateFrom.Date.Value.ToString("yyyy-MM-dd") : "";
                string dateTo = DateTo.Date.HasValue
                    ? DateTo.Date.Value.ToString("yyyy-MM-dd") : "";

                // ── Fetch access_logs ordered by access_date ───────────────
                Postgrest.Responses.ModeledResponse<AccessLog> result;

                if (!string.IsNullOrEmpty(dateFrom) && !string.IsNullOrEmpty(dateTo))
                {
                    result = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Filter("access_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }
                else if (!string.IsNullOrEmpty(dateFrom))
                {
                    result = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.GreaterThanOrEqual, dateFrom)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }
                else if (!string.IsNullOrEmpty(dateTo))
                {
                    result = await App.SupabaseClient.From<AccessLog>()
                        .Filter("access_date", Postgrest.Constants.Operator.LessThanOrEqual, dateTo)
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }
                else
                {
                    result = await App.SupabaseClient.From<AccessLog>()
                        .Order("access_date", Postgrest.Constants.Ordering.Descending)
                        .Get();
                }

                // ── Fetch all clients to join names/NIN/floor ──────────────
                var clientsResult = await App.SupabaseClient.From<Visitor>().Get();

                Dictionary<int, Visitor> clientMap = new Dictionary<int, Visitor>();
                if (clientsResult != null && clientsResult.Models != null)
                {
                    foreach (Visitor c in clientsResult.Models)
                        clientMap[c.Id] = c;
                }

                // ── Floor name map ─────────────────────────────────────────
                Dictionary<int, string> floorMap = new Dictionary<int, string>();
                floorMap[1] = "1st Floor";
                floorMap[2] = "2nd Floor";

                _allLogs.Clear();

                if (result != null && result.Models != null)
                {
                    foreach (AccessLog log in result.Models)
                    {
                        // Join client data onto the log
                        if (clientMap.ContainsKey(log.ClientId))
                        {
                            Visitor client = clientMap[log.ClientId];
                            log.ClientName = client.FullName;
                            log.NIN = client.NIN ?? "-";
                            log.Floor = floorMap.ContainsKey(client.FloorId)
                                             ? floorMap[client.FloorId] : "-";
                        }
                        else
                        {
                            log.ClientName = "Client #" + log.ClientId;
                            log.NIN = "-";
                            log.Floor = "-";
                        }

                        // Apply search filter (client name or NIN)
                        if (!string.IsNullOrEmpty(search))
                        {
                            string s = search.ToLower();
                            if (!log.ClientName.ToLower().Contains(s) &&
                                !log.NIN.ToLower().Contains(s))
                                continue;
                        }

                        // Apply floor filter
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
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Error loading logs",
                    Content = ex.Message,
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshPage()
        {
            List<AccessLog> all = new List<AccessLog>(_allLogs);
            _totalEntries = all.Count;

            int totalPages = _totalEntries == 0
                ? 1
                : (int)Math.Ceiling(_totalEntries / (double)PAGE_SIZE);

            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            int skip = (_currentPage - 1) * PAGE_SIZE;
            List<AccessLog> page = all.Skip(skip).Take(PAGE_SIZE).ToList();

            _pagedLogs.Clear();
            foreach (AccessLog log in page)
                _pagedLogs.Add(log);

            int from = _totalEntries == 0 ? 0 : skip + 1;
            int to = Math.Min(_currentPage * PAGE_SIZE, _totalEntries);
            EntriesLabel.Text = "Showing " + from + " to " + to
                              + " of " + _totalEntries + " entries";

            EmptyState.Visibility = _pagedLogs.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            BuildPaginationButtons(totalPages);
        }

        private void BuildPaginationButtons(int totalPages)
        {
            PaginationPanel.Children.Clear();

            AddPagerButton("‹", _currentPage > 1, _currentPage - 1);

            int start = Math.Max(1, _currentPage - 2);
            int end = Math.Min(totalPages, start + 4);

            for (int p = start; p <= end; p++)
                AddPagerButton(p.ToString(), p != _currentPage, p);

            AddPagerButton("›", _currentPage < totalPages, _currentPage + 1);
        }

        private void AddPagerButton(string label, bool enabled, int targetPage)
        {
            bool isActive = label == _currentPage.ToString();

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

            PaginationPanel.Children.Add(btn);
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private void receptionistlogoutbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }

        private void receptionaccesbackbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(dashboard));
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