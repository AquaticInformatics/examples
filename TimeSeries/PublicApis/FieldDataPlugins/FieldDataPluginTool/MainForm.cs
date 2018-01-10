using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Aquarius.TimeSeries.Client;
using Aquarius.TimeSeries.Client.ServiceModels.Provisioning;
using log4net;
using Microsoft.Win32;
using ServiceStack;

namespace FieldDataPluginTool
{
    public partial class MainForm : Form
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MainForm()
        {
            InitializeComponent();

            usernameTextBox.Text = @"admin";
            passwordTextBox.Text = @"admin";

            disconnectButton.Enabled = false;
            addButton.Enabled = false;
            removeButton.Enabled = false;
            priorityUpButton.Enabled = false;
            priorityDownButton.Enabled = false;
            pluginListBox.Enabled = false;
        }

        private void Info(string message)
        {
            Log.Info(message);
            WriteLine($"INFO: {message}");
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

        private IAquariusClient _client;

        private void connectButton_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private readonly string _hostname = "localhost";

        private void Connect()
        {
            try
            {
                ThrowIfServerNotInstalled();
                ThrowIfWrongVersion();
                ThrowIfNotAdministrativeUser();

                _client = AquariusClient.CreateConnectedClient(_hostname, usernameTextBox.Text.Trim(),
                    passwordTextBox.Text);

                Info($"Connected to AQTS {_client.ServerVersion} on {_hostname}");

                GetAllPlugins();
            
                connectButton.Enabled = false;
                disconnectButton.Enabled = true;
                addButton.Enabled = true;
            }
            catch (Exception exception)
            {
                Error(exception);
            }
        }

        private void ThrowIfServerNotInstalled()
        {
            GetInstallPath();
        }

        private string GetInstallPath()
        {
            var installPath = (string)Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Aquatic Informatics\AQUARIUS Server",
                "InstallDir",
                null);

            if (string.IsNullOrWhiteSpace(installPath))
                throw new Exception("AQUARIUS Time-Series Server is not installed on this system.");

            installPath = Path.Combine(installPath, "FieldDataPlugins");

            if (!Directory.Exists(installPath))
                throw new Exception($"Can't find the root plugin folder at '{installPath}'");

            return installPath;
        }

        private void ThrowIfWrongVersion()
        {
            var serverType = AquariusSystemDetector.Instance.GetAquariusServerType(_hostname);

            if (serverType != AquariusServerType.NextGeneration)
                throw new Exception($"This tool only works on AQTS 201x (Detected server={serverType}).");

            var version = AquariusSystemDetector.Instance.GetAquariusServerVersion(_hostname);

            if (version.IsLessThan(_minimumVersion))
                throw new Exception($"The AQTS server must be running {_minimumVersion} or higher.");
        }

        private void ThrowIfNotAdministrativeUser()
        {
            if (!UserHasAdminPrivileges())
                throw new Exception("Please relaunch this utility with administrative rights.");
        }

        private readonly AquariusServerVersion _minimumVersion = AquariusServerVersion.Create("17.4");

        private List<FieldDataPlugin> _allPlugins;

        private void GetAllPlugins()
        {
            _allPlugins = _client.Provisioning.Get(new GetFieldDataPlugins())
                .Results;

            var pluginsDictionary = _allPlugins.ToDictionary(p => p, FormatPluginListItem);
            pluginListBox.DataSource = new BindingSource(pluginsDictionary, null);
            pluginListBox.DisplayMember = "Value";
            pluginListBox.ValueMember = "Key";
            pluginListBox.Enabled = true;

            if (_allPlugins.Any())
            {
                pluginListBox.SelectedIndex = 0;
            }
        }

        private string FormatPluginListItem(FieldDataPlugin plugin)
        {
            var description = string.IsNullOrWhiteSpace(plugin.Description)
                ? string.Empty
                : $", {plugin.Description}";

            return $"{plugin.PluginFolderName}{description}, PluginPriority={plugin.PluginPriority}";
        }

        private void disconnectButton_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (_client != null)
            {
                Info($"Disconnected from AQTS {_client.ServerVersion} on {_hostname}");

                _client.Dispose();
                _client = null;
            }

            pluginListBox.DataSource = new BindingSource(new Dictionary<FieldDataPlugin,string>(), null);
            disconnectButton.Enabled = false;
            connectButton.Enabled = true;
            addButton.Enabled = false;
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            var fileDialog = new OpenFileDialog
            {
                RestoreDirectory = true,
                Filter = @"Plugin files (*.plugin)|*.plugin|All Files(*.*)|*.*",
                Title = @"Select the Field Visit Plugin bundle to install"
            };

            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                AddFile(fileDialog.FileName);
            }
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            if (pluginListBox.SelectedIndex < 0)
                return;

            var plugin = GetPlugin(pluginListBox.SelectedIndex);

            if (!ConfirmAction($"Remove the '{plugin.PluginFolderName}' plugin from the AQTS server?"))
                return;

            DeletePlugin(plugin);
            GetAllPlugins();

            Info($"Removed the '{plugin.PluginFolderName}' plugin.");

            var folderPath = Path.Combine(GetInstallPath(), plugin.PluginFolderName);

            if (!ConfirmAction($"Also delete the '{folderPath}' folder from the server?"))
                return;

            Directory.Delete(folderPath, true);

            Info($"Deleted the '{folderPath}' folder.");
        }

        private bool ConfirmAction(string message)
        {
            var result = MessageBox.Show(this, message, @"Are you sure?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            return result == DialogResult.Yes;
        }

        private FieldDataPlugin GetPlugin(int index)
        {
            var item = (KeyValuePair<FieldDataPlugin, string>) pluginListBox.Items[index];

            return item.Key;
        }

        private void DeletePlugin(FieldDataPlugin plugin)
        {
            _client.Provisioning.Delete(new DeleteFieldDataPlugin { UniqueId = plugin.UniqueId });
        }

        private void RegisterPlugin(FieldDataPlugin plugin)
        {
            _client.Provisioning.Post(new PostFieldDataPlugin
            {
                AssemblyQualifiedTypeName = plugin.AssemblyQualifiedTypeName,
                PluginFolderName = plugin.PluginFolderName,
                Description = plugin.Description,
                PluginPriority = plugin.PluginPriority
            });
        }

        private void priorityUpButton_Click(object sender, EventArgs e)
        {
            if (pluginListBox.SelectedIndex < 1)
                return;

            SwapPluginPriority(pluginListBox.SelectedIndex, pluginListBox.SelectedIndex - 1);
        }

        private void SwapPluginPriority(int sourceIndex, int targetIndex)
        {
            var plugin1 = GetPlugin(sourceIndex);
            var plugin2 = GetPlugin(targetIndex);

            if (!ConfirmAction($"Swap the priority of the '{plugin1.PluginFolderName}' and '{plugin2.PluginFolderName}' plugins?"))
                return;

            var temp = plugin1.PluginPriority;
            plugin1.PluginPriority = plugin2.PluginPriority;
            plugin2.PluginPriority = temp;

            DeletePlugin(plugin1);
            DeletePlugin(plugin2);

            RegisterPlugin(plugin1);
            RegisterPlugin(plugin2);

            Info($"Swapped plugin priority of {plugin1.PluginFolderName} and {plugin2.PluginFolderName}.");

            GetAllPlugins();

            pluginListBox.SelectedIndex = targetIndex;
        }

        private void priorityDownButton_Click(object sender, EventArgs e)
        {
            if (pluginListBox.SelectedIndex >= pluginListBox.Items.Count - 1)
                return;

            SwapPluginPriority(pluginListBox.SelectedIndex, pluginListBox.SelectedIndex + 1);
        }

        private void pluginListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var index = pluginListBox.SelectedIndex;

            removeButton.Enabled = index >= 0;
            priorityUpButton.Enabled = index > 0;
            priorityDownButton.Enabled = index < pluginListBox.Items.Count - 1;
        }

        private void pluginListBox_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any())
                AddFile(files.First());
        }

        private void pluginListBox_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Link
                : DragDropEffects.None;
        }

        private void AddFile(string path)
        {
            if (!File.Exists(path))
                return;

            Info($"Adding {path}");

            try
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    var manifestEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.Equals("manifest.json", StringComparison.InvariantCultureIgnoreCase));

                    if (manifestEntry == null)
                        throw new Exception($"Invalid plugin bundle. No manifest found.");

                    var plugin = LoadPluginFromManifest(manifestEntry);

                    var otherEntries = archive.Entries.Where(e => e != manifestEntry).ToList();

                    if (!otherEntries.Any())
                        throw new Exception($"Invalid plugin bundle. No file entries found.");

                    var installPath = GetInstallPath();

                    var targetPath = Path.Combine(installPath, plugin.PluginFolderName);

                    if (Directory.Exists(targetPath))
                    {
                        Info($"Deleting existing folder '{targetPath}' ...");
                        Directory.Delete(targetPath, true);
                    }
                    else
                    {
                        Info($"Creating new plugin folder '{targetPath}' ...");
                    }

                    Directory.CreateDirectory(targetPath);

                    foreach (var entry in otherEntries)
                    {
                        var extractedPath = Path.Combine(targetPath, entry.FullName);
                        var extractedDir = Path.GetDirectoryName(extractedPath);

                        if (!Directory.Exists(extractedDir))
                        {
                            // ReSharper disable once AssignNullToNotNullAttribute
                            Directory.CreateDirectory(extractedDir);
                        }

                        Info($"Extracting {entry.FullName} ...");
                        using (var stream = entry.Open())
                        using(var x = File.Create(extractedPath))
                        {
                            stream.CopyTo(x);
                        }
                    }

                    Info($"Registering new plugin '{plugin.PluginFolderName}' ...");
                    RegisterPlugin(plugin);
                    GetAllPlugins();

                    pluginListBox.SelectedIndex =
                        _allPlugins.IndexOf(_allPlugins.Single(p => p.PluginFolderName == plugin.PluginFolderName));
                }
            }
            catch (Exception exception)
            {
                Error(exception);
            }
        }

        private FieldDataPlugin LoadPluginFromManifest(ZipArchiveEntry manifestEntry)
        {
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var jsonText = reader.ReadToEnd();
                var plugin = jsonText.FromJson<FieldDataPlugin>();

                if (string.IsNullOrWhiteSpace(plugin.PluginFolderName))
                    throw new Exception($"Invalid plugin manifest. {nameof(plugin.PluginFolderName)} must be set.");

                if (string.IsNullOrWhiteSpace(plugin.AssemblyQualifiedTypeName))
                    throw new Exception($"Invalid plugin manifest. {nameof(plugin.AssemblyQualifiedTypeName)} must be set.");

                var conflictingPlugin = _allPlugins
                    .FirstOrDefault(p =>
                        p.PluginFolderName.Equals(plugin.PluginFolderName,
                            StringComparison.InvariantCultureIgnoreCase));

                if (conflictingPlugin != null)
                    throw new Exception(
                        $"Plugin '{plugin.PluginFolderName}' is already installed. Delete it first before reinstalling.");

                plugin.PluginPriority = 100 + _allPlugins.Max(p => p.PluginPriority);

                return plugin;
            }
        }

        private static bool UserHasAdminPrivileges()
        {
            try
            {
                Thread.GetDomain().SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
                var wp = (WindowsPrincipal)Thread.CurrentPrincipal;

                return (wp.IsInRole(WindowsBuiltInRole.Administrator));
            }
            catch
            {
                return false;
            }
        }
    }
}
