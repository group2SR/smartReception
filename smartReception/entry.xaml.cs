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
    public sealed partial class entry : Page
    {
        private static readonly HttpClient _http = new HttpClient();

        private readonly ObservableCollection<ClientDisplayModel> _clients =
            new ObservableCollection<ClientDisplayModel>();

        private int? _editingClientId = null;

        public entry()
        {
            this.InitializeComponent();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            ClientsListView.ItemsSource = _clients;

            DpStart.Date = DateTimeOffset.Now;
            DpEnd.Date = DateTimeOffset.Now.AddDays(30);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadClientsAsync();
        }

        // â”€â”€ LOAD â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                                ? (fl.GetArrayLength() > 0 ? fl[0] : default)
                                : fl;
                        }

                        string floorName = GetStr(floor, "floor_name") ?? "Unknown";
                        int? floorId = GetNullableInt(row, "floor_id");

                        string rawStart = GetStr(row, "start_date") ?? "";
                        string rawEnd = GetStr(row, "end_date") ?? "";

                        DateTime startDate = DateTime.TryParse(rawStart, out DateTime sd) ? sd : DateTime.Now;
                        DateTime endDate = DateTime.TryParse(rawEnd, out DateTime ed) ? ed : DateTime.Now;

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
                await ShowMessageAsync("Load error: " + ex.Message);
            }
        }

        // â”€â”€ SAVE (INSERT or UPDATE) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string firstName = TxtFirstName.Text.Trim();
            string lastName = TxtLastName.Text.Trim();
            string email = TxtEmail.Text.Trim();
            string phone = TxtPhone.Text.Trim();
            string nin = TxtNIN.Text.Trim();
            int floorId = cmbfloor.SelectedIndex + 1;

            if (string.IsNullOrEmpty(firstName))
            {
                await ShowMessageAsync("First Name is required.");
                return;
            }
            if (string.IsNullOrEmpty(email))
            {
                await ShowMessageAsync("Email is required.");
                return;
            }
            if (string.IsNullOrEmpty(phone))
            {
                await ShowMessageAsync("Phone Number is required.");
                return;
            }
            if (cmbfloor.SelectedIndex == -1)
            {
                await ShowMessageAsync("Please select a floor.");
                return;
            }

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
                    // â”€â”€ UPDATE existing client â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    string url = App.SupabaseUrl +
                                 "/rest/v1/client?client_id=eq." + _editingClientId.Value;

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("Prefer", "return=minimal");
                    response = await _http.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        throw new Exception(err);
                    }

                    await ShowMessageAsync("Client updated successfully.");
                }
                else
                {
                    // â”€â”€ INSERT new client and get back the new client_id â”€â”€â”€
                    string url = App.SupabaseUrl + "/rest/v1/client";
                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    // return=representation so we get the inserted row with its client_id
                    request.Headers.Add("Prefer", "return=representation");
                    response = await _http.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        throw new Exception(err);
                    }

                    // â”€â”€ Parse the new client_id from the response â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    string responseJson = await response.Content.ReadAsStringAsync();
                    int newClientId = 0;

                    using (JsonDocument doc = JsonDocument.Parse(responseJson))
                    {
                        // Response is an array with one element
                        JsonElement root = doc.RootElement;
                        JsonElement inserted = root.ValueKind == JsonValueKind.Array
                            ? root[0] : root;

                        newClientId = GetInt(inserted, "client_id");
                    }

                    // â”€â”€ Auto sign-in: create access_log entry â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                    if (newClientId > 0)
                    {
                        await CreateAccessLogAsync(newClientId);
                    }

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

        // â”€â”€ AUTO SIGN-IN â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                string logUrl = App.SupabaseUrl + "/rest/v1/access_logs";

                var logRequest = new HttpRequestMessage(HttpMethod.Post, logUrl)
                {
                    Content = new StringContent(logBody, Encoding.UTF8, "application/json")
                };
                logRequest.Headers.Add("Prefer", "return=minimal");

                HttpResponseMessage logResponse = await _http.SendAsync(logRequest);

                if (!logResponse.IsSuccessStatusCode)
                {
                    string err = await logResponse.Content.ReadAsStringAsync();
                    // Don't block the user â€” just log the warning
                    System.Diagnostics.Debug.WriteLine("Access log creation failed: " + err);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Access log error: " + ex.Message);
            }
        }

        // â”€â”€ EDIT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                ClientDisplayModel client = null;
                foreach (var c in _clients)
                    if (c.ClientID == id) { client = c; break; }

                if (client == null) return;

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
            }
        }

        // â”€â”€ DELETE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is int id)
            {
                try
                {
                    string url = App.SupabaseUrl + "/rest/v1/client?client_id=eq." + id;
                    HttpResponseMessage response = await _http.DeleteAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync();
                        throw new Exception(err);
                    }

                    await LoadClientsAsync();
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("Delete failed: " + ex.Message);
                }
            }
        }

        // â”€â”€ HELPERS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            _editingClientId = null;
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

        private static int? GetNullableInt(JsonElement el, string prop)
        {
            return el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : (int?)null;
        }

        // â”€â”€ NAVIGATION â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void generalreportsbtn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Reports));
        }

        private void backbtnentry_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(dashboard));
            }
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(dashboard));
            }
        }

        private void NavRegister_Click(object sender, RoutedEventArgs e)
        {
            // Already on register clients page
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
    }
}