using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Aquarius.Helpers;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Humanizer;
using log4net;
using SamplesTripScheduler.PrivateApis;
using ServiceStack;
using ServiceStack.Text;

namespace SamplesTripScheduler
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MainForm()
        {
            InitializeComponent();

            // ReSharper disable once VirtualMemberCallInConstructor
            Text = ExeHelper.ExeNameAndVersion;

            LoadConfig();

            apiTokenLinkLabel.Enabled = false;
            disconnectButton.Enabled = false;
            loadButton.Enabled = false;
            scheduleButton.Enabled = false;
            tripListBox.Enabled = false;
        }

        private void Info(string message)
        {
            Log.Info(message);
            WriteLine($"INFO: {message}");
        }

        private void Warn(string message)
        {
            Log.Warn(message);
            WriteLine($"WARN: {message}");
        }

        private void Error(Exception exception)
        {
            Log.Error(exception);
            WriteLine($"ERROR: {exception.Message}");
        }

        private void WriteLine(string message)
        {
            var text = outputTextBox.Text;

            if (!string.IsNullOrEmpty(text))
                text += "\r\n";

            text += message;

            outputTextBox.Text = text;
            KeepOutputVisible();
        }

        private void KeepOutputVisible()
        {
            outputTextBox.SelectionStart = outputTextBox.TextLength;
            outputTextBox.SelectionLength = 0;
            outputTextBox.ScrollToCaret();
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            outputTextBox.Text = string.Empty;
            KeepOutputVisible();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        private void LoadConfig()
        {
            var jsonPath = GetConfigPath();

            var config = File.Exists(jsonPath)
                ? File.ReadAllText(jsonPath).FromJson<Config>()
                : new Config();

            serverTextBox.Text = config.Server ?? string.Empty;
            apiTokenTextBox.Text = config.ApiToken ?? string.Empty;

            OnServerConfigChanged();
        }

        private string GetConfigPath()
        {
            return Path.Combine(ExeHelper.ExeDirectory, $"{nameof(Config)}.json");
        }

        private void SaveConfig()
        {
            var jsonText = new Config
                {
                    Server = serverTextBox.Text.Trim(),
                    ApiToken = apiTokenTextBox.Text.Trim()
                }
                .ToJson()
                .IndentJson();

            File.WriteAllText(GetConfigPath(), jsonText);
        }

        private ISamplesClient _client;

        private void connectButton_Click(object sender, EventArgs e)
        {
            using (new CursorWait())
            {
                Connect();
            }
        }

        private void Connect()
        {
            try
            {
                _client = SamplesClient.CreateConnectedClient(
                    serverTextBox.Text.Trim(),
                    apiTokenTextBox.Text.Trim());

                GetConnectionInfo();

                SaveConfig();

                ClearTripList();

                connectButton.Enabled = false;
                disconnectButton.Enabled = true;
                loadButton.Enabled = true;
                serverTextBox.Enabled = false;
                apiTokenTextBox.Enabled = false;
                apiTokenLinkLabel.Enabled = false;
            }
            catch (Exception exception)
            {
                Disconnect();
                Error(exception);
            }
        }

        private User User { get; set; }

        private void GetConnectionInfo()
        {
            User = _client.Get(new GetUserTokens()).User;

            var serverName = GetServerName();

            if (_client.Client is SdkServiceClient client)
            {
                var serverUri = new Uri(client.BaseUri);

                serverName = $"{serverUri.Scheme}://{serverUri.Host}";
            }

            Info($"{Text} connected to {serverName} (v{_client.ServerVersion}) as {User.UserProfile.FullName}");

            // ReSharper disable once LocalizableElement
            connectionLabel.Text = $"Connected to {serverName} (v{_client.ServerVersion}) as {User.UserProfile.FullName}";
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            using (new CursorWait())
            {
                Disconnect();
            }
        }

        private string GetServerName()
        {
            return serverTextBox.Text.Trim();
        }

        private void Disconnect()
        {
            if (_client != null)
            {
                Info($"Disconnected from AQS {_client.ServerVersion} on {GetServerName()}");

                _client.Dispose();
                _client = null;
            }

            ClearTripList();

            connectionLabel.Text = string.Empty;
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
            serverTextBox.Enabled = true;
            apiTokenTextBox.Enabled = true;
        }

        private void ClearTripList()
        {
            tripListBox.DataSource = null;
            tripListBox.Enabled = false;
            tripListBox.SelectedIndex = -1;

            loadButton.Enabled = false;
            scheduleButton.Enabled = false;
        }

        private void SetTripList(Dictionary<FieldTrip, string> items)
        {
            tripListBox.DataSource = new BindingSource(items, null);
            tripListBox.DisplayMember = "Value";
            tripListBox.ValueMember = "Key";
            tripListBox.Enabled = true;
        }

        private void LoadTripsWithPlannedVisits()
        {
            var allTrips = _client.Get(new GetFieldTrips()).DomainObjects;

            var plannedVisits = _client.Get(new GetFieldVisits
            {
                PlanningStatuses = new List<string> {$"{PlanningStatusType.PLANNED}"}
            }).DomainObjects;

            var plannedTripIds = plannedVisits
                .Where(v => v.FieldTrip != null)
                .Select(v => v.FieldTrip)
                .Select(t => t.Id)
                .ToList();

            var tripsWithPlannedVisits = allTrips
                .Where(t => plannedTripIds.Contains(t.Id))
                .ToList();

            var tripItems = tripsWithPlannedVisits.ToDictionary(t => t, FormatTripListItem);

            if (tripItems.Any())
            {
                SetTripList(tripItems);

                tripListBox.SelectedIndex = 0;
            }
            else
            {
                ClearTripList();
            }

            Info($"{"trip".ToQuantity(tripsWithPlannedVisits.Count)} with planned visits.");
        }

        private string FormatTripListItem(FieldTrip trip)
        {
            var plannedVisits = trip
                .FieldVisits
                .Where(v => v.PlanningStatus == PlanningStatusType.PLANNED && (v.PlannedActivities?.Any() ?? false))
                .ToList();

            return $"{trip.CustomId} @ {trip.StartTime} with {"visit".ToQuantity(plannedVisits.Count)}: {string.Join(", ", plannedVisits.Select(v => $"{v.SamplingLocation.CustomId} with {"activity".ToQuantity(v.PlannedActivities.Count)}"))}";
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            using (new CursorWait())
            {
                LoadPlannedTrips();
            }
        }

        private void LoadPlannedTrips()
        {
            LoadTripsWithPlannedVisits();
        }

        private void scheduleButton_Click(object sender, EventArgs e)
        {
            using (new CursorWait())
            {
                ScheduleSelectedTrips();
                LoadTripsWithPlannedVisits();
            }
        }

        private void tripListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var index = tripListBox.SelectedIndex;

            scheduleButton.Enabled = index >= 0;
        }

        private void ScheduleSelectedTrips()
        {
            if (tripListBox.SelectedIndex < 0 || tripListBox.Items.Count < 0)
                return;

            var visitsToSchedule = tripListBox
                .SelectedItems
                .Cast<KeyValuePair<FieldTrip, string>>()
                .Select(kvp => kvp.Key)
                .SelectMany(t => t.FieldVisits.Where(v => v.PlanningStatus == PlanningStatusType.PLANNED))
                .ToList();

            Warn($"Scheduling {visitsToSchedule.Count} visits.");

            foreach (var visit in visitsToSchedule)
            {
                ScheduleVisit(visit);
            }
        }

        private void ScheduleVisit(FieldVisit visit)
        {
            // Mark the visit as in progress
            var updatedVisit = _client.Put(new PutFieldVisit
            {
                Id = visit.Id,
                PlanningStatus = PlanningStatusType.IN_PROGRESS
            });

            var message = $"Trip '{visit.FieldTrip.CustomId}' on {visit.StartTime} @ '{visit.SamplingLocation.CustomId}': ";

            foreach (var plannedActivity in visit.PlannedActivities)
            {
                var activityTemplate = plannedActivity.ActivityTemplate;

                message += $" '{activityTemplate.CustomId}' with {"specimen".ToQuantity(activityTemplate.SpecimenTemplates.Count)} {string.Join(", ", activityTemplate.SpecimenTemplates.Select(s => s.CustomId))}";

                var activity = _client.Post(new PostSpecimensFromPlannedActivity
                {
                    VisitId = visit.Id,
                    Id = plannedActivity.Id,
                    ActivityTemplate = new ActivityTemplate
                    {
                        Id = activityTemplate.Id,
                        Medium = activityTemplate.Medium,
                        SpecimenTemplates = null,
                        Type = activityTemplate.Type
                    },
                    ActivityType = plannedActivity.ActivityType
                });
            }

            Info(message);
        }

        private void OnServerConfigChanged()
        {
            if (!string.IsNullOrWhiteSpace(apiTokenTextBox.Text))
            {
                apiTokenLinkLabel.Enabled = false;
                return;
            }

            apiTokenLinkLabel.Enabled = TryParseServerUri(serverTextBox.Text, out _);
        }

        private void apiTokenLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (TryParseServerUri(serverTextBox.Text, out var uri))
            {
                System.Diagnostics.Process.Start($"{uri}/v1/authentication/google?type=application");
            }
        }

        private bool TryParseServerUri(string text, out Uri uri)
        {
            uri = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                uri = UriHelper.ResolveUri(text.Trim(), "/api", Uri.UriSchemeHttps);

                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        private void serverTextBox_TextChanged(object sender, EventArgs e)
        {
            OnServerConfigChanged();
        }

        private void apiTokenTextBox_TextChanged(object sender, EventArgs e)
        {
            OnServerConfigChanged();
        }
    }
}
