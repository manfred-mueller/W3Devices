using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace W3Devices
{
    public partial class Form1 : Form
    {
        private const string ApiUrl = "https://api.scalefusion.com/api/v2/devices.json";
        private DateTime lastApiCallTime = DateTime.MinValue;
        private int apiCallCount = 0;
        private const int MaxApiCallsPerMinute = 3;
        private const int MaxApiCallsPerDay = 500;
        private Timer dailyResetTimer;
        private List<DeviceInfo> displayedDevices;
        private string ApiKey;
        private static readonly HttpClient httpClient = new HttpClient();
        private ContextMenuStrip _actionMenu;
        public DeviceInfo devices;
        public string actualGroup = Properties.Resources.AllGroups;
        private bool searchTextBoxHasFocus = false;

        private const string ApiKeyRegistryPath = @"Software\W3Devices";
        private const string ApiKeyRegistryName = "ApiKey";

        private bool IsApiKeySaved()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ApiKeyRegistryPath))
            {
                return key != null && !string.IsNullOrEmpty(key.GetValue(ApiKeyRegistryName)?.ToString());
            }
        }

        private void SaveApiKeyToRegistry(string apiKey)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(ApiKeyRegistryPath))
            {
                key?.SetValue(ApiKeyRegistryName, apiKey);
            }
        }

        private string GetApiKeyFromRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(ApiKeyRegistryPath))
            {
                return key?.GetValue(ApiKeyRegistryName) as string;
            }
        }

        public Form1()
        {
            this.Text = String.Format("{0} {1}", AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Name, Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            InitializeComponent();
            InitializeSearchBox();
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.EditMode = DataGridViewEditMode.EditProgrammatically;
            dataGridView1.CellMouseDoubleClick += DataGridView1_CellDoubleClick;

            // FIX: AllCells measures every row on every layout pass — DisplayedCells is far cheaper
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;

            dailyResetTimer = new Timer();
            dailyResetTimer.Interval = (int)TimeSpan.FromDays(1).TotalMilliseconds;
            dailyResetTimer.Tick += DailyResetTimer_Tick;
            dailyResetTimer.Start();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            string apiKey = GetApiKeyFromRegistry();
            if (!string.IsNullOrEmpty(apiKey))
            {
                ApiKey = apiKey;
            }
        }

        private void DailyResetTimer_Tick(object sender, EventArgs e)
        {
            apiCallCount = 0;
        }

        private void InitializeSearchBox()
        {
            txtSearch.ForeColor = Color.Gray;
            txtSearch.Text = Properties.Resources.SearchText;
            txtSearch.GotFocus += txtSearch_GotFocus;
            txtSearch.LostFocus += txtSearch_LostFocus;
            txtSearch.TextChanged += txtSearch_TextChanged;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            // FIX: Handle empty string too (e.g. after Reload clears the box)
            if (displayedDevices == null) return;

            if (string.IsNullOrWhiteSpace(txtSearch.Text) || txtSearch.Text == Properties.Resources.SearchText)
                dataGridView1.DataSource = displayedDevices;
            else
                ApplySearchFilter(txtSearch.Text, displayedDevices);
        }

        private void txtSearch_GotFocus(object sender, EventArgs e)
        {
            if (!searchTextBoxHasFocus)
            {
                txtSearch.Text = string.Empty;
                txtSearch.ForeColor = Color.Black;
                searchTextBoxHasFocus = true;
            }
        }

        private void txtSearch_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = Properties.Resources.SearchText;
                txtSearch.ForeColor = Color.Gray;
                searchTextBoxHasFocus = false;
            }
            ApplySearchFilter(txtSearch.Text, displayedDevices);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            if (!IsApiKeySaved())
            {
                this.messageLabel.Text = Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData;
                this.messageLabel.Visible = true;
                txtApiKey.Focus();
                return;
            }

            string savedApiKey = GetApiKeyFromRegistry();
            if (!string.IsNullOrEmpty(savedApiKey))
            {
                txtApiKey.Text = savedApiKey;
            }

            // Show cached data immediately so the UI is usable right away
            var cached = FetchDevicesFromCache();
            if (cached.Count > 0)
            {
                SortAndDisplayDevices(cached);
                InitializeGroupNameComboBox();
            }

            // Then refresh from API (always runs, even on first start with empty cache)
            await FetchAndDisplayDevicesAsync();
            new ToolTip().SetToolTip(btnFetchDevices, Properties.Resources.FetchDevicesFromScalefusionCloud);
            new ToolTip().SetToolTip(cmbGroupName, Properties.Resources.ShowDevicesByGroup);
            new ToolTip().SetToolTip(btnPrint, Properties.Resources.SaveDeviceReportToPdf);
            new ToolTip().SetToolTip(btnReload, Properties.Resources.ClearSearchTextAndResetDevices);
            new ToolTip().SetToolTip(btnSaveApiKey, Properties.Resources.SaveAPIKeyToRegistry);
        }

        private async Task<List<DeviceInfo>> FetchDevicesAsync()
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", ApiKey);
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            try
            {
                // Fetch page 1 first to find out how many devices there are
                string firstUrl = $"{ApiUrl}?page=1&per_page=100";
                HttpResponseMessage firstResponse = await httpClient.GetAsync(firstUrl);

                switch (firstResponse.StatusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        MessageBox.Show(Properties.Resources.UnauthorizedAccessPleaseCheckYourAPIKeyAndTryAgain, Properties.Resources.Unauthorized, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return new List<DeviceInfo>();
                    case HttpStatusCode.NotFound:
                        MessageBox.Show(Properties.Resources.ResourceNotFoundPleaseVerifyTheAPIURLAndTryAgain, Properties.Resources.NotFound, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return new List<DeviceInfo>();
                    case HttpStatusCode.NotAcceptable:
                        MessageBox.Show(Properties.Resources.RequestNotAcceptablePleaseCheckYourRequestHeadersAndTryAgain, Properties.Resources.NotAcceptable, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return new List<DeviceInfo>();
                    default:
                        firstResponse.EnsureSuccessStatusCode();
                        break;
                }

                string firstBody = await firstResponse.Content.ReadAsStringAsync();
                List<DeviceInfo> firstBatch = await Task.Run(() => ParseJson(firstBody));

                if (firstBatch.Count < 100)
                    return firstBatch; // Only one page — done

                // More pages exist: fetch them all in parallel
                var allDevices = new List<DeviceInfo>(firstBatch);
                int page = 2;

                while (true)
                {
                    // Launch up to 5 pages concurrently
                    var pageTasks = new List<Task<List<DeviceInfo>>>();
                    for (int i = 0; i < 5; i++)
                    {
                        int p = page + i;
                        pageTasks.Add(Task.Run(async () =>
                        {
                            string url = $"{ApiUrl}?page={p}&per_page=100";
                            HttpResponseMessage r = await httpClient.GetAsync(url);
                            r.EnsureSuccessStatusCode();
                            string body = await r.Content.ReadAsStringAsync();
                            return await Task.Run(() => ParseJson(body));
                        }));
                    }

                    List<DeviceInfo>[] batches = await Task.WhenAll(pageTasks);
                    bool done = false;
                    foreach (var batch in batches)
                    {
                        allDevices.AddRange(batch);
                        if (batch.Count < 100) { done = true; break; }
                    }
                    if (done) break;
                    page += 5;
                }

                return allDevices;
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(Properties.Resources.ErrorFetchingData + $"{ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<DeviceInfo>();
            }
        }

        private void cmbGroupName_SelectedIndexChanged(object sender, EventArgs e)
        {
            dataGridView1.Focus();
            ApplyGroupName();
        }

        private void ApplyGroupName()
        {
            string selectedGroupName = cmbGroupName.SelectedItem.ToString();
            if (selectedGroupName.Equals(Properties.Resources.AllGroups))
            {
                ApplySearchFilter(txtSearch.Text, displayedDevices);
            }
            else
            {
                int index = selectedGroupName.LastIndexOf(" ");
                if (index >= 0)
                    selectedGroupName = selectedGroupName.Substring(0, index);
                ApplySearchFilter(selectedGroupName, displayedDevices);
            }
            actualGroup = selectedGroupName;
        }

        private void ApplySearchFilter(string searchText, List<DeviceInfo> devices)
        {
            if (devices == null) return;

            if (string.IsNullOrWhiteSpace(searchText) || searchText.Equals(Properties.Resources.SearchText))
            {
                dataGridView1.DataSource = devices;
            }
            else
            {
                string lower = searchText.ToLower();
                var filteredDevices = devices.Where(device =>
                    (device.Name?.ToLower().Contains(lower) == true) ||
                    (device.Model?.ToLower().Contains(lower) == true) ||
                    (device.Serial?.ToLower().Contains(lower) == true) ||
                    (device.GroupName?.ToLower().Contains(lower) == true)
                ).ToList();

                dataGridView1.DataSource = filteredDevices;
            }
        }

        private void SortAndDisplayDevices(List<DeviceInfo> devices)
        {
            if (devices == null) return;
            displayedDevices = devices
                .OrderBy(d => d.GroupName)
                .ThenBy(d => d.Name)
                .ToList();
            BindDeviceGrid(displayedDevices);
        }

        private void BindDeviceGrid(List<DeviceInfo> devices)
        {
            // FIX: Suspend AutoSize + layout before binding to prevent O(n*m) GDI remeasuring
            dataGridView1.SuspendLayout();
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            dataGridView1.DataSource = devices;

            dataGridView1.Columns["Id"].Visible = false;
            dataGridView1.Columns["Battery"].Visible = false;
            dataGridView1.Columns["Serial2"].Visible = false;
            dataGridView1.Columns["Charging"].Visible = false;
            dataGridView1.Columns["Location"].Visible = false;
            dataGridView1.Columns["Remarks"].Visible = false;
            dataGridView1.Columns["Repairs"].HeaderCell.ToolTipText = String.Format(Properties.Resources.ADoubleClickOntoTheCellOpensAnEditorNwindowForTheDeviceSRepairsField, Environment.NewLine);

            FormatColumns(dataGridView1);

            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.HeaderText = GetLocalizedHeaderText(column.Name);
                column.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
            }

            dataGridView1.ResumeLayout();
        }

        private string GetLocalizedHeaderText(string columnName)
        {
            switch (columnName)
            {
                case "GroupName":   return Properties.Resources.GroupName;
                case "Model":       return Properties.Resources.Model;
                case "Name":        return Properties.Resources.DevName;
                case "Serial":      return Properties.Resources.Serial;
                case "Serial2":     return Properties.Resources.Serial2;
                case "Battery":     return Properties.Resources.Battery;
                case "Charging":    return Properties.Resources.Charging;
                case "Renew":       return Properties.Resources.Renew;
                case "Connection":  return Properties.Resources.Connection;
                case "Remarks":     return Properties.Resources.Remarks;
                case "Location":    return Properties.Resources.Location;
                case "Repairs":     return Properties.Resources.Repairs;
                default:            return columnName;
            }
        }

        private async void btnFetchDevices_Click(object sender, EventArgs e)
        {
            if (!IsApiKeySaved())
            {
                this.messageLabel.Text = Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData;
                this.messageLabel.Visible = true;
                txtApiKey.Focus();
                return;
            }
            // FIX: Reset counter when 60s have passed (was: counter never reset within a minute)
            TimeSpan timeSinceLastApiCall = DateTime.Now - lastApiCallTime;
            if (timeSinceLastApiCall.TotalSeconds >= 60)
                apiCallCount = 0;

            if (apiCallCount >= MaxApiCallsPerMinute)
            {
                int secondsLeft = (int)(60 - timeSinceLastApiCall.TotalSeconds);
                MessageBox.Show(
                    string.Format("{0} ({1}s)", Properties.Resources.YouHaveReachedTheMaximumAPICallsPerMinuteLimit, secondsLeft),
                    Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (apiCallCount >= MaxApiCallsPerDay)
            {
                MessageBox.Show(Properties.Resources.YouHaveReachedTheMaximumAPICallsPerDayLimit, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            apiCallCount++;
            lastApiCallTime = DateTime.Now;

            DeleteCachedJsonFile();
            await FetchAndDisplayDevicesAsync();
            InitializeGroupNameComboBox();
        }

        private List<DeviceInfo> ParseJson(string jsonData)
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();

            try
            {
                JObject json = JObject.Parse(jsonData);
                JArray deviceArray = (JArray)json["devices"];

                if (deviceArray == null)
                    return devices;

                foreach (var device in deviceArray)
                {
                    var deviceInfo = device["device"];
                    if (deviceInfo == null) continue;

                    string name    = (string)deviceInfo["name"];
                    string model   = (string)deviceInfo["model"];
                    string remarks = (string)deviceInfo["remarks"];
                    string repairs = (string)deviceInfo["repairs"];
                    string serial  = (string)deviceInfo["build_serial_no"];
                    string serial2 = null;

                    long? licenseExpiryTimestamp = (long?)deviceInfo["licence_expires_at"];
                    string formattedExpiryDate = licenseExpiryTimestamp.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(licenseExpiryTimestamp.Value).ToString("dd.MM.yyyy")
                        : "N/A";

                    string lastConnectedRaw = (string)deviceInfo["last_connected_at"];
                    string formattedLastConnected;
                    if (DateTime.TryParse(lastConnectedRaw, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTime lastConnectedDate))
                        formattedLastConnected = lastConnectedDate.ToString("dd.MM.yyyy HH:mm:ss");
                    else
                        formattedLastConnected = "N/A";

                    JArray customProperties = deviceInfo["custom_properties"] as JArray;
                    if (customProperties != null)
                    {
                        foreach (var prop in customProperties)
                        {
                            if (prop is JObject property)
                            {
                                string propName = (string)property["name"];
                                string value = property["value"]?.ToString();
                                if (value == null) continue;

                                switch (propName)
                                {
                                    case "Serial2":  serial2 = value; break;
                                    case "Remarks":  remarks = value; break;
                                    case "Repairs":  repairs = value; break;
                                }

                                if (serial2 != null && remarks != null && repairs != null)
                                    break;
                            }
                        }
                    }

                    if ((string.IsNullOrEmpty(serial) || serial.Equals("unknown")) && !string.IsNullOrEmpty(serial2))
                        serial = serial2;

                    string groupName = (string)deviceInfo["group"]?["name"] ?? "Unknown";

                    // FIX: Null-safe Replace — custom properties may not exist for every device
                    DeviceInfo info = new DeviceInfo
                    {
                        Name       = name,
                        Model      = model,
                        Serial     = serial,
                        Serial2    = serial2,
                        GroupName  = groupName,
                        Battery    = (string)deviceInfo["battery_status"],
                        Charging   = (string)deviceInfo["battery_charging"],
                        Connection = formattedLastConnected,
                        Repairs    = (repairs ?? "").Replace(",", Environment.NewLine),
                        Remarks    = (remarks ?? "").Replace(",", Environment.NewLine),
                        Location   = (string)deviceInfo["location"]?["address"],
                        Renew      = formattedExpiryDate,
                        Id         = (int)deviceInfo["id"]
                    };

                    devices.Add(info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.ErrorParsingJSON + ex.Message, Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return devices;
        }

        private void InitializeGroupNameComboBox()
        {
            // FIX: Unsubscribe first to prevent duplicate handlers accumulating on each fetch
            cmbGroupName.SelectedIndexChanged -= cmbGroupName_SelectedIndexChanged;

            cmbGroupName.Items.Clear();
            cmbGroupName.Items.Add(Properties.Resources.AllGroups);

            if (displayedDevices != null)
            {
                var groupCounts = displayedDevices
                    .GroupBy(device => device.GroupName)
                    .Select(group => new { GroupName = group.Key, DeviceCount = group.Count() })
                    .OrderBy(g => g.GroupName)
                    .ToList();

                foreach (var group in groupCounts)
                    cmbGroupName.Items.Add($"{group.GroupName} ({group.DeviceCount})");
            }

            cmbGroupName.SelectedIndex = 0;
            cmbGroupName.SelectedIndexChanged += cmbGroupName_SelectedIndexChanged;
        }

        public class DeviceInfo
        {
            public string GroupName  { get; set; }
            public string Model      { get; set; }
            public string Name       { get; set; }
            public string Serial     { get; set; }
            public string Serial2    { get; set; }
            public string Battery    { get; set; }
            public string Charging   { get; set; }
            public string Connection { get; set; }
            public string Renew      { get; set; }
            public string Repairs    { get; set; }
            public string Remarks    { get; set; }
            public string Location   { get; set; }
            public int    Id         { get; set; }
            // FIX: Removed JArray custom_properties — internal parse artefact not needed in the model
            //      (it was also leaking into DataGridView as an extra column)
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            // FIX: Suppress TextChanged event while clearing to prevent spurious filter call
            txtSearch.TextChanged -= txtSearch_TextChanged;
            txtSearch.Text = "";
            txtSearch.TextChanged += txtSearch_TextChanged;

            cmbGroupName.SelectedIndex = 0;
            dataGridView1.DataSource = displayedDevices;
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await SearchAndApplyFiltersAsync();
        }

        private async Task SearchAndApplyFiltersAsync()
        {
            string searchText = txtSearch.Text;
            List<DeviceInfo> filteredDevices = new List<DeviceInfo>();

            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            await Task.Run(() =>
            {
                filteredDevices = GetFilteredDevices(searchText, displayedDevices);
            });

            dataGridView1.Invoke((MethodInvoker)delegate
            {
                dataGridView1.DataSource = filteredDevices;
            });
        }

        private List<DeviceInfo> GetFilteredDevices(string searchText, List<DeviceInfo> devices)
        {
            if (devices == null) return new List<DeviceInfo>();

            if (string.IsNullOrWhiteSpace(searchText) || searchText.Equals(Properties.Resources.SearchText))
                return devices;

            string lower = searchText.ToLower();
            return devices.Where(device =>
                (device.Name?.ToLower().Contains(lower) == true) ||
                (device.Model?.ToLower().Contains(lower) == true) ||
                (device.Serial?.ToLower().Contains(lower) == true) ||
                (device.GroupName?.ToLower().Contains(lower) == true)
            ).ToList();
        }

        private void btnSaveApiKey1_Click(object sender, EventArgs e)
        {
            SaveApiKeyToRegistry(txtApiKey.Text);
        }

        private void btnSaveApiKey_Click(object sender, EventArgs e)
        {
            SaveApiKeyToRegistry(txtApiKey.Text);
            MessageBox.Show(Properties.Resources.APIKeySavedSuccessfully, Properties.Resources.APIKeyRequired, MessageBoxButtons.OK, MessageBoxIcon.Information);
            messageLabel.Visible = false;
        }

        private void txtApiKey_TextChanged(object sender, EventArgs e)
        {
            ApiKey = txtApiKey.Text;
        }

        private void DeleteCachedJsonFile()
        {
            string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cachedDevices.json");
            try
            {
                File.Delete(cacheFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.ErrorDeletingCachedJSONFile + $" {ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDevicesToCache(List<DeviceInfo> devices)
        {
            if (devices == null) return;
            string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cachedDevices.json");
            try
            {
                string json = JsonConvert.SerializeObject(devices);
                File.WriteAllText(cacheFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.ErrorSavingDevicesToCache + $"{ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task FetchAndDisplayDevicesAsync()
        {
            btnFetchDevices.Enabled = false;
            List<DeviceInfo> devices = new List<DeviceInfo>();

            if (!string.IsNullOrEmpty(ApiKey))
            {
                devices = await FetchDevicesAsync();

                foreach (var device in devices)
                {
                    if (string.IsNullOrEmpty(device.Serial))
                        device.Serial = "ID-" + device.Id.ToString();
                }

                if (devices.Count > 0)
                {
                    SortAndDisplayDevices(devices);
                    SaveDevicesToCache(devices);
                }
                else
                {
                    MessageBox.Show(Properties.Resources.NoDevicesFound, Properties.Resources.Information, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            InitializeGroupNameComboBox();
            messageLabel.Visible = false;
            btnFetchDevices.Enabled = true;
        }

        private void FormatColumns(DataGridView dataGridView)
        {
            dataGridView.SuspendLayout();

            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (column.Name.Equals("Repairs"))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    column.ReadOnly = false;
                }
                else
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    column.ReadOnly = true;
                }
            }

            // Measure once, then freeze widths so the grid never recalculates on scroll/refresh
            dataGridView.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (!column.Name.Equals("Repairs"))
                {
                    int w = column.Width;
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = w;
                }
            }

            dataGridView.ResumeLayout();
        }

        // FIX: Cache stores DeviceInfo list — deserialize directly, don't re-parse as API response
        private List<DeviceInfo> FetchDevicesFromCache()
        {
            string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cachedDevices.json");
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    string jsonData = File.ReadAllText(cacheFilePath);
                    return JsonConvert.DeserializeObject<List<DeviceInfo>>(jsonData) ?? new List<DeviceInfo>();
                }
                else
                {
                    // No cache yet on first run — silently ignored
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.ErrorFetchingDevicesFromCache + $"{ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return new List<DeviceInfo>();
        }

        // Send a broadcast message to one or more devices via the Scalefusion API.
        public async Task<bool> BroadcastMessage(string apiKey, IEnumerable<int> deviceIds, string senderName, string messageBody)
        {
            string url = "https://api.scalefusion.com/api/v1/devices/broadcast_message.json";

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            var fields = new List<KeyValuePair<string, string>>();
            foreach (int id in deviceIds)
                fields.Add(new KeyValuePair<string, string>("device_ids[]", id.ToString()));
            fields.Add(new KeyValuePair<string, string>("sender_name",    senderName));
            fields.Add(new KeyValuePair<string, string>("message_body",   messageBody));
            fields.Add(new KeyValuePair<string, string>("keep_ringing",   "false"));
            fields.Add(new KeyValuePair<string, string>("show_as_dialog", "false"));

            var content = new FormUrlEncodedContent(fields);

            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (HttpRequestException e)
            {
                MessageBox.Show(Properties.Resources.Error + ": " + e.Message,
                    Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Convenience overload for a single device
        public Task<bool> BroadcastMessage(string apiKey, int deviceId, string senderName, string messageBody)
            => BroadcastMessage(apiKey, new[] { deviceId }, senderName, messageBody);


        // Send a device action (buzz_device, screen_lock, reboot, ...) to one or more devices.
        public async Task<bool> DeviceAction(string apiKey, IEnumerable<int> deviceIds, string actionType)
        {
            // device_ids are passed as query-string: ?device_ids=1,2,3
            string ids = string.Join(",", deviceIds);
            string url = string.Format("https://api.scalefusion.com/api/v1/devices/actions.json?device_ids={0}", ids);

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action_type", actionType)
            });

            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(Properties.Resources.Error + ": " + ex.Message,
                    Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Convenience overload for a single device
        public Task<bool> DeviceAction(string apiKey, int deviceId, string actionType)
            => DeviceAction(apiKey, new[] { deviceId }, actionType);

        // FIX: Returns bool so callers can react to failure; no longer fire-and-forget
        public async Task<bool> UpdateScalefusion(string apiKey, string sendName, string sendValue, int idValue)
        {
            string url = $"https://api.scalefusion.com/api/v1/devices/{idValue}/custom_properties.json";
            string oneliner = sendValue.Replace("#", ",").Replace(Environment.NewLine, ",").Replace(",,", ",");

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("properties", $"[{{\"name\":\"{sendName}\",\"value\":\"{oneliner}\"}}]")
            });

            try
            {
                HttpResponseMessage response = await httpClient.PutAsync(url, content);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (HttpRequestException e)
            {
                MessageBox.Show(Properties.Resources.PrintError + e.Message);
                return false;
            }
        }


        // Shows a context menu with device actions and executes the chosen one.
        // targetLabel: displayed in confirmation dialogs (device name or group description)
        private void ShowActionMenu(Control parent, Point screenPt,
                                    IEnumerable<int> deviceIds, string targetLabel,
                                    bool isGroup)
        {
            string msgPrefix = isGroup ? Properties.Resources.AllDevices : Properties.Resources.Device;
            var ids = deviceIds.ToList();

            // Dispose any previous instance and create a fresh persistent menu
            // _actionMenu?.Dispose();  ← ENTFERNEN: Nicht jedesmal disposieren!
            _actionMenu = new ContextMenuStrip();

            var itemMsg    = new ToolStripMenuItem(Properties.Resources.SendMessage);
            var itemBuzz   = new ToolStripMenuItem(msgPrefix + " " + Properties.Resources.Ring);
            var itemLock   = new ToolStripMenuItem(msgPrefix + " " + Properties.Resources.Lock);
            var itemReboot = new ToolStripMenuItem(msgPrefix + " " + Properties.Resources.Reboot);

            _actionMenu.Items.AddRange(new ToolStripItem[]
                { itemMsg, new ToolStripSeparator(), itemBuzz, itemLock, itemReboot });

            // ── Nachricht ────────────────────────────────────────────────────
            itemMsg.Click += async (s, e) =>
            {
                using (var dlg = new SendMessage(targetLabel))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        bool ok = await BroadcastMessage(ApiKey, ids, dlg.SenderName, dlg.MessageBody);
                        if (ok)
                            MessageBox.Show(
                                string.Format(Properties.Resources.MessageSent, targetLabel),
                                Properties.Resources.Sent, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            // ── Klingeln ─────────────────────────────────────────────────────
            itemBuzz.Click += async (s, e) =>
            {
                bool ok = await DeviceAction(ApiKey, ids, "buzz_device");
                if (ok)
                    MessageBox.Show(
                        string.Format(Properties.Resources.BuzzCommandSent, targetLabel),
                        Properties.Resources.Done, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // ── Sperren ──────────────────────────────────────────────────────
            itemLock.Click += async (s, e) =>
            {
                bool ok = await DeviceAction(ApiKey, ids, "screen_lock");
                if (ok)
                    MessageBox.Show(
                        string.Format(Properties.Resources.LockCommandSent, targetLabel),
                        Properties.Resources.Done, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // ── Neustart (mit Sicherheitsabfrage) ────────────────────────────
            itemReboot.Click += async (s, e) =>
            {
                string question = string.Format(
                    Properties.Resources.ConfirmRestart, targetLabel);
                if (MessageBox.Show(question, Properties.Resources.ConfirmRestartTitle,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    bool ok = await DeviceAction(ApiKey, ids, "reboot");
                    if (ok)
                        MessageBox.Show(
                            string.Format(Properties.Resources.RestartCommandSent, targetLabel),
                            Properties.Resources.Done, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Point clientPt = parent.PointToClient(screenPt);
            _actionMenu.Show(parent, clientPt);
        }

        private async void DataGridView1_CellDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string columnName = dataGridView1.Columns[e.ColumnIndex].Name;

            // Doppelklick auf Gruppenname: Aktionsmenue fuer alle Geraete der Gruppe
            if (columnName == "GroupName")
            {
                string groupName = dataGridView1.Rows[e.RowIndex].Cells["GroupName"].Value?.ToString() ?? string.Empty;

                var groupDeviceIds = displayedDevices
                    .Where(d => d.GroupName == groupName)
                    .Select(d => d.Id)
                    .ToList();

                int count = groupDeviceIds.Count;
                string label = string.Format("Group \"{0}\" ({1} devices)", groupName, count);
                Point pt = dataGridView1.PointToScreen(dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false).Location);

                ShowActionMenu(dataGridView1, pt, groupDeviceIds, label, isGroup: true);
                return;
            }

            // Doppelklick auf Geraetename: Aktionsmenue fuer einzelnes Geraet
            if (columnName == "Name")
            {
                string deviceName = dataGridView1.Rows[e.RowIndex].Cells["Name"].Value?.ToString() ?? string.Empty;
                int    deviceId   = (int)dataGridView1.Rows[e.RowIndex].Cells["Id"].Value;
                Point pt = dataGridView1.PointToScreen(dataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false).Location);

                ShowActionMenu(dataGridView1, pt, new[] { deviceId }, deviceName, isGroup: false);
                return;
            }

            // Doppelklick auf Repairs: Reparaturfeld bearbeiten
            if (columnName != "Repairs") return;

            var cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
            string cellContent  = cell.Value?.ToString() ?? string.Empty;
            string columnHeader = dataGridView1.Columns[e.ColumnIndex].HeaderText;
            string devName      = dataGridView1.Rows[e.RowIndex].Cells["Name"].Value?.ToString() ?? string.Empty;

            using (var editForm = new CellEdit(cellContent, columnHeader, devName))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    var idValue   = (int)dataGridView1.Rows[e.RowIndex].Cells["Id"].Value;
                    var sendValue = editForm.EditedText.Replace(Environment.NewLine, ",");

                    bool success = await UpdateScalefusion(ApiKey, "Repairs", sendValue, idValue);
                    if (success)
                        cell.Value = editForm.EditedText.Replace(Environment.NewLine, ",");
                    else
                        cell.Value = cellContent;
                }
            }
        }

        private void btnSaveAsPdf_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    string currentDate = DateTime.Now.ToString("dd.MM.yyyy");
                    sfd.Filter = "PDF (*.pdf)|*.pdf";
                    sfd.FileName = String.Format("{0}-{1}.pdf", actualGroup.Replace(" ", ""), currentDate.Replace(".", ""));
                    bool fileError = false;

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (File.Exists(sfd.FileName))
                        {
                            try { File.Delete(sfd.FileName); }
                            catch (IOException) { fileError = true; }
                        }

                        if (!fileError)
                        {
                            try
                            {
                                iTextSharp.text.Font font = FontFactory.GetFont(FontFactory.HELVETICA, 12, iTextSharp.text.Font.NORMAL);
                                iTextSharp.text.Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, iTextSharp.text.Font.BOLD);
                                string headerText = String.Format(Properties.Resources.DeviceReportFor0From1, actualGroup, currentDate);

                                using (FileStream stream = new FileStream(sfd.FileName, FileMode.Create))
                                {
                                    Document pdfDoc = new Document(PageSize.A4.Rotate(), 10f, 10f, 20f, 10f);
                                    PdfWriter.GetInstance(pdfDoc, stream);
                                    pdfDoc.Open();

                                    Paragraph header = new Paragraph(headerText, headerFont)
                                    {
                                        Alignment = Element.ALIGN_CENTER,
                                        SpacingAfter = 20f
                                    };
                                    pdfDoc.Add(header);

                                    float[] columnWidths = CalculateColumnWidths(dataGridView1);

                                    int visibleColumns = dataGridView1.Columns
                                        .Cast<DataGridViewColumn>()
                                        .Count(c => c.Visible && c.Name != "GroupName" && c.Name != "Renew");

                                    PdfPTable pdfTable = new PdfPTable(visibleColumns);
                                    pdfTable.DefaultCell.Padding = 3;
                                    pdfTable.WidthPercentage = 100;
                                    pdfTable.HorizontalAlignment = Element.ALIGN_LEFT;
                                    pdfTable.SetWidths(columnWidths);

                                    foreach (DataGridViewColumn column in dataGridView1.Columns)
                                    {
                                        if (column.Visible && column.Name != "GroupName" && column.Name != "Renew")
                                        {
                                            PdfPCell headerCell = new PdfPCell(new Phrase(column.HeaderText, font))
                                            {
                                                BackgroundColor = new BaseColor(240, 240, 240),
                                                VerticalAlignment = Element.ALIGN_MIDDLE
                                            };
                                            pdfTable.AddCell(headerCell);
                                        }
                                    }

                                    foreach (DataGridViewRow row in dataGridView1.Rows)
                                    {
                                        if (!row.IsNewRow)
                                        {
                                            foreach (DataGridViewCell cell in row.Cells)
                                            {
                                                var column = dataGridView1.Columns[cell.ColumnIndex];
                                                if (column.Visible && column.Name != "GroupName" && column.Name != "Renew")
                                                {
                                                    PdfPCell dataCell = new PdfPCell(new Phrase(cell.Value?.ToString() ?? string.Empty, font))
                                                    {
                                                        VerticalAlignment = Element.ALIGN_MIDDLE
                                                    };
                                                    pdfTable.AddCell(dataCell);
                                                }
                                            }
                                        }
                                    }

                                    pdfDoc.Add(pdfTable);
                                    pdfDoc.Close();
                                    stream.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Properties.Resources.PrintError + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        // FIX: Single Graphics object for all measurements (was: new Graphics per cell = thousands of handles)
        private float[] CalculateColumnWidths(DataGridView gridView)
        {
            int visibleColumnCount = gridView.Columns
                .Cast<DataGridViewColumn>()
                .Count(c => c.Visible && c.Name != "GroupName" && c.Name != "Renew");

            float[] widths = new float[visibleColumnCount];
            int colIndex = 0;

            using (Graphics g = gridView.CreateGraphics())
            {
                foreach (DataGridViewColumn column in gridView.Columns)
                {
                    if (column.Visible && column.Name != "GroupName" && column.Name != "Renew")
                    {
                        widths[colIndex] = g.MeasureString(column.HeaderText, gridView.Font).Width + 10;

                        foreach (DataGridViewRow row in gridView.Rows)
                        {
                            if (!row.IsNewRow)
                            {
                                string cellValue = row.Cells[column.Index].Value?.ToString() ?? string.Empty;
                                float cellWidth = g.MeasureString(cellValue, gridView.Font).Width + 10;
                                if (cellWidth > widths[colIndex])
                                    widths[colIndex] = cellWidth;
                            }
                        }

                        colIndex++;
                    }
                }
            }

            return widths;
        }
    }
}
