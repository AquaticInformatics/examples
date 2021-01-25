using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Aquarius.Helpers;
using Humanizer;
using ServiceStack;
using ServiceStack.Logging;

namespace LabFileImporter
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Context Context { get; set; }

        public MainForm()
        {
            InitializeComponent();

            SetupOnce();
        }

        private void SetupOnce()
        {
            Text = ExeHelper.ExeNameAndVersion;
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

        private void Error(string message)
        {
            Log.Error(message);
            WriteLine($"ERROR: {message}");
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

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            LoadContext();

            //TryImportFiles(Context.Files.ToArray());
        }

        private List<string> UtcOffsets { get; set; }

        private void LoadContext()
        {
            serverTextBox.Text = Context.ServerUrl ?? string.Empty;
            apiTokenTextBox.Text = Context.ApiToken ?? string.Empty;

            bulkImportIndicatorTextBox.Text = Context.BulkImportIndicator;
            fieldResultPrefixTextBox.Text = Context.FieldResultPrefix;
            stopOnFirstErrorCheckBox.Checked = Context.StopOnFirstError;
            errorLimitNumericUpDown.Value = Context.ErrorLimit;

            SetDateTimeControl(startDateTimePicker, Context.StartTime);
            SetDateTimeControl(endDateTimePicker, Context.EndTime);

            UtcOffsets = TimeZoneInfo
                .GetSystemTimeZones()
                .Select(z => z.BaseUtcOffset)
                .Distinct()
                .OrderBy(ts => ts)
                .Select(ts => $"{ts}")
                .ToList();

            utcOffsetComboBox.DataSource = UtcOffsets;

            var utcIndex = UtcOffsets
                .IndexOf($"{Context.UtcOffset.ToTimeSpan()}");

            utcOffsetComboBox.SelectedIndex = utcIndex;

            dryRunCheckBox.Checked = Context.DryRun;
            verboseErrorsCheckBox.Checked = Context.VerboseErrors;

            maxObservationsCheckBox.Checked = Context.MaximumObservations.HasValue;
            maxObservationsNumericUpDown.Value = Context.MaximumObservations ?? 0;

            resultGradeTextBox.Text = Context.ResultGrade;
            estimatedGradeTextBox.Text = Context.EstimatedGrade;
            fieldResultStatusTextBox.Text = Context.FieldResultStatus;
            labResultStatusTextBox.Text = Context.LabResultStatus;
            defaultLaboratoryTextBox.Text = Context.DefaultLaboratory;
            defaultMediumTextBox.Text = Context.DefaultMedium;
            nonDetectConditionTextBox.Text = Context.NonDetectCondition;
            labSpecimenNameTextBox.Text = Context.LabSpecimenName;

            overwriteCheckBox.Checked = Context.Overwrite;
            CsvOutputPathTextBox.Text = Context.CsvOutputPath;

            locationAliasesLabel.Text = $@"{"location alias".ToQuantity(Context.LocationAliases.Count)} defined.";

            foreach (var kvp in Context.LocationAliases.OrderBy(kvp => kvp.Key))
            {
                locationAliasesListView.Items.Add(CreateListViewItem(kvp.Key, kvp.Value));
            }

            propertyAliasesLabel.Text = $@"{"observed property alias".ToQuantity(Context.ObservedPropertyAliases.Count)} defined.";

            foreach (var alias in Context.ObservedPropertyAliases.Values.OrderBy(alias => alias.AliasedPropertyId).ThenBy(alias=> alias.AliasedUnitId))
            {
                propertyAliasesListView.Items.Add(CreateListViewItem(alias.AliasedPropertyId, alias.AliasedUnitId, alias.PropertyId, alias.UnitId));
            }

            methodAliasesLabel.Text = $@"{"method alias".ToQuantity(Context.MethodAliases.Count)} defined.";

            foreach (var kvp in Context.MethodAliases.OrderBy(kvp => kvp.Key))
            {
                methodAliasesListView.Items.Add(CreateListViewItem(kvp.Key, kvp.Value));
            }

            qcTypeAliasesLabel.Text = $@"{"quality control alias".ToQuantity(Context.QCTypeAliases.Count)} defined.";

            foreach (var kvp in Context.QCTypeAliases.OrderBy(kvp => kvp.Key))
            {
                qcTypeAliasesListView.Items.Add(CreateListViewItem(kvp.Key, $"{kvp.Value.QualityControlType}", $"{kvp.Value.ActivityNameSuffix}"));
            }

            OnServerConfigChanged();
        }

        private ListViewItem CreateListViewItem(params string[] columns)
        {
            if (columns.Length < 1)
                throw new InvalidOperationException("Should never happen");

            var item = new ListViewItem(columns.First());

            foreach (var column in columns.Skip(1))
            {
                item.SubItems.Add(column);
            }

            return item;
        }

        private void SetDateTimeControl(DateTimePicker dateTimePicker, DateTimeOffset? dateTimeOffset)
        {
            dateTimePicker.MinDate = new DateTime(1900, 1, 1);
            dateTimePicker.Checked = dateTimeOffset.HasValue;
            dateTimePicker.Value = dateTimeOffset?.DateTime ?? dateTimePicker.MinDate;
        }

        private void clearButton_MouseClick(object sender, MouseEventArgs e)
        {
            outputTextBox.Text = string.Empty;
            KeepOutputVisible();
        }

        private void importButton_MouseClick(object sender, MouseEventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true,
                Filter = @"Excel files (*.xlxs)|*.xlsx|All Files(*.*)|*.*",
                Title = @"Select the Excel file to import"
            };

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                TryImportFiles(fileDialog.FileNames);
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] paths)) return;

            TryImportFiles(paths);
        }

        private void MainForm_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Link
                : DragDropEffects.None;
        }

        private void TryImportFiles(string[] paths)
        {
            try
            {
                ImportFiles(paths);
            }
            catch (WebServiceException exception)
            {
                Error($"API: ({exception.StatusCode}) {string.Join(" ", exception.StatusDescription, exception.ErrorCode)}: {string.Join(" ", exception.Message, exception.ErrorMessage)}");
                Log.Error(exception);
            }
            catch (ExpectedException exception)
            {
                Error(exception.Message);
            }
            catch (Exception exception)
            {
                Error(exception);
            }
        }

        private void ImportFiles(string[] paths)
        {
            if (!paths.Any())
                return;

            var log = new DualLogger
            {
                InfoAction = Info,
                WarnAction = Warn,
                ErrorAction = Error
            };

            Context.Files.Clear();
            Context.Files.AddRange(paths);

            new Importer(log, Context)
                .Import();
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

        private void apiTokenLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (TryParseServerUri(serverTextBox.Text, out var uri))
            {
                System.Diagnostics.Process.Start($"{uri}/v1/authentication/google?type=application");
            }
        }

        private void bulkImportIndicatorTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.BulkImportIndicator = bulkImportIndicatorTextBox.Text.Trim();
        }

        private void fieldResultPrefixTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.FieldResultPrefix = fieldResultPrefixTextBox.Text.Trim();
        }

        private void stopOnFirstErrorCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Context.StopOnFirstError = stopOnFirstErrorCheckBox.Checked;
        }

        private void errorLimitNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Context.ErrorLimit = (int) errorLimitNumericUpDown.Value;
        }

        private void startDateTimePicker_ValueChanged(object sender, EventArgs e)
        {
            if (!startDateTimePicker.Checked && Context.StartTime.HasValue)
            {
                Context.StartTime = null;
            }

            if (startDateTimePicker.Checked)
            {
                Context.StartTime = new DateTimeOffset(startDateTimePicker.Value, Context.UtcOffset.ToTimeSpan());
            }
        }

        private void endDateTimePicker_ValueChanged(object sender, EventArgs e)
        {
            if (!endDateTimePicker.Checked && Context.EndTime.HasValue)
            {
                Context.EndTime = null;
            }

            if (endDateTimePicker.Checked)
            {
                Context.EndTime = new DateTimeOffset(endDateTimePicker.Value, Context.UtcOffset.ToTimeSpan());
            }
        }

        private void utcOffsetComboBox_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (!(utcOffsetComboBox.SelectedItem is string text))
                return;

            if (string.IsNullOrEmpty(text))
                return;

            Context.UtcOffset = Program.ParseOffset(text);
        }

        private void dryRunCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Context.DryRun = dryRunCheckBox.Checked;
        }

        private void verboseErrorsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Context.VerboseErrors = verboseErrorsCheckBox.Checked;
        }

        private void resultGradeTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.ResultGrade = resultGradeTextBox.Text.Trim();
        }

        private void estimatedGradeTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.EstimatedGrade = estimatedGradeTextBox.Text.Trim();
        }

        private void fieldResultStatusTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.FieldResultStatus = fieldResultStatusTextBox.Text.Trim();
        }

        private void labResultStatusTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.LabResultStatus = labResultStatusTextBox.Text.Trim();
        }

        private void defaultLaboratoryTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.DefaultLaboratory = defaultLaboratoryTextBox.Text.Trim();
        }

        private void defaultMediumTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.DefaultMedium = defaultMediumTextBox.Text.Trim();
        }

        private void nonDetectConditionTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.NonDetectCondition = nonDetectConditionTextBox.Text.Trim();
        }

        private void labSpecimenNameTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.LabSpecimenName = labSpecimenNameTextBox.Text.Trim();
        }

        private void maxObservationsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Context.MaximumObservations = maxObservationsCheckBox.Checked
                ? (int?) maxObservationsNumericUpDown.Value
                : null;
        }

        private void maxObservationsNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Context.MaximumObservations = (int)maxObservationsNumericUpDown.Value;
        }

        private void overwriteCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Context.Overwrite = overwriteCheckBox.Checked;
        }

        private void CsvOutputPathTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.CsvOutputPath = CsvOutputPathTextBox.Text.Trim();
        }

        private void browseCsvOutputButton_Click(object sender, EventArgs e)
        {
            var fileDialog = new SaveFileDialog
            {
                RestoreDirectory = true,
                Filter = @"CSV files (*.csv)|*.xlsx|All Files(*.*)|*.*",
                Title = @"Choose a CSV file for the exported observations"
            };

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(fileDialog.FileName))
                {
                    Context.Overwrite = true;
                    overwriteCheckBox.Checked = true;
                }

                Context.CsvOutputPath = fileDialog.FileName;
                CsvOutputPathTextBox.Text = fileDialog.FileName;
            }

        }
    }
}
