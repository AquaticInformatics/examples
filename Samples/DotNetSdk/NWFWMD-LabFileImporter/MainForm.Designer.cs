namespace NWFWMDLabFileImporter
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.outputTextBox = new System.Windows.Forms.TextBox();
            this.clearButton = new System.Windows.Forms.Button();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.connectionTabPage = new System.Windows.Forms.TabPage();
            this.apiTokenLinkLabel = new System.Windows.Forms.LinkLabel();
            this.serverTextBox = new System.Windows.Forms.TextBox();
            this.serverLabel = new System.Windows.Forms.Label();
            this.apiTokenTextBox = new System.Windows.Forms.TextBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.importOptionsTabPage = new System.Windows.Forms.TabPage();
            this.label4 = new System.Windows.Forms.Label();
            this.utcOffsetComboBox = new System.Windows.Forms.ComboBox();
            this.endDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.startDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.errorLimitNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.stopOnFirstErrorCheckBox = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.locationAliasesTabPage = new System.Windows.Forms.TabPage();
            this.locationAliasesLabel = new System.Windows.Forms.Label();
            this.locationAliasesListView = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.propertyAliasesTabPage = new System.Windows.Forms.TabPage();
            this.propertyAliasesLabel = new System.Windows.Forms.Label();
            this.propertyAliasesListView = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader5 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.methodAliasesTabPage = new System.Windows.Forms.TabPage();
            this.methodAliasesListView = new System.Windows.Forms.ListView();
            this.columnHeader7 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader8 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.methodAliasesLabel = new System.Windows.Forms.Label();
            this.unitAliasesTabPage = new System.Windows.Forms.TabPage();
            this.unitAliasesListView = new System.Windows.Forms.ListView();
            this.columnHeader9 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader10 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.unitAliasesLabel = new System.Windows.Forms.Label();
            this.csvOutputTabPage = new System.Windows.Forms.TabPage();
            this.label19 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.overwriteCheckBox = new System.Windows.Forms.CheckBox();
            this.browseCsvOutputButton = new System.Windows.Forms.Button();
            this.CsvOutputPathTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.importButton = new System.Windows.Forms.Button();
            this.dryRunCheckBox = new System.Windows.Forms.CheckBox();
            this.verboseErrorsCheckBox = new System.Windows.Forms.CheckBox();
            this.resultGradeTextBox = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.labResultStatusTextBox = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.defaultLaboratoryTextBox = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.defaultMediumTextBox = new System.Windows.Forms.TextBox();
            this.nonDetectConditionTextBox = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.maxObservationsCheckBox = new System.Windows.Forms.CheckBox();
            this.maxObservationsNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.tabControl.SuspendLayout();
            this.connectionTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.importOptionsTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorLimitNumericUpDown)).BeginInit();
            this.locationAliasesTabPage.SuspendLayout();
            this.propertyAliasesTabPage.SuspendLayout();
            this.methodAliasesTabPage.SuspendLayout();
            this.unitAliasesTabPage.SuspendLayout();
            this.csvOutputTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxObservationsNumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // outputTextBox
            // 
            this.outputTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.outputTextBox.Location = new System.Drawing.Point(12, 287);
            this.outputTextBox.Multiline = true;
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.outputTextBox.Size = new System.Drawing.Size(717, 65);
            this.outputTextBox.TabIndex = 0;
            // 
            // clearButton
            // 
            this.clearButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.clearButton.Location = new System.Drawing.Point(12, 358);
            this.clearButton.Name = "clearButton";
            this.clearButton.Size = new System.Drawing.Size(75, 23);
            this.clearButton.TabIndex = 1;
            this.clearButton.Text = "Clear";
            this.clearButton.UseVisualStyleBackColor = true;
            this.clearButton.MouseClick += new System.Windows.Forms.MouseEventHandler(this.clearButton_MouseClick);
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.connectionTabPage);
            this.tabControl.Controls.Add(this.importOptionsTabPage);
            this.tabControl.Controls.Add(this.locationAliasesTabPage);
            this.tabControl.Controls.Add(this.propertyAliasesTabPage);
            this.tabControl.Controls.Add(this.methodAliasesTabPage);
            this.tabControl.Controls.Add(this.unitAliasesTabPage);
            this.tabControl.Controls.Add(this.csvOutputTabPage);
            this.tabControl.Location = new System.Drawing.Point(13, 13);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(716, 243);
            this.tabControl.TabIndex = 2;
            // 
            // connectionTabPage
            // 
            this.connectionTabPage.Controls.Add(this.apiTokenLinkLabel);
            this.connectionTabPage.Controls.Add(this.serverTextBox);
            this.connectionTabPage.Controls.Add(this.serverLabel);
            this.connectionTabPage.Controls.Add(this.apiTokenTextBox);
            this.connectionTabPage.Controls.Add(this.pictureBox1);
            this.connectionTabPage.Location = new System.Drawing.Point(4, 22);
            this.connectionTabPage.Name = "connectionTabPage";
            this.connectionTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.connectionTabPage.Size = new System.Drawing.Size(708, 217);
            this.connectionTabPage.TabIndex = 0;
            this.connectionTabPage.Text = "Connection";
            this.connectionTabPage.UseVisualStyleBackColor = true;
            // 
            // apiTokenLinkLabel
            // 
            this.apiTokenLinkLabel.AutoSize = true;
            this.apiTokenLinkLabel.Location = new System.Drawing.Point(12, 41);
            this.apiTokenLinkLabel.Name = "apiTokenLinkLabel";
            this.apiTokenLinkLabel.Size = new System.Drawing.Size(61, 13);
            this.apiTokenLinkLabel.TabIndex = 49;
            this.apiTokenLinkLabel.TabStop = true;
            this.apiTokenLinkLabel.Text = "API Token:";
            this.apiTokenLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.apiTokenLinkLabel_LinkClicked);
            // 
            // serverTextBox
            // 
            this.serverTextBox.Location = new System.Drawing.Point(75, 13);
            this.serverTextBox.Name = "serverTextBox";
            this.serverTextBox.Size = new System.Drawing.Size(175, 20);
            this.serverTextBox.TabIndex = 48;
            this.serverTextBox.TextChanged += new System.EventHandler(this.serverTextBox_TextChanged);
            // 
            // serverLabel
            // 
            this.serverLabel.AutoSize = true;
            this.serverLabel.Location = new System.Drawing.Point(12, 15);
            this.serverLabel.Name = "serverLabel";
            this.serverLabel.Size = new System.Drawing.Size(41, 13);
            this.serverLabel.TabIndex = 47;
            this.serverLabel.Text = "Server:";
            // 
            // apiTokenTextBox
            // 
            this.apiTokenTextBox.Location = new System.Drawing.Point(75, 38);
            this.apiTokenTextBox.Name = "apiTokenTextBox";
            this.apiTokenTextBox.PasswordChar = '*';
            this.apiTokenTextBox.Size = new System.Drawing.Size(175, 20);
            this.apiTokenTextBox.TabIndex = 46;
            this.apiTokenTextBox.TextChanged += new System.EventHandler(this.apiTokenTextBox_TextChanged);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(256, 6);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(209, 65);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // importOptionsTabPage
            // 
            this.importOptionsTabPage.Controls.Add(this.maxObservationsCheckBox);
            this.importOptionsTabPage.Controls.Add(this.maxObservationsNumericUpDown);
            this.importOptionsTabPage.Controls.Add(this.nonDetectConditionTextBox);
            this.importOptionsTabPage.Controls.Add(this.label14);
            this.importOptionsTabPage.Controls.Add(this.label13);
            this.importOptionsTabPage.Controls.Add(this.defaultMediumTextBox);
            this.importOptionsTabPage.Controls.Add(this.label12);
            this.importOptionsTabPage.Controls.Add(this.defaultLaboratoryTextBox);
            this.importOptionsTabPage.Controls.Add(this.label11);
            this.importOptionsTabPage.Controls.Add(this.labResultStatusTextBox);
            this.importOptionsTabPage.Controls.Add(this.resultGradeTextBox);
            this.importOptionsTabPage.Controls.Add(this.label8);
            this.importOptionsTabPage.Controls.Add(this.label4);
            this.importOptionsTabPage.Controls.Add(this.utcOffsetComboBox);
            this.importOptionsTabPage.Controls.Add(this.endDateTimePicker);
            this.importOptionsTabPage.Controls.Add(this.startDateTimePicker);
            this.importOptionsTabPage.Controls.Add(this.errorLimitNumericUpDown);
            this.importOptionsTabPage.Controls.Add(this.stopOnFirstErrorCheckBox);
            this.importOptionsTabPage.Controls.Add(this.label7);
            this.importOptionsTabPage.Controls.Add(this.label6);
            this.importOptionsTabPage.Controls.Add(this.label5);
            this.importOptionsTabPage.Location = new System.Drawing.Point(4, 22);
            this.importOptionsTabPage.Name = "importOptionsTabPage";
            this.importOptionsTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.importOptionsTabPage.Size = new System.Drawing.Size(708, 217);
            this.importOptionsTabPage.TabIndex = 1;
            this.importOptionsTabPage.Text = "Import Options";
            this.importOptionsTabPage.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(62, 171);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(60, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "UTC Offset";
            // 
            // utcOffsetComboBox
            // 
            this.utcOffsetComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.utcOffsetComboBox.FormattingEnabled = true;
            this.utcOffsetComboBox.Location = new System.Drawing.Point(129, 168);
            this.utcOffsetComboBox.Name = "utcOffsetComboBox";
            this.utcOffsetComboBox.Size = new System.Drawing.Size(121, 21);
            this.utcOffsetComboBox.TabIndex = 14;
            this.utcOffsetComboBox.SelectionChangeCommitted += new System.EventHandler(this.utcOffsetComboBox_SelectionChangeCommitted);
            // 
            // endDateTimePicker
            // 
            this.endDateTimePicker.Location = new System.Drawing.Point(128, 136);
            this.endDateTimePicker.Name = "endDateTimePicker";
            this.endDateTimePicker.ShowCheckBox = true;
            this.endDateTimePicker.Size = new System.Drawing.Size(200, 20);
            this.endDateTimePicker.TabIndex = 13;
            this.endDateTimePicker.ValueChanged += new System.EventHandler(this.endDateTimePicker_ValueChanged);
            // 
            // startDateTimePicker
            // 
            this.startDateTimePicker.Location = new System.Drawing.Point(128, 110);
            this.startDateTimePicker.Name = "startDateTimePicker";
            this.startDateTimePicker.ShowCheckBox = true;
            this.startDateTimePicker.Size = new System.Drawing.Size(200, 20);
            this.startDateTimePicker.TabIndex = 12;
            this.startDateTimePicker.ValueChanged += new System.EventHandler(this.startDateTimePicker_ValueChanged);
            // 
            // errorLimitNumericUpDown
            // 
            this.errorLimitNumericUpDown.Location = new System.Drawing.Point(128, 45);
            this.errorLimitNumericUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.errorLimitNumericUpDown.Name = "errorLimitNumericUpDown";
            this.errorLimitNumericUpDown.Size = new System.Drawing.Size(91, 20);
            this.errorLimitNumericUpDown.TabIndex = 11;
            this.errorLimitNumericUpDown.ValueChanged += new System.EventHandler(this.errorLimitNumericUpDown_ValueChanged);
            // 
            // stopOnFirstErrorCheckBox
            // 
            this.stopOnFirstErrorCheckBox.AutoSize = true;
            this.stopOnFirstErrorCheckBox.Location = new System.Drawing.Point(129, 22);
            this.stopOnFirstErrorCheckBox.Name = "stopOnFirstErrorCheckBox";
            this.stopOnFirstErrorCheckBox.Size = new System.Drawing.Size(112, 17);
            this.stopOnFirstErrorCheckBox.TabIndex = 10;
            this.stopOnFirstErrorCheckBox.Text = "Stop On First Error";
            this.stopOnFirstErrorCheckBox.UseVisualStyleBackColor = true;
            this.stopOnFirstErrorCheckBox.CheckedChanged += new System.EventHandler(this.stopOnFirstErrorCheckBox_CheckedChanged);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(46, 141);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(76, 13);
            this.label7.TabIndex = 9;
            this.label7.Text = "Include Before";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(55, 115);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(67, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "Include After";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(69, 45);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(53, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Error Limit";
            // 
            // locationAliasesTabPage
            // 
            this.locationAliasesTabPage.Controls.Add(this.locationAliasesLabel);
            this.locationAliasesTabPage.Controls.Add(this.locationAliasesListView);
            this.locationAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.locationAliasesTabPage.Name = "locationAliasesTabPage";
            this.locationAliasesTabPage.Size = new System.Drawing.Size(708, 217);
            this.locationAliasesTabPage.TabIndex = 3;
            this.locationAliasesTabPage.Text = "Location Aliases";
            this.locationAliasesTabPage.UseVisualStyleBackColor = true;
            this.locationAliasesTabPage.Visible = false;
            // 
            // locationAliasesLabel
            // 
            this.locationAliasesLabel.AutoSize = true;
            this.locationAliasesLabel.Location = new System.Drawing.Point(4, 4);
            this.locationAliasesLabel.Name = "locationAliasesLabel";
            this.locationAliasesLabel.Size = new System.Drawing.Size(0, 13);
            this.locationAliasesLabel.TabIndex = 1;
            // 
            // locationAliasesListView
            // 
            this.locationAliasesListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.locationAliasesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.locationAliasesListView.HideSelection = false;
            this.locationAliasesListView.Location = new System.Drawing.Point(4, 26);
            this.locationAliasesListView.Name = "locationAliasesListView";
            this.locationAliasesListView.Size = new System.Drawing.Size(701, 188);
            this.locationAliasesListView.TabIndex = 0;
            this.locationAliasesListView.UseCompatibleStateImageBehavior = false;
            this.locationAliasesListView.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "External Location";
            this.columnHeader3.Width = 200;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Samples Location ID";
            this.columnHeader4.Width = 200;
            // 
            // propertyAliasesTabPage
            // 
            this.propertyAliasesTabPage.Controls.Add(this.propertyAliasesLabel);
            this.propertyAliasesTabPage.Controls.Add(this.propertyAliasesListView);
            this.propertyAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.propertyAliasesTabPage.Name = "propertyAliasesTabPage";
            this.propertyAliasesTabPage.Size = new System.Drawing.Size(708, 217);
            this.propertyAliasesTabPage.TabIndex = 4;
            this.propertyAliasesTabPage.Text = "Property Aliases";
            this.propertyAliasesTabPage.UseVisualStyleBackColor = true;
            this.propertyAliasesTabPage.Visible = false;
            // 
            // propertyAliasesLabel
            // 
            this.propertyAliasesLabel.AutoSize = true;
            this.propertyAliasesLabel.Location = new System.Drawing.Point(4, 4);
            this.propertyAliasesLabel.Name = "propertyAliasesLabel";
            this.propertyAliasesLabel.Size = new System.Drawing.Size(41, 13);
            this.propertyAliasesLabel.TabIndex = 1;
            this.propertyAliasesLabel.Text = "label20";
            // 
            // propertyAliasesListView
            // 
            this.propertyAliasesListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyAliasesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader5});
            this.propertyAliasesListView.HideSelection = false;
            this.propertyAliasesListView.Location = new System.Drawing.Point(4, 25);
            this.propertyAliasesListView.Name = "propertyAliasesListView";
            this.propertyAliasesListView.Size = new System.Drawing.Size(701, 189);
            this.propertyAliasesListView.TabIndex = 0;
            this.propertyAliasesListView.UseCompatibleStateImageBehavior = false;
            this.propertyAliasesListView.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "External Property";
            this.columnHeader1.Width = 140;
            // 
            // columnHeader5
            // 
            this.columnHeader5.Text = "Samples Property ID";
            this.columnHeader5.Width = 140;
            // 
            // methodAliasesTabPage
            // 
            this.methodAliasesTabPage.Controls.Add(this.methodAliasesListView);
            this.methodAliasesTabPage.Controls.Add(this.methodAliasesLabel);
            this.methodAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.methodAliasesTabPage.Name = "methodAliasesTabPage";
            this.methodAliasesTabPage.Size = new System.Drawing.Size(708, 217);
            this.methodAliasesTabPage.TabIndex = 6;
            this.methodAliasesTabPage.Text = "Method Aliases";
            this.methodAliasesTabPage.UseVisualStyleBackColor = true;
            // 
            // methodAliasesListView
            // 
            this.methodAliasesListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.methodAliasesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader7,
            this.columnHeader8});
            this.methodAliasesListView.HideSelection = false;
            this.methodAliasesListView.Location = new System.Drawing.Point(7, 32);
            this.methodAliasesListView.Name = "methodAliasesListView";
            this.methodAliasesListView.Size = new System.Drawing.Size(698, 182);
            this.methodAliasesListView.TabIndex = 1;
            this.methodAliasesListView.UseCompatibleStateImageBehavior = false;
            this.methodAliasesListView.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader7
            // 
            this.columnHeader7.Text = "External Method";
            this.columnHeader7.Width = 200;
            // 
            // columnHeader8
            // 
            this.columnHeader8.Text = "Samples Method ID";
            this.columnHeader8.Width = 200;
            // 
            // methodAliasesLabel
            // 
            this.methodAliasesLabel.AutoSize = true;
            this.methodAliasesLabel.Location = new System.Drawing.Point(4, 4);
            this.methodAliasesLabel.Name = "methodAliasesLabel";
            this.methodAliasesLabel.Size = new System.Drawing.Size(41, 13);
            this.methodAliasesLabel.TabIndex = 0;
            this.methodAliasesLabel.Text = "label20";
            // 
            // unitAliasesTabPage
            // 
            this.unitAliasesTabPage.Controls.Add(this.unitAliasesListView);
            this.unitAliasesTabPage.Controls.Add(this.unitAliasesLabel);
            this.unitAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.unitAliasesTabPage.Name = "unitAliasesTabPage";
            this.unitAliasesTabPage.Size = new System.Drawing.Size(708, 217);
            this.unitAliasesTabPage.TabIndex = 7;
            this.unitAliasesTabPage.Text = "Unit Aliases";
            this.unitAliasesTabPage.UseVisualStyleBackColor = true;
            // 
            // unitAliasesListView
            // 
            this.unitAliasesListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.unitAliasesListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader9,
            this.columnHeader10});
            this.unitAliasesListView.HideSelection = false;
            this.unitAliasesListView.Location = new System.Drawing.Point(7, 28);
            this.unitAliasesListView.Name = "unitAliasesListView";
            this.unitAliasesListView.Size = new System.Drawing.Size(698, 186);
            this.unitAliasesListView.TabIndex = 1;
            this.unitAliasesListView.UseCompatibleStateImageBehavior = false;
            this.unitAliasesListView.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader9
            // 
            this.columnHeader9.Text = "External Unit";
            this.columnHeader9.Width = 200;
            // 
            // columnHeader10
            // 
            this.columnHeader10.Text = "Samples Unit ID";
            this.columnHeader10.Width = 200;
            // 
            // unitAliasesLabel
            // 
            this.unitAliasesLabel.AutoSize = true;
            this.unitAliasesLabel.Location = new System.Drawing.Point(4, 4);
            this.unitAliasesLabel.Name = "unitAliasesLabel";
            this.unitAliasesLabel.Size = new System.Drawing.Size(41, 13);
            this.unitAliasesLabel.TabIndex = 0;
            this.unitAliasesLabel.Text = "label21";
            // 
            // csvOutputTabPage
            // 
            this.csvOutputTabPage.Controls.Add(this.label19);
            this.csvOutputTabPage.Controls.Add(this.label18);
            this.csvOutputTabPage.Controls.Add(this.label17);
            this.csvOutputTabPage.Controls.Add(this.label16);
            this.csvOutputTabPage.Controls.Add(this.overwriteCheckBox);
            this.csvOutputTabPage.Controls.Add(this.browseCsvOutputButton);
            this.csvOutputTabPage.Controls.Add(this.CsvOutputPathTextBox);
            this.csvOutputTabPage.Location = new System.Drawing.Point(4, 22);
            this.csvOutputTabPage.Name = "csvOutputTabPage";
            this.csvOutputTabPage.Size = new System.Drawing.Size(708, 217);
            this.csvOutputTabPage.TabIndex = 5;
            this.csvOutputTabPage.Text = "CSV Output";
            this.csvOutputTabPage.UseVisualStyleBackColor = true;
            this.csvOutputTabPage.Visible = false;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(49, 143);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(230, 13);
            this.label19.TabIndex = 6;
            this.label19.Text = "- No AQUARIUS Samples credentials are given";
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(49, 126);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(181, 13);
            this.label18.TabIndex = 5;
            this.label18.Text = "- No CSV filename is specified above";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(43, 109);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(296, 13);
            this.label17.TabIndex = 4;
            this.label17.Text = "\"Observations-{yyyyMMddHHmmSS}.csv\" will be used when:";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(46, 17);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(122, 13);
            this.label16.TabIndex = 3;
            this.label16.Text = "CSV output file and path";
            // 
            // overwriteCheckBox
            // 
            this.overwriteCheckBox.AutoSize = true;
            this.overwriteCheckBox.Location = new System.Drawing.Point(46, 62);
            this.overwriteCheckBox.Name = "overwriteCheckBox";
            this.overwriteCheckBox.Size = new System.Drawing.Size(136, 17);
            this.overwriteCheckBox.TabIndex = 2;
            this.overwriteCheckBox.Text = "Overwrite existing files?";
            this.overwriteCheckBox.UseVisualStyleBackColor = true;
            this.overwriteCheckBox.CheckedChanged += new System.EventHandler(this.overwriteCheckBox_CheckedChanged);
            // 
            // browseCsvOutputButton
            // 
            this.browseCsvOutputButton.Location = new System.Drawing.Point(16, 34);
            this.browseCsvOutputButton.Name = "browseCsvOutputButton";
            this.browseCsvOutputButton.Size = new System.Drawing.Size(24, 23);
            this.browseCsvOutputButton.TabIndex = 1;
            this.browseCsvOutputButton.Text = "...";
            this.browseCsvOutputButton.UseVisualStyleBackColor = true;
            this.browseCsvOutputButton.Click += new System.EventHandler(this.browseCsvOutputButton_Click);
            // 
            // CsvOutputPathTextBox
            // 
            this.CsvOutputPathTextBox.Location = new System.Drawing.Point(46, 36);
            this.CsvOutputPathTextBox.Name = "CsvOutputPathTextBox";
            this.CsvOutputPathTextBox.Size = new System.Drawing.Size(404, 20);
            this.CsvOutputPathTextBox.TabIndex = 0;
            this.CsvOutputPathTextBox.TextChanged += new System.EventHandler(this.CsvOutputPathTextBox_TextChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(17, 259);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(708, 25);
            this.label1.TabIndex = 3;
            this.label1.Text = "Drag and drop files here to import  them to AQUARIUS Samples";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // importButton
            // 
            this.importButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.importButton.Location = new System.Drawing.Point(654, 358);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(75, 23);
            this.importButton.TabIndex = 4;
            this.importButton.Text = "Import ...";
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.MouseClick += new System.Windows.Forms.MouseEventHandler(this.importButton_MouseClick);
            // 
            // dryRunCheckBox
            // 
            this.dryRunCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.dryRunCheckBox.AutoSize = true;
            this.dryRunCheckBox.Location = new System.Drawing.Point(93, 362);
            this.dryRunCheckBox.Name = "dryRunCheckBox";
            this.dryRunCheckBox.Size = new System.Drawing.Size(202, 17);
            this.dryRunCheckBox.TabIndex = 5;
            this.dryRunCheckBox.Text = "Dry-Run mode (no changes imported)";
            this.dryRunCheckBox.UseVisualStyleBackColor = true;
            this.dryRunCheckBox.CheckedChanged += new System.EventHandler(this.dryRunCheckBox_CheckedChanged);
            // 
            // verboseErrorsCheckBox
            // 
            this.verboseErrorsCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.verboseErrorsCheckBox.AutoSize = true;
            this.verboseErrorsCheckBox.Location = new System.Drawing.Point(302, 362);
            this.verboseErrorsCheckBox.Name = "verboseErrorsCheckBox";
            this.verboseErrorsCheckBox.Size = new System.Drawing.Size(94, 17);
            this.verboseErrorsCheckBox.TabIndex = 6;
            this.verboseErrorsCheckBox.Text = "Verbose errors";
            this.verboseErrorsCheckBox.UseVisualStyleBackColor = true;
            this.verboseErrorsCheckBox.CheckedChanged += new System.EventHandler(this.verboseErrorsCheckBox_CheckedChanged);
            // 
            // resultGradeTextBox
            // 
            this.resultGradeTextBox.Location = new System.Drawing.Point(448, 138);
            this.resultGradeTextBox.Name = "resultGradeTextBox";
            this.resultGradeTextBox.Size = new System.Drawing.Size(100, 20);
            this.resultGradeTextBox.TabIndex = 17;
            this.resultGradeTextBox.TextChanged += new System.EventHandler(this.resultGradeTextBox_TextChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(373, 141);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(69, 13);
            this.label8.TabIndex = 16;
            this.label8.Text = "Result Grade";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(351, 114);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(91, 13);
            this.label11.TabIndex = 19;
            this.label11.Text = "Lab Result Status";
            // 
            // labResultStatusTextBox
            // 
            this.labResultStatusTextBox.Location = new System.Drawing.Point(448, 111);
            this.labResultStatusTextBox.Name = "labResultStatusTextBox";
            this.labResultStatusTextBox.Size = new System.Drawing.Size(100, 20);
            this.labResultStatusTextBox.TabIndex = 18;
            this.labResultStatusTextBox.TextChanged += new System.EventHandler(this.labResultStatusTextBox_TextChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(347, 48);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(94, 13);
            this.label12.TabIndex = 21;
            this.label12.Text = "Default Laboratory";
            // 
            // defaultLaboratoryTextBox
            // 
            this.defaultLaboratoryTextBox.Location = new System.Drawing.Point(448, 45);
            this.defaultLaboratoryTextBox.Name = "defaultLaboratoryTextBox";
            this.defaultLaboratoryTextBox.Size = new System.Drawing.Size(100, 20);
            this.defaultLaboratoryTextBox.TabIndex = 20;
            this.defaultLaboratoryTextBox.TextChanged += new System.EventHandler(this.defaultLaboratoryTextBox_TextChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(361, 19);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(81, 13);
            this.label13.TabIndex = 23;
            this.label13.Text = "Default Medium";
            // 
            // defaultMediumTextBox
            // 
            this.defaultMediumTextBox.Location = new System.Drawing.Point(448, 16);
            this.defaultMediumTextBox.Name = "defaultMediumTextBox";
            this.defaultMediumTextBox.Size = new System.Drawing.Size(100, 20);
            this.defaultMediumTextBox.TabIndex = 22;
            this.defaultMediumTextBox.TextChanged += new System.EventHandler(this.defaultMediumTextBox_TextChanged);
            // 
            // nonDetectConditionTextBox
            // 
            this.nonDetectConditionTextBox.Location = new System.Drawing.Point(447, 168);
            this.nonDetectConditionTextBox.Name = "nonDetectConditionTextBox";
            this.nonDetectConditionTextBox.Size = new System.Drawing.Size(100, 20);
            this.nonDetectConditionTextBox.TabIndex = 25;
            this.nonDetectConditionTextBox.TextChanged += new System.EventHandler(this.nonDetectConditionTextBox_TextChanged);
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(332, 171);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(109, 13);
            this.label14.TabIndex = 24;
            this.label14.Text = "Non-Detect Condition";
            // 
            // maxObservationsCheckBox
            // 
            this.maxObservationsCheckBox.AutoSize = true;
            this.maxObservationsCheckBox.Location = new System.Drawing.Point(14, 73);
            this.maxObservationsCheckBox.Name = "maxObservationsCheckBox";
            this.maxObservationsCheckBox.Size = new System.Drawing.Size(114, 17);
            this.maxObservationsCheckBox.TabIndex = 27;
            this.maxObservationsCheckBox.Text = "Max. Observations";
            this.maxObservationsCheckBox.UseVisualStyleBackColor = true;
            this.maxObservationsCheckBox.CheckedChanged += new System.EventHandler(this.maxObservationsCheckBox_CheckedChanged);
            // 
            // maxObservationsNumericUpDown
            // 
            this.maxObservationsNumericUpDown.Location = new System.Drawing.Point(129, 71);
            this.maxObservationsNumericUpDown.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.maxObservationsNumericUpDown.Name = "maxObservationsNumericUpDown";
            this.maxObservationsNumericUpDown.Size = new System.Drawing.Size(96, 20);
            this.maxObservationsNumericUpDown.TabIndex = 26;
            this.maxObservationsNumericUpDown.ValueChanged += new System.EventHandler(this.maxObservationsNumericUpDown_ValueChanged);
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(741, 393);
            this.Controls.Add(this.verboseErrorsCheckBox);
            this.Controls.Add(this.dryRunCheckBox);
            this.Controls.Add(this.importButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.clearButton);
            this.Controls.Add(this.outputTextBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(520, 420);
            this.Name = "MainForm";
            this.Text = "Lab File Importer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainForm_DragDrop);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.MainForm_DragOver);
            this.tabControl.ResumeLayout(false);
            this.connectionTabPage.ResumeLayout(false);
            this.connectionTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.importOptionsTabPage.ResumeLayout(false);
            this.importOptionsTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorLimitNumericUpDown)).EndInit();
            this.locationAliasesTabPage.ResumeLayout(false);
            this.locationAliasesTabPage.PerformLayout();
            this.propertyAliasesTabPage.ResumeLayout(false);
            this.propertyAliasesTabPage.PerformLayout();
            this.methodAliasesTabPage.ResumeLayout(false);
            this.methodAliasesTabPage.PerformLayout();
            this.unitAliasesTabPage.ResumeLayout(false);
            this.unitAliasesTabPage.PerformLayout();
            this.csvOutputTabPage.ResumeLayout(false);
            this.csvOutputTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.maxObservationsNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox outputTextBox;
        private System.Windows.Forms.Button clearButton;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage connectionTabPage;
        private System.Windows.Forms.TabPage importOptionsTabPage;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.TabPage locationAliasesTabPage;
        private System.Windows.Forms.TabPage propertyAliasesTabPage;
        private System.Windows.Forms.TabPage csvOutputTabPage;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.LinkLabel apiTokenLinkLabel;
        private System.Windows.Forms.TextBox serverTextBox;
        private System.Windows.Forms.Label serverLabel;
        private System.Windows.Forms.TextBox apiTokenTextBox;
        private System.Windows.Forms.DateTimePicker endDateTimePicker;
        private System.Windows.Forms.DateTimePicker startDateTimePicker;
        private System.Windows.Forms.NumericUpDown errorLimitNumericUpDown;
        private System.Windows.Forms.CheckBox stopOnFirstErrorCheckBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox utcOffsetComboBox;
        private System.Windows.Forms.CheckBox dryRunCheckBox;
        private System.Windows.Forms.CheckBox verboseErrorsCheckBox;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.CheckBox overwriteCheckBox;
        private System.Windows.Forms.Button browseCsvOutputButton;
        private System.Windows.Forms.TextBox CsvOutputPathTextBox;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label locationAliasesLabel;
        private System.Windows.Forms.ListView locationAliasesListView;
        private System.Windows.Forms.Label propertyAliasesLabel;
        private System.Windows.Forms.ListView propertyAliasesListView;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.TabPage methodAliasesTabPage;
        private System.Windows.Forms.ListView methodAliasesListView;
        private System.Windows.Forms.ColumnHeader columnHeader7;
        private System.Windows.Forms.ColumnHeader columnHeader8;
        private System.Windows.Forms.Label methodAliasesLabel;
        private System.Windows.Forms.TabPage unitAliasesTabPage;
        private System.Windows.Forms.ListView unitAliasesListView;
        private System.Windows.Forms.ColumnHeader columnHeader9;
        private System.Windows.Forms.ColumnHeader columnHeader10;
        private System.Windows.Forms.Label unitAliasesLabel;
        private System.Windows.Forms.CheckBox maxObservationsCheckBox;
        private System.Windows.Forms.NumericUpDown maxObservationsNumericUpDown;
        private System.Windows.Forms.TextBox nonDetectConditionTextBox;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox defaultMediumTextBox;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox defaultLaboratoryTextBox;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox labResultStatusTextBox;
        private System.Windows.Forms.TextBox resultGradeTextBox;
        private System.Windows.Forms.Label label8;
    }
}