using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Aquarius.Helpers;
using Aquarius.Samples.Client;
using Aquarius.Samples.Client.ServiceModel;
using Humanizer;
using log4net;
using NodaTime;
using SamplesPlannedSpecimenInstantiator;
using SamplesPlannedSpecimenInstantiator.PrivateApis;
using ServiceStack;
using ServiceStack.Text;
using Config = SamplesPlannedSpecimenInstantiator.Config;

namespace SamplesTripScheduler
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MainForm()
        {
            InitializeComponent();

            // ReSharper disable once VirtualMemberCallInConstructor
            Text = $"Planned Specimens Instantiator v{ExeHelper.ExeVersion}";

            LoadConfig();

            apiTokenLinkLabel.Enabled = false;
            disconnectButton.Enabled = false;
            loadButton.Enabled = false;
            scheduleButton.Enabled = false;
            tripDataGridView.Enabled = false;
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
            tripDataGridView.DataSource = null;
            tripDataGridView.Enabled = false;

            loadButton.Enabled = false;
            scheduleButton.Enabled = false;
        }

        private void LoadTripsWithPlannedVisits()
        {
            var plannedVisits = _client.LazyGet<FieldVisitSimple, HackedGetFieldVisits, SearchResultFieldVisitSimple>(
                    new HackedGetFieldVisits
                    {
                        PlanningStatuses = new List<string> {$"{PlanningStatusType.PLANNED}"},
                    }).DomainObjects
                .ToList();

            var plannedTripVisits = plannedVisits
                .Where(v => v.StartTime.HasValue && v.FieldTrip != null)
                .ToList();

            var plannedTripIds = new HashSet<string>(plannedTripVisits
                .Select(v => v.FieldTrip.Id)
                .Distinct());

            Info($"Fetching {"visit detail".ToQuantity(plannedTripVisits.Count)} ...");

            var stopwatch = Stopwatch.StartNew();

            var plannedVisitsWithStartTimes = plannedTripVisits
                .Select(v => _client.Get(new GetFieldVisit {Id = v.Id}))
                .ToList();

            Info($"Fetched {"visit".ToQuantity(plannedTripVisits.Count)} in {stopwatch.Elapsed.Humanize(2)}");

            var tripsWithPlannedVisits = plannedTripIds
                .ToDictionary(
                    tripId => tripId,
                    tripId => plannedVisitsWithStartTimes
                        .Where(v => tripId == v.FieldTrip.Id && IsCandidateVisit(v))
                        .ToList());

            var items = tripsWithPlannedVisits
                .SelectMany(t => t.Value)
                .Select(v => new TableItem
                {
                    Trip = v.FieldTrip.CustomId,
                    Start = v.StartTime.Value.UtcDateTime.Add(DateTimeOffset.Now.Offset),
                    Location = v.SamplingLocation.CustomId,
                    Specimens = $"{v.PlannedActivities.SelectMany(a => a.ActivityTemplate.SpecimenTemplates).Count()} specimens",
                    Visit = v
                })
                .OrderBy(i => i.Trip)
                .ThenBy(i => i.Start)
                .ToList();

            tripDataGridView.DataSource = items;
            tripDataGridView.Columns[0].SortMode = DataGridViewColumnSortMode.Automatic;
            tripDataGridView.Columns[1].SortMode = DataGridViewColumnSortMode.Automatic;
            tripDataGridView.Columns[2].SortMode = DataGridViewColumnSortMode.Automatic;
            tripDataGridView.Columns[3].SortMode = DataGridViewColumnSortMode.NotSortable;
            tripDataGridView.Columns[4].Visible = false;
            tripDataGridView.Enabled = true;

            Info($"{"trip".ToQuantity(tripsWithPlannedVisits.Sum(k => k.Value.Count))} with planned visits.");
        }

        // Heacked until 2020.6 is releases with true pagination support in the DTOs
        [DataContract]
        [Route("/v1/fieldvisits", "GET")]
        public class HackedGetFieldVisits : IReturn<SearchResultFieldVisitSimple>, IPaginatedRequest
        {
            [DataMember(Name = "cursor")]
            public string Cursor { get; set; }
            [DataMember(Name = "end-startTime")]
            public Instant? EndStartTime { get; set; }
            [DataMember(Name = "fieldTripIds")]
            public List<string> FieldTripIds { get; set; }
            [DataMember(Name = "ids")]
            public List<string> Ids { get; set; }
            [DataMember(Name = "limit")]
            public int? Limit { get; set; }
            [DataMember(Name = "planningStatuses")]
            public List<string> PlanningStatuses { get; set; }
            [DataMember(Name = "projectIds")]
            public List<string> ProjectIds { get; set; }
            [DataMember(Name = "samplingLocationIds")]
            public List<string> SamplingLocationIds { get; set; }
            [DataMember(Name = "scheduleIds")]
            public List<string> ScheduleIds { get; set; }
            [DataMember(Name = "search")]
            public List<string> Search { get; set; }
            [DataMember(Name = "sort")]
            public string Sort { get; set; }
            [DataMember(Name = "start-startTime")]
            public Instant? StartStartTime { get; set; }
        }

        private bool IsCandidateVisit(FieldVisit visit)
        {
            return visit.FieldTrip != null
                   && visit.PlanningStatus == PlanningStatusType.PLANNED
                   && visit.StartTime.HasValue
                   && visit.PlannedActivities.Any(a => a.ActivityTemplate.SpecimenTemplates.Any());
        }

        public class TableItem
        {
            public string Trip { get; set; }
            public DateTime Start { get; set; }
            public string Location { get; set; }
            public string Specimens { get; set; }
            public FieldVisit Visit { get; set; }
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

        private void tripDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            scheduleButton.Enabled = tripDataGridView.SelectedRows.Count > 0;
        }

        private void ScheduleSelectedTrips()
        {
            var visitsToSchedule = tripDataGridView
                .SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as TableItem)
                .Where(ti => ti != null)
                .Select(ti => ti.Visit)
                .ToList();

            Info($"Scheduling {visitsToSchedule.Count} visits.");

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

            foreach (var plannedActivity in visit.PlannedActivities.Where(a => a.ActivityTemplate.SpecimenTemplates.Any()))
            {
                var activityTemplate = plannedActivity.ActivityTemplate;

                message += $" '{activityTemplate.CustomId}' with {activityTemplate.SpecimenTemplates.Count} specimens {string.Join(", ", activityTemplate.SpecimenTemplates.Select(s => s.Name))}";

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
