using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace smartReception
{
    // ── Page ──────────────────────────────────────────────────────────────────
    public sealed partial class entry : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        private readonly ObservableCollection<ClientDisplayModel> _clients =
            new ObservableCollection<ClientDisplayModel>();
        private readonly ObservableCollection<VisitorDisplayModel> _visitors =
            new ObservableCollection<VisitorDisplayModel>();

        private int? _editingClientId = null;
        private bool _isVisitorMode = false;

        // Floor names for the preview card
        private static readonly string[] FloorNames =
            { "1st Floor", "2nd Floor", "3rd Floor", "4th Floor" };

        public entry()
        {
            this.InitializeComponent();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            ClientsListView.ItemsSource = _clients;
            VisitorsListView.ItemsSource = _visitors;

            DpStart.Date = DateTimeOffset.Now;
            DpEnd.Date = DateTimeOffset.Now.AddDays(30);

            // Set time_in default to now
            TxtTimeIn.Text = DateTime.Now.ToString("HH:mm");

            // Apply initial floor restrictions (client mode — only 1st & 2nd)
            ApplyFloorRestrictions();
            UpdatePreviewCard();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadClientsAsync();
            await LoadVisitorsAsync();
        }

        // ── TYPE TOGGLE ───────────────────────────────────────────────────────

        private void BtnTypeClient_Click(object sender, RoutedEventArgs e)
        {
            if (_isVisitorMode)
            {
                _isVisitorMode = false;
                BtnTypeClient.Style = (Style)Resources["ToggleActiveStyle"];
                BtnTypeVisitor.Style = (Style)Resources["ToggleInactiveStyle"];

                ClientDateRow.Visibility = Visibility.Visible;
                VisitorTimeRow.Visibility = Visibility.Collapsed;

                TxtTypeDesc.Text = "🏢  Client — rents space on Floor 1 or 2. Assigned for days, months or years.";
                TypeDescBorder.Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 236, 253, 245));
                TypeDescBorder.BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 167, 243, 208));
                TxtTypeDesc.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 6, 95, 70));

                TxtNINLabel.Text = "NIN (National ID) *";

                // Swap card gradient to green
                CardBgClient.Visibility = Visibility.Visible;
                CardBgVisitor.Visibility = Visibility.Collapsed;
                TypeBadge.Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 16, 185, 129));
                TxtTypeBadge.Text = "CLIENT";

                ApplyFloorRestrictions();
                ClearForm();
                UpdatePreviewCard();
            }
        }

        private void BtnTypeVisitor_Click(object sender, RoutedEventArgs e)
        {
            if (!_isVisitorMode)
            {
                _isVisitorMode = true;
                BtnTypeVisitor.Style = (Style)Resources["ToggleActiveStyle"];
                BtnTypeClient.Style = (Style)Resources["ToggleInactiveStyle"];

                ClientDateRow.Visibility = Visibility.Collapsed;
                VisitorTimeRow.Visibility = Visibility.Visible;

                TxtTypeDesc.Text = "🪪  Visitor — brief visit, any floor, within a single day.";
                TypeDescBorder.Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 239, 246, 255));
                TypeDescBorder.BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 191, 219, 254));
                TxtTypeDesc.Foreground = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 29, 78, 216));

                TxtNINLabel.Text = "NIN (optional)";

                // Swap card gradient to blue
                CardBgClient.Visibility = Visibility.Collapsed;
                CardBgVisitor.Visibility = Visibility.Visible;
                TypeBadge.Background = new Windows.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 29, 78, 216));
                TxtTypeBadge.Text = "VISITOR";

                ApplyFloorRestrictions();
                ClearForm();
                UpdatePreviewCard();
            }
        }

        // ── FLOOR RESTRICTIONS ────────────────────────────────────────────────

        private void ApplyFloorRestrictions()
        {
            // Visitors can access all 4 floors; clients only 1st & 2nd
            bool showAll = _isVisitorMode;
            Floor3Item.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
            Floor4Item.Visibility = showAll ? Visibility.Visible : Visibility.Collapsed;

            // Reset selection if client mode and floor 3/4 was selected
            if (!showAll && cmbfloor.SelectedIndex >= 2)
                cmbfloor.SelectedIndex = -1;
        }

        // ── LIVE PREVIEW CARD ─────────────────────────────────────────────────

        private void FormField_Changed(object sender, object e)
        {
            UpdatePreviewCard();
        }

        private void UpdatePreviewCard()
        {
            // Name
            string firstName = TxtFirstName?.Text?.Trim() ?? "";
            string lastName = TxtLastName?.Text?.Trim() ?? "";
            string fullName = (firstName + " " + lastName).Trim();

            if (string.IsNullOrEmpty(fullName)) fullName = "Full Name";

            // Initials
            string initials = "?";
            if (firstName.Length > 0)
            {
                initials = lastName.Length > 0
                    ? $"{firstName[0]}{lastName[0]}".ToUpper()
                    : $"{firstName[0]}".ToUpper();
            }

            CardFullName.Text = fullName;
            CardInitials.Text = initials;

            // Email
            string email = TxtEmail?.Text?.Trim() ?? "";
            CardEmail.Text = string.IsNullOrEmpty(email) ? "email@domain.com" : email;

            // Phone
            string phone = TxtPhone?.Text?.Trim() ?? "";
            CardPhone.Text = string.IsNullOrEmpty(phone) ? "—" : phone;

            // Floor
            int floorIdx = cmbfloor?.SelectedIndex ?? -1;
            CardFloor.Text = floorIdx >= 0 && floorIdx < FloorNames.Length
                ? FloorNames[floorIdx] : "—";

            // NIN
            string nin = TxtNIN?.Text?.Trim() ?? "";
            CardNIN.Text = string.IsNullOrEmpty(nin) ? "—" : nin;

            // Date / Time
            if (_isVisitorMode)
            {
                CardDateLabel.Text = "VISIT TIME";
                string timeIn = TxtTimeIn?.Text?.Trim() ?? "";
                string timeOut = TxtTimeOut?.Text?.Trim() ?? "";
                string timeStr = string.IsNullOrEmpty(timeIn) ? "—" : timeIn;
                if (!string.IsNullOrEmpty(timeOut)) timeStr += $"  →  {timeOut}";
                CardDateValue.Text = timeStr;
            }
            else
            {
                CardDateLabel.Text = "ACCESS PERIOD";
                string start = DpStart?.Date?.ToString("dd MMM yyyy") ?? "—";
                string end = DpEnd?.Date?.ToString("dd MMM yyyy") ?? "—";
                CardDateValue.Text = $"{start}  –  {end}";
            }
        }

        // ── LOAD CLIENTS ──────────────────────────────────────────────────────

        private async Task LoadClientsAsync()
        {
            try
            {
                string url = App.SupabaseUrl +
                             "/rest/v1/client" +
                             "?select=client_id,first_name,last_name,email,phone_number,nin,floor_id,start_date,end_date," +
                             "floors!client_floor_id_fkey(floor_id,floor_name)" +
                             "&order=client_id.desc";

                HttpResponseMessage response = await _http.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Supabase error: " + json);

                _clients.Clear();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    foreach (JsonElement row in doc.RootElement.EnumerateArray())
                    {
                        JsonElement floor = default;
                        if (row.TryGetProperty("floors", out JsonElement fl) &&
                            fl.ValueKind != JsonValueKind.Null)
                        {
                            floor = fl.ValueKind == JsonValueKind.Array
                                ? (fl.GetArrayLength() > 0 ? fl[0] : default) : fl;
                        }

                        string floorName = GetStr(floor, "floor_name") ?? "Unknown";
                        int? floorId = GetNullableInt(row, "floor_id");

                        DateTime startDate = DateTime.TryParse(GetStr(row, "start_date") ?? "", out DateTime sd)
                            ? sd : DateTime.Now;
                        DateTime endDate = DateTime.TryParse(GetStr(row, "end_date") ?? "", out DateTime ed)
                            ? ed : DateTime.Now;

                        _clients.Add(new ClientDisplayModel
                        {
                            ClientID = GetInt(row, "client_id"),
                            FullName = (GetStr(row, "first_name") + " " + GetStr(row, "last_name")).Trim(),
                            Email = GetStr(row, "email") ?? "",
                            NIN = GetStr(row, "nin") ?? "",
                            FloorName = floorName,
                            FloorID = floorId,
                            StartDate = startDate,
                            EndDate = endDate
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Client load error: " + ex.Message);
            }
        }

        // ── LOAD VISITORS ─────────────────────────────────────────────────────

        private async Task LoadVisitorsAsync()
        {
            try
            {
                string url = App.SupabaseUrl +
                             "/rest/v1/visitors" +
                             "?select=visitor_id,first_name,last_name,email,phone_number,nin,floor_id,visit_date,time_in,time_out," +
                             "floors!visitors_floor_id_fkey(floor_id,floor_name)" +
                             "&order=visitor_id.desc";

                HttpResponseMessage response = await _http.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Supabase error: " + json);

                _visitors.Clear();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    foreach (JsonElement row in doc.RootElement.EnumerateArray())
                    {
                        JsonElement floor = default;
                        if (row.TryGetProperty("floors", out JsonElement fl) &&
                            fl.ValueKind != JsonValueKind.Null)
                        {
                            floor = fl.ValueKind == JsonValueKind.Array
                                ? (fl.GetArrayLength() > 0 ? fl[0] : default) : fl;
                        }

                        _visitors.Add(new VisitorDisplayModel
                        {
                            VisitorID = GetInt(row, "visitor_id"),
                            FullName = (GetStr(row, "first_name") + " " + GetStr(row, "last_name")).Trim(),
                            Email = GetStr(row, "email") ?? "",
                            NIN = GetStr(row, "nin") ?? "",
                            FloorName = GetStr(floor, "floor_name") ?? "Unknown",
                            FloorID = GetNullableInt(row, "floor_id"),
                            VisitDate = GetStr(row, "visit_date") ?? "",
                            TimeIn = GetStr(row, "time_in") ?? "",
                            TimeOut = GetStr(row, "time_out") ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Visitor load error: " + ex.Message);
            }
        }

        // ── SAVE (INSERT or UPDATE) ───────────────────────────────────────────

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isVisitorMode)
                await SaveVisitorAsync();
            else
                await SaveClientAsync();
        }

        private async Task SaveClientAsync()
        {
            string firstName = TxtFirstName.Text.Trim();
            string lastName = TxtLastName.Text.Trim();
            string email = TxtEmail.Text.Trim();
            string phone = TxtPhone.Text.Trim();
            string nin = TxtNIN.Text.Trim();
            int floorId = cmbfloor.SelectedIndex + 1;

            if (string.IsNullOrEmpty(firstName)) { await ShowMessageAsync("First Name is required."); return; }
            if (string.IsNullOrEmpty(phone)) { await ShowMessageAsync("Phone Number is required."); return; }
            if (string.IsNullOrEmpty(nin)) { await ShowMessageAsync("NIN is required for clients."); return; }
            if (cmbfloor.SelectedIndex == -1) { await ShowMessageAsync("Please select a floor."); return; }
            if (cmbfloor.SelectedIndex >= 2) { await ShowMessageAsync("Clients can only be assigned to Floor 1 or Floor 2."); return; }

            string startDate = DpStart.Date?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd");
            string endDate = DpEnd.Date?.ToString("yyyy-MM-dd");

            var payload = new Dictionary<string, object>
            {
                ["first_name"] = firstName,
                ["last_name"] = lastName,
                ["email"] = email,
                ["phone_number"] = phone,
                ["nin"] = nin,
                ["floor_id"] = floorId,
                ["start_date"] = startDate
            };
            if (endDate != null) payload["end_date"] = endDate;

            string body = JsonSerializer.Serialize(payload);

            try
            {
                HttpResponseMessage response;

                if (_editingClientId.HasValue)
                {
                    string url = App.SupabaseUrl + "/rest/v1/client?client_id=eq." + _editingClientId.Value;
                    var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("Prefer", "return=minimal");
                    response = await _http.SendAsync(req);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception(await response.Content.ReadAsStringAsync());

                    await ShowMessageAsync("Client updated successfully.");
                }
                else
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, App.SupabaseUrl + "/rest/v1/client")
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("Prefer", "return=representation");
                    response = await _http.SendAsync(req);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception(await response.Content.ReadAsStringAsync());

                    string responseJson = await response.Content.ReadAsStringAsync();
                    int newClientId = 0;
                    using (JsonDocument doc = JsonDocument.Parse(responseJson))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement inserted = root.ValueKind == JsonValueKind.Array ? root[0] : root;
                        newClientId = GetInt(inserted, "client_id");
                    }

                    if (newClientId > 0)
                        await CreateAccessLogAsync(newClientId);

                    await ShowMessageAsync("Client registered and signed in successfully.");
                }

                ClearForm();
                await LoadClientsAsync();
                rootPivot.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Save failed: " + ex.Message);
            }
        }

        private async Task SaveVisitorAsync()
        {
            string firstName = TxtFirstName.Text.Trim();
            string lastName = TxtLastName.Text.Trim();
            string email = TxtEmail.Text.Trim();
            string phone = TxtPhone.Text.Trim();
            string nin = TxtNIN.Text.Trim();   // optional for visitors
            string timeIn = TxtTimeIn.Text.Trim();
            string timeOut = TxtTimeOut.Text.Trim();
            int floorId = cmbfloor.SelectedIndex + 1;

            if (string.IsNullOrEmpty(firstName)) { await ShowMessageAsync("First Name is required."); return; }
            if (string.IsNullOrEmpty(phone)) { await ShowMessageAsync("Phone Number is required."); return; }
            if (cmbfloor.SelectedIndex == -1) { await ShowMessageAsync("Please select a floor."); return; }
            if (string.IsNullOrEmpty(timeIn)) { await ShowMessageAsync("Time In is required."); return; }

            // Validate time format HH:mm
            if (!TimeSpan.TryParse(timeIn, out _))
            {
                await ShowMessageAsync("Time In must be in HH:MM format (e.g. 09:30).");
                return;
            }
            if (!string.IsNullOrEmpty(timeOut) && !TimeSpan.TryParse(timeOut, out _))
            {
                await ShowMessageAsync("Time Out must be in HH:MM format (e.g. 17:00).");
                return;
            }

            var payload = new Dictionary<string, object>
            {
                ["first_name"] = firstName,
                ["last_name"] = lastName,
                ["email"] = email,
                ["phone_number"] = phone,
                ["floor_id"] = floorId,
                ["visit_date"] = DateTime.Now.ToString("yyyy-MM-dd"),
                ["time_in"] = timeIn
            };
            if (!string.IsNullOrEmpty(nin)) payload["nin"] = nin;
            if (!string.IsNullOrEmpty(timeOut)) payload["time_out"] = timeOut;

            string body = JsonSerializer.Serialize(payload);

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, App.SupabaseUrl + "/rest/v1/visitors")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("Prefer", "return=minimal");

                HttpResponseMessage response = await _http.SendAsync(req);
                if (!response.IsSuccessStatusCode)
                    throw new Exception(await response.Content.ReadAsStringAsync());

                await ShowMessageAsync("Visitor registered successfully.");
                ClearForm();
                await LoadVisitorsAsync();
                rootPivot.SelectedIndex = 2;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Visitor save failed: " + ex.Message);
            }
        }

        // ── ACCESS LOG (auto sign-in for new clients) ─────────────────────────

        private async Task CreateAccessLogAsync(int clientId)
        {
            try
            {
                var logPayload = new Dictionary<string, object>
                {
                    ["client_id"] = clientId,
                    ["access_date"] = DateTime.Now.ToString("yyyy-MM-dd"),
                    ["time_in"] = DateTime.Now.ToString("HH:mm:ss"),
                    ["status"] = "Signed In"
                };

                string logBody = JsonSerializer.Serialize(logPayload);
                var logReq = new HttpRequestMessage(HttpMethod.Post, App.SupabaseUrl + "/rest/v1/access_logs")
                {
                    Content = new StringContent(logBody, Encoding.UTF8, "application/json")
                };
                logReq.Headers.Add("Prefer", "return=minimal");

                HttpResponseMessage logResp = await _http.SendAsync(logReq);
                if (!logResp.IsSuccessStatusCode)
                    System.Diagnostics.Debug.WriteLine("Access log creation failed: " +
                        await logResp.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Access log error: " + ex.Message);
            }
        }

        // ── EDIT CLIENT ───────────────────────────────────────────────────────

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                ClientDisplayModel client = null;
                foreach (var c in _clients)
                    if (c.ClientID == id) { client = c; break; }

                if (client == null) return;

                // Switch to client mode first
                if (_isVisitorMode) BtnTypeClient_Click(null, null);

                string[] parts = client.FullName.Split(new[] { ' ' }, 2);
                TxtFirstName.Text = parts[0];
                TxtLastName.Text = parts.Length > 1 ? parts[1] : "";
                TxtEmail.Text = client.Email ?? "";
                TxtNIN.Text = client.NIN ?? "";

                cmbfloor.SelectedIndex = client.FloorID.HasValue ? client.FloorID.Value - 1 : -1;
                DpStart.Date = new DateTimeOffset(client.StartDate);
                DpEnd.Date = new DateTimeOffset(client.EndDate);

                _editingClientId = id;
                rootPivot.SelectedIndex = 0;
                UpdatePreviewCard();
            }
        }

        // ── DELETE CLIENT ─────────────────────────────────────────────────────

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                try
                {
                    HttpResponseMessage response = await _http.DeleteAsync(
                        App.SupabaseUrl + "/rest/v1/client?client_id=eq." + id);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception(await response.Content.ReadAsStringAsync());

                    await LoadClientsAsync();
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Delete failed: " + ex.Message);
                }
            }
        }

        // ── DELETE VISITOR ────────────────────────────────────────────────────

        private async void btnDeleteVisitor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                try
                {
                    HttpResponseMessage response = await _http.DeleteAsync(
                        App.SupabaseUrl + "/rest/v1/visitors?visitor_id=eq." + id);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception(await response.Content.ReadAsStringAsync());

                    await LoadVisitorsAsync();
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Visitor delete failed: " + ex.Message);
                }
            }
        }

        // ── CLEAR FORM ────────────────────────────────────────────────────────

        private void ClearForm()
        {
            TxtFirstName.Text = "";
            TxtLastName.Text = "";
            TxtEmail.Text = "";
            TxtPhone.Text = "";
            TxtNIN.Text = "";
            cmbfloor.SelectedIndex = -1;
            DpStart.Date = DateTimeOffset.Now;
            DpEnd.Date = DateTimeOffset.Now.AddDays(30);
            TxtTimeIn.Text = DateTime.Now.ToString("HH:mm");
            TxtTimeOut.Text = "";
            _editingClientId = null;
            UpdatePreviewCard();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

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

        private static int? GetNullableInt(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : (int?)null;
        }

        // ── NAVIGATION ────────────────────────────────────────────────────────

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(dashboard));
        }

        private void NavRegister_Click(object sender, RoutedEventArgs e) { /* already here */ }

        private void NavLogout_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOut));
        }

        private void generalreportsbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Reports));
        }

        private void backbtnentry_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(dashboard));
        }

        private void NavReports_Click(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(Reports)); }
        private void NavLogs_Click(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(SystemLogs)); }
        private void NavReceptionists_Click(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(UsersReceptionist)); }
        private void NavSettings_Click(object sender, RoutedEventArgs e) { Frame.Navigate(typeof(Settings)); }
    }
}