using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
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
        private string cachedJsonData;
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
            // Store the API key in the Registry
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
            dataGridView1.CellDoubleClick += DataGridView1_CellDoubleClick;

            dailyResetTimer = new Timer(); // Initialize the daily reset timer
            dailyResetTimer.Interval = (int)TimeSpan.FromDays(1).TotalMilliseconds;
            dailyResetTimer.Tick += DailyResetTimer_Tick;
            dailyResetTimer.Start();
            // Remove minimize and maximize buttons
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Retrieve the API key from the Registry and set it
            string apiKey = GetApiKeyFromRegistry();
            if (!string.IsNullOrEmpty(apiKey))
            {
                ApiKey = apiKey;
            }
        }

        private void DailyResetTimer_Tick(object sender, EventArgs e)
        {
            // Reset the API call count at the beginning of each day
            apiCallCount = 0;
        }

        private void InitializeSearchBox()
        {
            txtSearch.ForeColor = System.Drawing.Color.Gray; // Set initial text color to gray
            txtSearch.Text = Properties.Resources.SearchText; // Set initial placeholder text
            txtSearch.GotFocus += txtSearch_GotFocus; // Attach event handler for focus
            txtSearch.LostFocus += txtSearch_LostFocus; // Attach event handler for lost focus
            txtSearch.TextChanged += txtSearch_TextChanged;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                ApplySearchFilter(txtSearch.Text, displayedDevices);
            }
        }

        private void txtSearch_GotFocus(object sender, EventArgs e)
        {
            if (!searchTextBoxHasFocus)
            {
                txtSearch.Text = string.Empty; // Clear the placeholder text
                txtSearch.ForeColor = System.Drawing.Color.Black; // Set text color to black when focused
                searchTextBoxHasFocus = true; // Update focus state
            }
        }

        private void txtSearch_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = Properties.Resources.SearchText; // Restore placeholder text if no input and lost focus
                txtSearch.ForeColor = System.Drawing.Color.Gray; // Set text color back to gray
                searchTextBoxHasFocus = false; // Update focus state
            }

            ApplySearchFilter(txtSearch.Text, displayedDevices);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            if (!IsApiKeySaved())
            {
                MessageBox.Show(Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData, Properties.Resources.APIKeyRequired, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.messageLabel.Text = Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData;
                txtApiKey.Focus();
                return;
            }
            else
            {
                this.messageLabel.Text = Properties.Resources.PleaseWaitWhileFetchingDeviceData;
            }

            // Set the API key if found in the Registry
            string savedApiKey = GetApiKeyFromRegistry();
            if (!string.IsNullOrEmpty(savedApiKey))
            {
                txtApiKey.Text = savedApiKey;
            }

            await FetchAndDisplayDevicesAsync();
            SortAndDisplayDevices(displayedDevices);
            SaveDevicesToCache(displayedDevices);
            InitializeGroupNameComboBox();
            new ToolTip().SetToolTip(btnFetchDevices, W3Devices.Properties.Resources.FetchDevicesFromScalefusionCloud);
            new ToolTip().SetToolTip(cmbGroupName, W3Devices.Properties.Resources.ShowDevicesByGroup);
            new ToolTip().SetToolTip(btnPrint, W3Devices.Properties.Resources.SaveDeviceReportToPdf);
            new ToolTip().SetToolTip(btnReload, W3Devices.Properties.Resources.ClearSearchTextAndResetDevices);
            new ToolTip().SetToolTip(btnSaveApiKey, W3Devices.Properties.Resources.SaveAPIKeyToRegistry);
        }

        private async Task<List<DeviceInfo>> FetchDevicesAsync()
        {
            if (!string.IsNullOrEmpty(cachedJsonData))
            {
                return ParseJson(cachedJsonData);
            }

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", ApiKey);
                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                };
                try
                {
                    HttpResponseMessage response = await client.GetAsync(ApiUrl);
                    switch (response.StatusCode)
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
                            response.EnsureSuccessStatusCode();
                            break;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    cachedJsonData = responseBody; // Cache the fetched JSON data
                    return ParseJson(responseBody);
                }
                catch (HttpRequestException ex)
                {
                    MessageBox.Show(Properties.Resources.ErrorFetchingData + $"{ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return new List<DeviceInfo>(); // Return an empty list in case of error
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
            if (searchText.Equals(Properties.Resources.SearchText))
            {
                dataGridView1.DataSource = devices;
            }
            else
            {
                var filteredDevices = devices.Where(device =>
                    device.Name.ToLower().Contains(searchText.ToLower()) ||
                    device.Model.ToLower().Contains(searchText.ToLower()) ||
                    device.Serial.ToLower().Contains(searchText.ToLower()) ||
                    device.GroupName.ToLower().Contains(searchText.ToLower())
                ).ToList();

                dataGridView1.DataSource = filteredDevices;
            }
        }

        private void SortAndDisplayDevices(List<DeviceInfo> devices)
        {
            displayedDevices = devices.AsParallel()
                .OrderBy(d => d.GroupName)
                .ThenBy(d => d.Name)
                .ToList();
            BindDeviceGrid(displayedDevices);
        }

        private void BindDeviceGrid(List<DeviceInfo> devices)
        {
            dataGridView1.DataSource = devices;
            dataGridView1.Columns["Id"].Visible = false; // Hide the Id column
            dataGridView1.Columns["Battery"].Visible = false; // Hide the Battery column
            dataGridView1.Columns["Serial2"].Visible = false; // Hide the Serial2 column
            dataGridView1.Columns["Charging"].Visible = false; // Hide the Charging column
            dataGridView1.Columns["Location"].Visible = false; // Hide the Location column
            dataGridView1.Columns["Remarks"].Visible = false; // Hide the Remarks column
            dataGridView1.Columns["Repairs"].HeaderCell.ToolTipText = String.Format(W3Devices.Properties.Resources.ADoubleClickOntoTheCellOpensAnEditorNwindowForTheDeviceSRepairsField, Environment.NewLine);
            FormatColumns(dataGridView1); // Set column widths

            // Set column headers and disable line wrapping
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.HeaderText = GetLocalizedHeaderText(column.Name);
                column.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
            }
        }

        private string GetLocalizedHeaderText(string columnName)
        {
            switch (columnName)
            {
                case "GroupName":
                    return Properties.Resources.GroupName;
                case "Model":
                    return Properties.Resources.Model;
                case "Name":
                    return Properties.Resources.DevName;
                case "Serial":
                    return Properties.Resources.Serial;
                case "Serial2":
                    return Properties.Resources.Serial2;
                case "Battery":
                    return Properties.Resources.Battery;
                case "Charging":
                    return Properties.Resources.Charging;
                case "Renew":
                    return Properties.Resources.Renew;
                case "Connection":
                    return Properties.Resources.Connection;
                case "Remarks":
                    return Properties.Resources.Remarks;
                case "Location":
                    return Properties.Resources.Location;
                case "Repairs":
                    return Properties.Resources.Repairs;
                default:
                    return columnName; // Default to the column name if not found
            }
        }

        private async void btnFetchDevices_Click(object sender, EventArgs e)
        {
            if (!IsApiKeySaved())
            {
                MessageBox.Show(Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData, Properties.Resources.APIKeyRequired, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.messageLabel.Text = Properties.Resources.PleaseEnterYourAPIKeyAndSaveItBeforeFetchingData;
                txtApiKey.Focus();
                return;
            }
            else
            {
                this.messageLabel.Text = Properties.Resources.PleaseWaitWhileFetchingDeviceData;
            }

            // Check if the maximum number of API calls per minute has been reached
            TimeSpan timeSinceLastApiCall = DateTime.Now - lastApiCallTime;
            if (timeSinceLastApiCall.TotalSeconds < 60 && apiCallCount >= MaxApiCallsPerMinute)
            {
                MessageBox.Show(Properties.Resources.YouHaveReachedTheMaximumAPICallsPerMinuteLimit, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if the maximum number of API calls per day has been reached
            if (apiCallCount >= MaxApiCallsPerDay)
            {
                MessageBox.Show(Properties.Resources.YouHaveReachedTheMaximumAPICallsPerDayLimit, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Update the API call count and time
            apiCallCount++;
            lastApiCallTime = DateTime.Now;

            cachedJsonData = "";
            DeleteCachedJsonFile();
            await FetchAndDisplayDevicesAsync();
            InitializeGroupNameComboBox();
        }

        private List<DeviceInfo> ParseJson(string jsonData)
        {
            List<DeviceInfo> devices = new List<DeviceInfo>();
            Dictionary<string, int> groupCounts = new Dictionary<string, int>();

            try
            {
                // Parse JSON and populate devices list
                JObject json = JObject.Parse(jsonData);
                JArray deviceArray = (JArray)json["devices"];

                foreach (var device in deviceArray)
                {
                    var deviceInfo = device["device"];
                    string name = (string)deviceInfo["name"];
                    string model = (string)deviceInfo["model"];
                    string remarks = (string)deviceInfo["remarks"];
                    string repairs = (string)deviceInfo["repairs"];
                    string serial = (string)deviceInfo["build_serial_no"];
                    string serial2 = null;

                    // Format the license expiration date
                    long? licenseExpiryTimestamp = (long?)deviceInfo["licence_expires_at"];
                    string formattedExpiryDate = licenseExpiryTimestamp.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(licenseExpiryTimestamp.Value).ToString("dd.MM.yyyy")
                        : "N/A";

                    // Parse and format the last connected date
                    string lastConnectedRaw = (string)deviceInfo["last_connected_at"];
                    string formattedLastConnected;
                    if (DateTime.TryParse(lastConnectedRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime lastConnectedDate))
                    {
                        formattedLastConnected = lastConnectedDate.ToString("dd.MM.yyyy HH:mm:ss");
                    }
                    else
                    {
                        formattedLastConnected = "N/A"; // Set to "N/A" if parsing fails
                    }

                    // Directly access custom_properties with null-check
                    JArray customProperties = deviceInfo["custom_properties"] as JArray;
                    if (customProperties != null)
                    {
                        foreach (var prop in customProperties)
                        {
                            if (prop is JObject property && (string)property["name"] == "Serial2" && property["value"] != null)
                            {
                                serial2 = property["value"].ToString();
                                break;
                            }
                        }
                        foreach (var prop in customProperties)
                        {
                            if (prop is JObject property && (string)property["name"] == "Remarks" && property["value"] != null)
                            {
                                remarks = property["value"].ToString();
                                break;
                            }
                        }
                        foreach (var prop in customProperties)
                        {
                            if (prop is JObject property && (string)property["name"] == "Repairs" && property["value"] != null)
                            {
                                repairs = property["value"].ToString();
                                break;
                            }
                        }
                    }

                    // Use Serial2 as the main serial if Serial is empty or "unknown"
                    if ((string.IsNullOrEmpty(serial) || serial.Equals("unknown")) && !string.IsNullOrEmpty(serial2))
                    {
                        serial = serial2;
                    }

                    // Get group name and update group count
                    string groupName = (string)deviceInfo["group"]?["name"] ?? "Unknown";
                    if (groupCounts.ContainsKey(groupName))
                    {
                        groupCounts[groupName]++;
                    }
                    else
                    {
                        groupCounts[groupName] = 1;
                    }

                    // Populate DeviceInfo object
                    DeviceInfo info = new DeviceInfo
                    {
                        Name = name,
                        Model = model,
                        Serial = serial,
                        Serial2 = serial2,
                        GroupName = groupName,
                        Battery = (string)deviceInfo["battery_status"],
                        Charging = (string)deviceInfo["battery_charging"],
                        Connection = formattedLastConnected,
                        Repairs = repairs.Replace(",", Environment.NewLine),
                        Remarks = remarks.Replace(",", Environment.NewLine),
                        Location = (string)deviceInfo["location"]?["address"],
                        Renew = formattedExpiryDate,
                        Id = (int)deviceInfo["id"]
                    };

                    devices.Add(info);
                }

                // Prepare the message for displaying group counts, sorted alphabetically by group name
                var sortedGroupCounts = groupCounts.OrderBy(kvp => kvp.Key);
                string message = string.Join(Environment.NewLine, sortedGroupCounts.Select(kvp => $"{kvp.Value.ToString("D2")} - {kvp.Key}"));

                // Add the total count of devices to the message
                int totalDevices = devices.Count;
                message += $"\n\nTotal Devices: {totalDevices}";

                // Show the message box asynchronously
//                Task.Run(() => MessageBox.Show(message, "Device Count by Group", MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
            catch (Exception ex)
            {
                MessageBox.Show(W3Devices.Properties.Resources.ErrorParsingJSON + ex.Message, W3Devices.Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return devices;
        }

        private void InitializeGroupNameComboBox()
        {
            // Clear existing items in the combobox
            cmbGroupName.Items.Clear();
            cmbGroupName.Items.Add(Properties.Resources.AllGroups); // Add the default option

            // Fetch devices and group them by GroupName with device counts
            var groupCounts = displayedDevices
                .GroupBy(device => device.GroupName)
                .Select(group => new
                {
                    GroupName = group.Key,
                    DeviceCount = group.Count()
                })
                .OrderBy(g => g.GroupName) // Sort alphabetically by group name
                .ToList();

            // Add group names with device counts to the combobox
            foreach (var group in groupCounts)
            {
                cmbGroupName.Items.Add($"{group.GroupName} ({group.DeviceCount})");
            }

            cmbGroupName.SelectedIndex = 0; // Set default selection to "Show All"
            cmbGroupName.SelectedIndexChanged += cmbGroupName_SelectedIndexChanged;
        }

        private List<string> GetUniqueGroupNames()
        {
            // Fetch devices from the API or cached data
            List<DeviceInfo> devices = displayedDevices ?? FetchDevicesFromCache();

            // Get unique GroupNames from the devices
            return devices
                .Select(device => device.GroupName)
                .Where(groupName => !string.IsNullOrEmpty(groupName))
                .Distinct()
                .ToList();
        }
        public class DeviceInfo
        {
            public string GroupName { get; set; }
            public string Model { get; set; }
            public string Name { get; set; }
            public string Serial { get; set; }
            public JArray custom_properties { get; set; }
            public string Serial2 { get; set; }
            public string Battery { get; set; }
            public string Charging { get; set; }
            public string Connection { get; set; }
            public string Renew { get; set; }
            public string Repairs { get; set; }
            public string Remarks { get; set; }
            public string Location { get; set; }
            public int Id { get; set; }
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            cmbGroupName.SelectedIndex = 0;
            ApplySearchFilter(txtSearch.Text, displayedDevices);
        }

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await SearchAndApplyFiltersAsync();
        }

        private async Task SearchAndApplyFiltersAsync()
        {
            string searchText = txtSearch.Text;
            List<DeviceInfo> filteredDevices = new List<DeviceInfo>();

            // Disable auto column width adjustment
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            await Task.Run(() =>
            {
                filteredDevices = GetFilteredDevices(searchText, displayedDevices);
            });

            // Update UI on the UI thread
            dataGridView1.Invoke((MethodInvoker)delegate
            {
                dataGridView1.DataSource = filteredDevices;
            });

            // Re-enable auto column width adjustment
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }

        private List<DeviceInfo> GetFilteredDevices(string searchText, List<DeviceInfo> devices)
        {
            if (searchText.Equals(Properties.Resources.SearchText))
            {
                return devices;
            }
            else
            {
                return devices.Where(device =>
                    device.Name.ToLower().Contains(searchText.ToLower()) ||
                    device.Model.ToLower().Contains(searchText.ToLower()) ||
                    device.Serial.ToLower().Contains(searchText.ToLower()) ||
                    device.GroupName.ToLower().Contains(searchText.ToLower())
                ).ToList();
            }
        }

        private void btnSaveApiKey_Click(object sender, EventArgs e)
        {
            // Save the API key to the Registry when the Save button is clicked
            SaveApiKeyToRegistry(txtApiKey.Text);
        }

        private void txtApiKey_TextChanged(object sender, EventArgs e)
        {
            // Update the API key when the text in the TextBox changes
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

                // Add the code to handle "ID-" prefix for devices with empty Serial
                foreach (var device in devices)
                {
                    if (string.IsNullOrEmpty(device.Serial))
                    {
                        device.Serial = "ID-" + device.Id.ToString();
                    }
                }

                if (devices.Count > 0)
                {
                    SortAndDisplayDevices(devices);
                    SaveDevicesToCache(devices); // Save fetched data to cache
                }
                else
                {
                    MessageBox.Show(Properties.Resources.NoDevicesFound, Properties.Resources.Information, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            messageLabel.Visible = false;
            btnFetchDevices.Enabled = true;
        }

        private void FormatColumns(DataGridView dataGridView)
        {
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (column.Name.Equals("Location"))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
                else if (column.Name.Equals("Remarks") || column.Name.Equals("Repairs"))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    column.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    column.ReadOnly = false; // Make "Repairs" and "Remarks" columns editable
                }
                else
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    column.ReadOnly = true; // Make other columns read-only for safety
                }
            }
        }

        private List<DeviceInfo> FetchDevicesFromCache()
        {
            string cacheFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cachedDevices.json");

            try
            {
                if (File.Exists(cacheFilePath))
                {
                    string jsonData = File.ReadAllText(cacheFilePath);
                    return ParseJson(jsonData);
                }
                else
                {
                    MessageBox.Show(Properties.Resources.NoCachedDataFound, Properties.Resources.Information, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.ErrorFetchingDevicesFromCache + $"{ex.Message}", Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return new List<DeviceInfo>(); // Return an empty list in case of an error or no cached data
        }

        public async Task UpdateScalefusion(string apiKey, string sendName, string sendValue, int idValue)
        {
            // API endpoint with the device ID
            string url = $"https://api.scalefusion.com/api/v1/devices/{idValue}/custom_properties.json";
            string oneliner = sendValue.Replace("#", ",").Replace(Environment.NewLine, ",").Replace(",,", ",");

            using (HttpClient client = new HttpClient())
            {
                // Set the required headers
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

                // Prepare the content in form-urlencoded format
                var content = new FormUrlEncodedContent(new[]
                {
            new KeyValuePair<string, string>("properties", $"[{{\"name\":\"{sendName}\",\"value\":\"{oneliner}\"}}]")
        });

                try
                {
                    // Send the PUT request
                    HttpResponseMessage response = await client.PutAsync(url, content);
                    response.EnsureSuccessStatusCode(); // Throws an exception if the response status code is not successful

                    // Process the response if needed
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Update successful: " + responseBody);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("Request error: " + e.Message);
                }
            }
        }
        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ensure the double-clicked cell is valid
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                string cellContent = cell.Value?.ToString() ?? string.Empty;
                string columnHeader = dataGridView1.Columns[e.ColumnIndex].HeaderText;
                string deviceName = (string)dataGridView1.Rows[e.RowIndex].Cells["Name"].Value;

                // Open the edit form with the cell content and column header as the title
                using (var editForm = new CellEdit(cellContent, columnHeader, deviceName))
                {
                    if (editForm.ShowDialog() == DialogResult.OK)
                    {
                        // Check if the edited cell is in the "Repairs" or "Remarks" column
                        if (columnHeader == Properties.Resources.Repairs)
                        {
                            // Get the "Id" cell value from the same row
                            var idValue = (int)dataGridView1.Rows[e.RowIndex].Cells["Id"].Value;
                            var sendValue = editForm.EditedText.Replace(Environment.NewLine, ",");

                            // Start the async update without awaiting
                            _ = UpdateScalefusion(ApiKey, "Repairs", sendValue, idValue);
                        }
                        else if (columnHeader == Properties.Resources.Remarks)
                        {
                            // Get the "Id" cell value from the same row
                            var idValue = (int)dataGridView1.Rows[e.RowIndex].Cells["Id"].Value;
                            var sendValue = editForm.EditedText.Replace(Environment.NewLine, ",");

                            // Start the async update without awaiting
                            _ = UpdateScalefusion(ApiKey, "Remarks", sendValue, idValue);
                        }
                        // Update the cell content with the edited text
                        cell.Value = editForm.EditedText.Replace(Environment.NewLine, ",");
                    }
                }
            }
        }
        private void btnSaveAsPdf_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count > 0)
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    string currentDate = DateTime.Now.ToString("mm.dd.yyyy"); // Use locale-specific date format
                    sfd.Filter = "PDF (*.pdf)|*.pdf";
                    sfd.FileName = String.Format("{0}-{1}.pdf", actualGroup.Replace(" ", ""), currentDate.Replace(".", ""));
                    bool fileError = false;

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        if (File.Exists(sfd.FileName))
                        {
                            try
                            {
                                File.Delete(sfd.FileName);
                            }
                            catch (IOException)
                            {
                                fileError = true;
                            }
                        }

                        if (!fileError)
                        {
                            try
                            {
                                // Set the font size to 12
                                iTextSharp.text.Font font = FontFactory.GetFont(FontFactory.HELVETICA, 12, iTextSharp.text.Font.NORMAL);
                                iTextSharp.text.Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, iTextSharp.text.Font.BOLD);

                                // Create the header text
                                string headerText = String.Format(W3Devices.Properties.Resources.DeviceReportFor0From1, actualGroup, currentDate);

                                // Initialize the document and writer
                                using (FileStream stream = new FileStream(sfd.FileName, FileMode.Create))
                                {
                                    Document pdfDoc = new Document(PageSize.A4.Rotate(), 10f, 10f, 20f, 10f); // Landscape
                                    PdfWriter.GetInstance(pdfDoc, stream);
                                    pdfDoc.Open();

                                    // Add the header to the document
                                    Paragraph header = new Paragraph(headerText, headerFont)
                                    {
                                        Alignment = Element.ALIGN_CENTER,
                                        SpacingAfter = 20f
                                    };
                                    pdfDoc.Add(header);

                                    // Calculate optimal widths for each column based on DataGridView content and headers
                                    float[] columnWidths = CalculateColumnWidths(dataGridView1);

                                    // Count visible columns excluding "GroupName"
                                    int visibleColumns = dataGridView1.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible && c.Name != "GroupName");

                                    // Create the PDF table with the number of visible columns (excluding "GroupName")
                                    PdfPTable pdfTable = new PdfPTable(visibleColumns);
                                    pdfTable.DefaultCell.Padding = 3;
                                    pdfTable.WidthPercentage = 100;
                                    pdfTable.HorizontalAlignment = Element.ALIGN_LEFT;
                                    pdfTable.SetWidths(columnWidths); // Set calculated column widths

                                    // Add header cells to the PDF table, excluding "GroupName"
                                    foreach (DataGridViewColumn column in dataGridView1.Columns)
                                    {
                                        if (column.Visible && column.Name != "GroupName")
                                        {
                                            PdfPCell headerCell = new PdfPCell(new Phrase(column.HeaderText, font))
                                            {
                                                BackgroundColor = new BaseColor(240, 240, 240),
                                                VerticalAlignment = Element.ALIGN_MIDDLE
                                            };
                                            pdfTable.AddCell(headerCell);
                                        }
                                    }

                                    // Add rows to the PDF table
                                    foreach (DataGridViewRow row in dataGridView1.Rows)
                                    {
                                        if (!row.IsNewRow)
                                        {
                                            foreach (DataGridViewCell cell in row.Cells)
                                            {
                                                var column = dataGridView1.Columns[cell.ColumnIndex];
                                                if (column.Visible && column.Name != "GroupName") // Exclude "GroupName"
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

                                    // Add the table to the document
                                    pdfDoc.Add(pdfTable);
                                    pdfDoc.Close();
                                    stream.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(W3Devices.Properties.Resources.PrintError + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        // Adjusted method to ignore "GroupName" column when calculating column widths
        private float[] CalculateColumnWidths(DataGridView gridView)
        {
            int visibleColumnCount = gridView.Columns.Cast<DataGridViewColumn>().Count(c => c.Visible && c.Name != "GroupName");
            float[] widths = new float[visibleColumnCount];
            int colIndex = 0;

            // Initialize widths array to 0 for all visible columns except "GroupName"
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = 0;
            }

            // Calculate the maximum width for each visible column based on both headers and cell content in each row
            foreach (DataGridViewColumn column in gridView.Columns)
            {
                if (column.Visible && column.Name != "GroupName")
                {
                    // Measure header text width
                    using (Graphics g = gridView.CreateGraphics())
                    {
                        float headerWidth = g.MeasureString(column.HeaderText, gridView.Font).Width + 10; // Add padding
                        widths[colIndex] = headerWidth;
                    }

                    // Measure each cell in the column to find the widest cell content
                    foreach (DataGridViewRow row in gridView.Rows)
                    {
                        if (!row.IsNewRow)
                        {
                            var cellValue = row.Cells[column.Index].Value?.ToString() ?? string.Empty;

                            using (Graphics g = gridView.CreateGraphics())
                            {
                                float cellWidth = g.MeasureString(cellValue, gridView.Font).Width + 10; // Add padding
                                if (cellWidth > widths[colIndex])
                                {
                                    widths[colIndex] = cellWidth; // Use the widest cell content
                                }
                            }
                        }
                    }
                    colIndex++;
                }
            }

            return widths;
        }
    }
}
