using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Aquarius.Helpers;
using NodaTime.Text;
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
            fieldResultPrefioxTextBox.Text = Context.FieldResultPrefix;
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

            OnServerConfigChanged();
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
            Context.BulkImportIndicator = bulkImportIndicatorTextBox.Text;
        }

        private void fieldResultPrefioxTextBox_TextChanged(object sender, EventArgs e)
        {
            Context.FieldResultPrefix = fieldResultPrefioxTextBox.Text;
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
    }
}
