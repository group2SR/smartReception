using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace smartReception
{
    public sealed partial class Reports : Page
    {
        private readonly HttpClient _http;

        private const int PAGE_SIZE = 15;
        private int _currentPage = 1;
        private int _totalCount = 0;
        private List<ReportRow> _allRows = new List<ReportRow>();

        // floor_id (int key as string) → floor_name
        private Dictionary<string, string> _floorMap = new Dictionary<string, string>();

        public Reports()
        {
            this.InitializeComponent();

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("apikey", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", App.SupabaseAnonKey);
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadFloorMapAsync();
            await LoadStatisticsAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FLOOR MAP
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadFloorMapAsync()
        {
            try
            {
                string url = App.SupabaseUrl + "/rest/v1/floors?select=floor_id,floor_name";
                string json = await _http.GetStringAsync(url);

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    foreach (JsonElement row in doc.RootElement.EnumerateArray())
                    {
                        string id = row.TryGetProperty("floor_id", out JsonElement fid)
                            && fid.ValueKind == JsonValueKind.Number
                            ? fid.GetInt32().ToString()
                            : GetStr(row, "floor_id") ?? "";

                        string name = GetStr(row, "floor_name") ?? "Floor " + id;
                        if (!string.IsNullOrEmpty(id))
                            _floorMap[id] = name;
                    }
                }
            }
            catch { /* non-critical — fallback names used */ }

            // Fallback: ensure all 4 floors exist in the map
            if (!_floorMap.ContainsKey("1")) _floorMap["1"] = "1st Floor";
            if (!_floorMap.ContainsKey("2")) _floorMap["2"] = "2nd Floor";
            if (!_floorMap.ContainsKey("3")) _floorMap["3"] = "3rd Floor";
            if (!_floorMap.ContainsKey("4")) _floorMap["4"] = "4th Floor";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATISTICS CARDS
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadStatisticsAsync()
        {
            try
            {
                // Total clients (long-term)
                int totalClients = await GetCountAsync("/rest/v1/client");

                // Deleted clients
                int deleted = await GetCountAsync("/rest/v1/deleted_clients");

                // Active clients = total - deleted
                int active = Math.Max(0, totalClients - deleted);

                // Today's logins = client access logs + visitor entries today
                string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                int clientLogins = await GetCountAsync("/rest/v1/access_logs?access_date=eq." + today);
                int visitorLogins = await GetCountAsync("/rest/v1/visitors?visit_date=eq." + today);
                int todayLogins = clientLogins + visitorLogins;

                // Receptionists
                int receptionists = await GetCountAsync("/rest/v1/system_users?role_id=eq.2");

                // Blocked users
                int blocked = await GetCountAsync("/rest/v1/system_users?is_blocked=eq.true");

                TotalClientsText.Text = totalClients.ToString();
                ActiveClientsText.Text = active.ToString();
                DeletedClientsText.Text = deleted.ToString();
                TodayLoginsText.Text = todayLogins.ToString();
                ReceptionistsText.Text = receptionists.ToString();
                BlockedUsersText.Text = blocked.ToString();
            }
            catch (Exception ex)
            {
                ShowStatus("Could not load statistics: " + ex.Message, isError: true);
            }
        }

        private async Task<int> GetCountAsync(string path)
        {
            try
            {
                string url = App.SupabaseUrl + path;
                if (!url.Contains("select="))
                    url += (url.Contains("?") ? "&" : "?") + "select=*";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Prefer", "count=exact");
                HttpResponseMessage resp = await _http.SendAsync(req);

                IEnumerable<string> vals;
                if (resp.Headers.TryGetValues("Content-Range", out vals))
                {
                    foreach (string raw in vals)
                    {
                        string[] parts = raw.Split('/');
                        int count;
                        if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out count))
                            return count;
                        break;
                    }
                }

                string body = await resp.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        return doc.RootElement.GetArrayLength();
                }
            }
            catch { }

            return 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GENERATE REPORT
        // ══════════════════════════════════════════════════════════════════════

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            ShowStatus("Generating report...", isError: false);
            GenerateReportBtn.IsEnabled = false;

            try
            {
                string reportType = (ReportTypeCombo.SelectedItem as ComboBoxItem) != null
                    ? (ReportTypeCombo.SelectedItem as ComboBoxItem).Content.ToString() : "";

                string floorLabel = (FloorCombo.SelectedItem as ComboBoxItem) != null
                    ? (FloorCombo.SelectedItem as ComboBoxItem).Content.ToString() : "All Floors";

                DateTimeOffset? start = StartDatePicker.Date;
                DateTimeOffset? end = EndDatePicker.Date;

                // Resolve floor label → floor_id key
                string filterFloorId = null;
                if (floorLabel != "All Floors")
                {
                    foreach (var kv in _floorMap)
                    {
                        if (kv.Value == floorLabel) { filterFloorId = kv.Key; break; }
                    }
                }

                if (reportType == "Access Logs Report")
                {
                    var clientRows = await FetchAccessLogsAsync(filterFloorId, start, end);
                    var visitorRows = await FetchVisitorsAsync(filterFloorId, start, end);

                    // Merge both, sort by date then time descending
                    _allRows = clientRows
                        .Concat(visitorRows)
                        .OrderByDescending(r => r.AccessDate)
                        .ThenByDescending(r => r.TimeIn)
                        .ToList();
                }
                else if (reportType == "Deleted Clients")
                {
                    _allRows = await FetchDeletedClientsAsync();
                }
                else if (reportType == "Blocked Users")
                {
                    _allRows = await FetchBlockedUsersAsync();
                }
                else
                {
                    _allRows = new List<ReportRow>();
                }

                _totalCount = _allRows.Count;
                _currentPage = 1;
                RenderPage();
                ShowStatus("Report generated — " + _totalCount + " record(s) found.", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus("Error: " + ex.Message, isError: true);
            }
            finally
            {
                GenerateReportBtn.IsEnabled = true;
            }
        }

        // ── Access Logs (long-term clients) ───────────────────────────────────

        private async Task<List<ReportRow>> FetchAccessLogsAsync(
            string filterFloorId, DateTimeOffset? start, DateTimeOffset? end)
        {
            // Fetch clients to resolve names and floors
            string clientUrl = App.SupabaseUrl +
                "/rest/v1/client?select=client_id,first_name,last_name,floor_id&order=client_id.desc";
            string clientJson = await _http.GetStringAsync(clientUrl);

            var clientNames = new Dictionary<int, string>();
            var clientFloors = new Dictionary<int, string>();

            using (JsonDocument cdoc = JsonDocument.Parse(clientJson))
            {
                foreach (JsonElement row in cdoc.RootElement.EnumerateArray())
                {
                    int id = GetInt(row, "client_id");
                    string name = (GetStr(row, "first_name") + " " +
                                   GetStr(row, "last_name")).Trim();
                    string fid = row.TryGetProperty("floor_id", out JsonElement fv)
                                 && fv.ValueKind == JsonValueKind.Number
                                 ? fv.GetInt32().ToString() : null;
                    clientNames[id] = name;
                    clientFloors[id] = fid;
                }
            }

            // Build access_logs query
            string logUrl = App.SupabaseUrl +
                "/rest/v1/access_logs?select=log_id,client_id,access_date,time_in,time_out,status" +
                "&order=log_id.desc";

            if (start.HasValue)
                logUrl += "&access_date=gte." + start.Value.ToString("yyyy-MM-dd");
            if (end.HasValue)
                logUrl += "&access_date=lte." + end.Value.ToString("yyyy-MM-dd");

            string logJson = await _http.GetStringAsync(logUrl);
            var rows = new List<ReportRow>();

            using (JsonDocument ldoc = JsonDocument.Parse(logJson))
            {
                foreach (JsonElement row in ldoc.RootElement.EnumerateArray())
                {
                    int clientId = GetInt(row, "client_id");

                    string floorId;
                    clientFloors.TryGetValue(clientId, out floorId);

                    // Apply floor filter
                    if (filterFloorId != null && floorId != filterFloorId)
                        continue;

                    string clientName;
                    if (!clientNames.TryGetValue(clientId, out clientName))
                        clientName = "Client #" + clientId;

                    string floorName;
                    if (floorId == null || !_floorMap.TryGetValue(floorId, out floorName))
                        floorName = "--";

                    string timeOut = GetStr(row, "time_out");

                    rows.Add(new ReportRow
                    {
                        LogId = GetInt(row, "log_id").ToString(),
                        ClientName = clientName,
                        FloorTarget = floorName,
                        AccessDate = GetStr(row, "access_date") ?? "--",
                        TimeIn = GetStr(row, "time_in") ?? "--",
                        TimeOut = string.IsNullOrEmpty(timeOut) ? "--" : timeOut,
                        Status = GetStr(row, "status") ?? "--",
                        RowType = "Client"
                    });
                }
            }

            return rows;
        }

        // ── Visitors (short-term, all 4 floors) ───────────────────────────────

        private async Task<List<ReportRow>> FetchVisitorsAsync(
            string filterFloorId, DateTimeOffset? start, DateTimeOffset? end)
        {
            string url = App.SupabaseUrl +
                "/rest/v1/visitors" +
                "?select=visitor_id,first_name,last_name,floor_id,visit_date,time_in,time_out" +
                "&order=visitor_id.desc";

            if (start.HasValue)
                url += "&visit_date=gte." + start.Value.ToString("yyyy-MM-dd");
            if (end.HasValue)
                url += "&visit_date=lte." + end.Value.ToString("yyyy-MM-dd");

            // Visitors: floor filter applied server-side
            if (filterFloorId != null)
                url += "&floor_id=eq." + filterFloorId;

            string json = await _http.GetStringAsync(url);
            var rows = new List<ReportRow>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    string floorId = row.TryGetProperty("floor_id", out JsonElement fv)
                                     && fv.ValueKind == JsonValueKind.Number
                                     ? fv.GetInt32().ToString() : null;

                    string floorName;
                    if (floorId == null || !_floorMap.TryGetValue(floorId, out floorName))
                        floorName = "--";

                    string timeOut = GetStr(row, "time_out");

                    rows.Add(new ReportRow
                    {
                        LogId = "V-" + GetInt(row, "visitor_id"),
                        ClientName = (GetStr(row, "first_name") + " " +
                                       GetStr(row, "last_name")).Trim(),
                        FloorTarget = floorName,
                        AccessDate = GetStr(row, "visit_date") ?? "--",
                        TimeIn = GetStr(row, "time_in") ?? "--",
                        TimeOut = string.IsNullOrEmpty(timeOut) ? "--" : timeOut,
                        Status = string.IsNullOrEmpty(timeOut) ? "Active" : "Completed",
                        RowType = "Visitor"
                    });
                }
            }

            return rows;
        }

        // ── Deleted Clients ───────────────────────────────────────────────────

        private async Task<List<ReportRow>> FetchDeletedClientsAsync()
        {
            string url = App.SupabaseUrl +
                "/rest/v1/deleted_clients" +
                "?select=deleted_client_id,original_client_id,first_name,last_name,floor_id,deleted_date,reason" +
                "&order=deleted_client_id.desc";

            string json = await _http.GetStringAsync(url);
            var rows = new List<ReportRow>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    string floorId = row.TryGetProperty("floor_id", out JsonElement fv)
                                     && fv.ValueKind == JsonValueKind.Number
                                     ? fv.GetInt32().ToString() : null;

                    string floorName;
                    if (floorId == null || !_floorMap.TryGetValue(floorId, out floorName))
                        floorName = "--";

                    string deletedDate = GetStr(row, "deleted_date");
                    if (deletedDate != null && deletedDate.Length >= 10)
                        deletedDate = deletedDate.Substring(0, 10);

                    rows.Add(new ReportRow
                    {
                        LogId = GetInt(row, "original_client_id").ToString(),
                        ClientName = (GetStr(row, "first_name") + " " +
                                       GetStr(row, "last_name")).Trim(),
                        FloorTarget = floorName,
                        AccessDate = deletedDate ?? "--",
                        TimeIn = "--",
                        TimeOut = "--",
                        Status = "Deleted",
                        RowType = "Client"
                    });
                }
            }

            return rows;
        }

        // ── Blocked Users ─────────────────────────────────────────────────────

        private async Task<List<ReportRow>> FetchBlockedUsersAsync()
        {
            string url = App.SupabaseUrl +
                "/rest/v1/system_users" +
                "?select=user_id,first_name,last_name,username,role_id" +
                "&is_blocked=eq.true&order=user_id.desc";

            string json = await _http.GetStringAsync(url);
            var rows = new List<ReportRow>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    rows.Add(new ReportRow
                    {
                        LogId = GetInt(row, "user_id").ToString(),
                        ClientName = (GetStr(row, "first_name") + " " +
                                       GetStr(row, "last_name")).Trim(),
                        FloorTarget = "--",
                        AccessDate = "--",
                        TimeIn = "--",
                        TimeOut = "--",
                        Status = "Blocked",
                        RowType = "Staff"
                    });
                }
            }

            return rows;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PAGINATION
        // ══════════════════════════════════════════════════════════════════════

        private void RenderPage()
        {
            int totalPages = _totalCount == 0 ? 1
                : (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE);

            int startIdx = (_currentPage - 1) * PAGE_SIZE;
            int endIdx = Math.Min(startIdx + PAGE_SIZE, _totalCount);

            var pageItems = new List<ReportRow>();
            for (int i = startIdx; i < endIdx; i++)
                pageItems.Add(_allRows[i]);

            ReportListView.ItemsSource = pageItems;

            PaginationInfoText.Text = _totalCount == 0
                ? "No records found"
                : string.Format("Showing {0} to {1} of {2} record(s)",
                    startIdx + 1, endIdx, _totalCount);

            CurrentPageBtn.Content = _currentPage.ToString();
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < totalPages;
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1) { _currentPage--; RenderPage(); }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE);
            if (_currentPage < totalPages) { _currentPage++; RenderPage(); }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EXPORT — PDF
        // ══════════════════════════════════════════════════════════════════════

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_allRows.Count == 0)
            {
                await ShowMessageAsync("Generate a report first.");
                return;
            }

            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "Report_" + DateTime.Now.ToString("yyyyMMdd_HHmm")
                };
                picker.FileTypeChoices.Add("PDF Document", new List<string> { ".pdf" });
                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                Stream stream = await file.OpenStreamForWriteAsync();

                var doc = new iTextSharp.text.Document(
                    iTextSharp.text.PageSize.A4.Rotate(), 20, 20, 30, 30);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, stream);
                doc.Open();

                var boldFont = iTextSharp.text.FontFactory.GetFont("Helvetica-Bold", 14);
                var subFont = iTextSharp.text.FontFactory.GetFont("Helvetica", 9,
                    new iTextSharp.text.BaseColor(100, 116, 139));
                var cellFont = iTextSharp.text.FontFactory.GetFont("Helvetica", 9);
                var hdrFont = iTextSharp.text.FontFactory.GetFont("Helvetica-Bold", 9,
                    new iTextSharp.text.BaseColor(255, 255, 255));
                var typeFont = iTextSharp.text.FontFactory.GetFont("Helvetica-Bold", 8,
                    new iTextSharp.text.BaseColor(255, 255, 255));

                doc.Add(new iTextSharp.text.Paragraph(
                    "smartReception — Report Export", boldFont));
                doc.Add(new iTextSharp.text.Paragraph(
                    "Generated: " + DateTime.Now.ToString("dd MMM yyyy  HH:mm"), subFont));
                doc.Add(new iTextSharp.text.Paragraph(" "));

                // 8 columns now — added Type
                var table = new iTextSharp.text.pdf.PdfPTable(8) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 1f, 1f, 2.5f, 1.5f, 1.5f, 1.2f, 1.2f, 1.2f });

                var darkBg = new iTextSharp.text.BaseColor(15, 23, 42);
                var altBg = new iTextSharp.text.BaseColor(248, 250, 252);

                string[] headers = {
                    "TYPE", "LOG ID", "NAME", "FLOOR",
                    "DATE", "TIME IN", "TIME OUT", "STATUS"
                };

                foreach (string h in headers)
                {
                    var cell = new iTextSharp.text.pdf.PdfPCell(
                        new iTextSharp.text.Phrase(h, hdrFont));
                    cell.BackgroundColor = darkBg;
                    cell.Padding = 6;
                    cell.BorderColor = new iTextSharp.text.BaseColor(255, 255, 255);
                    table.AddCell(cell);
                }

                bool alt = false;
                foreach (ReportRow row in _allRows)
                {
                    var bg = alt ? altBg : new iTextSharp.text.BaseColor(255, 255, 255);

                    // Type badge cell
                    bool isVisitor = row.RowType == "Visitor";
                    var typeBg = isVisitor
                        ? new iTextSharp.text.BaseColor(29, 78, 216)
                        : new iTextSharp.text.BaseColor(4, 120, 87);
                    var typeCell = new iTextSharp.text.pdf.PdfPCell(
                        new iTextSharp.text.Phrase(row.RowType ?? "--", typeFont));
                    typeCell.BackgroundColor = typeBg;
                    typeCell.Padding = 5;
                    typeCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                    table.AddCell(typeCell);

                    // Remaining data cells
                    string[] vals = {
                        row.LogId, row.ClientName, row.FloorTarget,
                        row.AccessDate, row.TimeIn, row.TimeOut, row.Status
                    };
                    foreach (string val in vals)
                    {
                        var cell = new iTextSharp.text.pdf.PdfPCell(
                            new iTextSharp.text.Phrase(val ?? "--", cellFont));
                        cell.BackgroundColor = bg;
                        cell.Padding = 5;
                        table.AddCell(cell);
                    }

                    alt = !alt;
                }

                doc.Add(table);
                doc.Close();
                stream.Dispose();

                ShowStatus("PDF exported — " + _allRows.Count + " record(s).", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus("PDF export failed: " + ex.Message, isError: true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EXPORT — EXCEL
        // ══════════════════════════════════════════════════════════════════════

        private async void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allRows.Count == 0)
            {
                await ShowMessageAsync("Generate a report first.");
                return;
            }

            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "Report_" + DateTime.Now.ToString("yyyyMMdd_HHmm")
                };
                picker.FileTypeChoices.Add("Excel Workbook", new List<string> { ".xlsx" });
                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                var wb = new ClosedXML.Excel.XLWorkbook();
                var ws = wb.Worksheets.Add("Report");

                string[] headers = {
                    "Type", "Log ID", "Name", "Floor",
                    "Date", "Time In", "Time Out", "Status"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor =
                        ClosedXML.Excel.XLColor.FromHtml("#0F172A");
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal =
                        ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                }

                for (int r = 0; r < _allRows.Count; r++)
                {
                    ReportRow row = _allRows[r];
                    bool isVisitor = row.RowType == "Visitor";

                    ws.Cell(r + 2, 1).Value = row.RowType ?? "--";
                    ws.Cell(r + 2, 2).Value = row.LogId;
                    ws.Cell(r + 2, 3).Value = row.ClientName;
                    ws.Cell(r + 2, 4).Value = row.FloorTarget;
                    ws.Cell(r + 2, 5).Value = row.AccessDate;
                    ws.Cell(r + 2, 6).Value = row.TimeIn;
                    ws.Cell(r + 2, 7).Value = row.TimeOut;
                    ws.Cell(r + 2, 8).Value = row.Status;

                    // Colour the Type cell — green for Client, blue for Visitor
                    ws.Cell(r + 2, 1).Style.Fill.BackgroundColor = isVisitor
                        ? ClosedXML.Excel.XLColor.FromHtml("#1D4ED8")
                        : ClosedXML.Excel.XLColor.FromHtml("#047857");
                    ws.Cell(r + 2, 1).Style.Font.FontColor =
                        ClosedXML.Excel.XLColor.White;
                    ws.Cell(r + 2, 1).Style.Font.Bold = true;

                    // Alternate row shading for data columns
                    if (r % 2 == 1)
                    {
                        for (int col = 2; col <= 8; col++)
                            ws.Cell(r + 2, col).Style.Fill.BackgroundColor =
                                ClosedXML.Excel.XLColor.FromHtml("#F8FAFC");
                    }
                }

                ws.Columns().AdjustToContents();

                Stream outStream = await file.OpenStreamForWriteAsync();
                wb.SaveAs(outStream);
                outStream.Dispose();
                wb.Dispose();

                ShowStatus("Excel exported — " + _allRows.Count + " record(s).", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus("Excel export failed: " + ex.Message, isError: true);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static string GetStr(JsonElement el, string prop)
        {
            JsonElement v;
            return el.TryGetProperty(prop, out v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() : null;
        }

        private static int GetInt(JsonElement el, string prop)
        {
            JsonElement v;
            return el.TryGetProperty(prop, out v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : 0;
        }

        private void ShowStatus(string msg, bool isError)
        {
            StatusText.Text = msg;
            StatusText.Foreground = isError
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 4, 120, 87));
            StatusText.Visibility = Visibility.Visible;
        }

        private async Task ShowMessageAsync(string message)
        {
            await new MessageDialog(message, "Smart Reception").ShowAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ══════════════════════════════════════════════════════════════════════

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(dashboard)) Frame.Navigate(typeof(dashboard)); }

        private void NavAccessControl_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(Access_Control)) Frame.Navigate(typeof(Access_Control)); }

        private void NavReports_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(Reports)) Frame.Navigate(typeof(Reports)); }

        private void NavLogs_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(SystemLogs)) Frame.Navigate(typeof(SystemLogs)); }

        private void NavReceptionists_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(UsersReceptionist)) Frame.Navigate(typeof(UsersReceptionist)); }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        { if (GetType() != typeof(Settings)) Frame.Navigate(typeof(Settings)); }

        private void NavLogout_Click(object sender, RoutedEventArgs e)
        { Frame.Navigate(typeof(LogOut)); }

        private void SystemsLogsbtn_Click(object sender, RoutedEventArgs e)
        { Frame.Navigate(typeof(SystemLogs)); }

        private void reportsBacktbtn_Click(object sender, RoutedEventArgs e)
        { Frame.Navigate(typeof(entry)); }
    }
}