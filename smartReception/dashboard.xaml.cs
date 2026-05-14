using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using System.Linq;
using Postgrest;

namespace smartReception
{
    public sealed partial class dashboard : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<string> VisitorLogs { get; set; } = new ObservableCollection<string>();

        // Binding Properties for Metric Cards
        private string _totalClients = "0";
        public string TotalClients
        {
            get => _totalClients;
            set { _totalClients = value; OnPropertyChanged(nameof(TotalClients)); }
        }

        private string _activeToday = "0";
        public string ActiveToday
        {
            get => _activeToday;
            set { _activeToday = value; OnPropertyChanged(nameof(ActiveToday)); }
        }

        private string _entriesToday = "0";
        public string EntriesToday
        {
            get => _entriesToday;
            set { _entriesToday = value; OnPropertyChanged(nameof(EntriesToday)); }
        }

        public dashboard()
        {
            this.InitializeComponent();
            this.DataContext = this;
            _ = RefreshDashboardMetrics();
        }

        private async Task RefreshDashboardMetrics()
        {
            try
            {
                // 1. Total Clients count
                var totalRes = await App.SupabaseClient.From<Visitor>().Count(Constants.CountType.Exact);
                TotalClients = totalRes.ToString();

                // 2. Active Today (Currently Signed In)
                var activeRes = await App.SupabaseClient.From<AccessLog>()
                    .Filter("status", Constants.Operator.Equals, "Signed In")
                    .Count(Constants.CountType.Exact);
                ActiveToday = activeRes.ToString();

                // 3. Entries Today (Filtered by date)
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var entriesRes = await App.SupabaseClient.From<AccessLog>()
                    .Filter("access_date", Constants.Operator.Equals, today)
                    .Count(Constants.CountType.Exact);
                EntriesToday = entriesRes.ToString();

                // 4. Update Activity List
                var recentLogs = await App.SupabaseClient.From<AccessLog>()
                    .Order("id", Constants.Ordering.Descending)
                    .Limit(10)
                    .Get();

                VisitorLogs.Clear();
                foreach (var log in recentLogs.Models)
                {
                    VisitorLogs.Add($"Client ID: {log.ClientId} - {log.Status} at {DateTime.Now:HH:mm}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Metric Error: {ex.Message}");
            }
        }

        // --- REGISTRATION LOGIC ---
        private async void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFirst.Text) || string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                ShowFeedback("⚠ Name and Email are required.", false);
                return;
            }

            try
            {
                var newClient = new Visitor
                {
                    FirstName = txtFirst.Text.Trim(),
                    LastName = txtLast.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    PhoneNumber = txtPhone.Text.Trim(),
                    NIN = txtNIN.Text.Trim()
                };

                await App.SupabaseClient.From<Visitor>().Insert(newClient);

                ShowFeedback($"✅ {newClient.FirstName} Registered!", true);

                // Reset Form
                txtFirst.Text = txtLast.Text = txtEmail.Text = txtPhone.Text = txtNIN.Text = "";
                await RefreshDashboardMetrics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save Error: {ex.Message}");
                ShowFeedback("❌ Save failed. Email might already exist.", false);
            }
        }

        // --- ACCESS CONTROL LOGIC ---
        private async void btnVerify_Click(object sender, RoutedEventArgs e)
        {
            string searchName = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchName)) return;

            try
            {
                var result = await App.SupabaseClient.From<Visitor>()
                    .Filter("first_name", Constants.Operator.ILike, $"%{searchName}%").Get();

                if (result.Models.Any())
                {
                    var client = result.Models.First();
                    var activeLog = await App.SupabaseClient.From<AccessLog>()
                        .Filter("client_id", Constants.Operator.Equals, client.Id)
                        .Filter("status", Constants.Operator.Equals, "Signed In")
                        .Get();

                    if (activeLog.Models.Any())
                    {
                        var logToUpdate = activeLog.Models.First();
                        logToUpdate.Status = "Signed Out";
                        await App.SupabaseClient.From<AccessLog>().Update(logToUpdate);
                        ShowFeedback($"✅ {client.FirstName} Signed Out", true);
                    }
                    else
                    {
                        var newLog = new AccessLog { ClientId = client.Id, Status = "Signed In" };
                        await App.SupabaseClient.From<AccessLog>().Insert(newLog);
                        ShowFeedback($"✅ Access Granted: {client.FirstName}", true);
                    }

                    await RefreshDashboardMetrics();
                }
                else
                {
                    ShowFeedback("❌ Access Denied: Client Not Found .", false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Verify Error: {ex.Message}");
            }
        }

        private void ShowFeedback(string message, bool isSuccess)
        {
            txtStatusFeedback.Text = message;
            txtStatusFeedback.Foreground = isSuccess ?
                new SolidColorBrush(Windows.UI.Colors.Green) :
                new SolidColorBrush(Windows.UI.Colors.Red);
            borderFeedback.Visibility = Visibility.Visible;
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}