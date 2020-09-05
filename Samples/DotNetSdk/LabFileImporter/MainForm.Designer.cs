namespace LabFileImporter
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
            this.excelParsingTabPage = new System.Windows.Forms.TabPage();
            this.imporTabPage = new System.Windows.Forms.TabPage();
            this.locationAliasesTabPage = new System.Windows.Forms.TabPage();
            this.propertyAliasesTabPage = new System.Windows.Forms.TabPage();
            this.csvOutputTabPage = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.importButton = new System.Windows.Forms.Button();
            this.bulkImportIndicatorTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.fieldResultPrefioxTextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.stopOnFirstErrorCheckBox = new System.Windows.Forms.CheckBox();
            this.errorLimitNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.startDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.endDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.utcOffsetComboBox = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tabControl.SuspendLayout();
            this.connectionTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.excelParsingTabPage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorLimitNumericUpDown)).BeginInit();
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
            this.outputTextBox.Size = new System.Drawing.Size(480, 53);
            this.outputTextBox.TabIndex = 0;
            // 
            // clearButton
            // 
            this.clearButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.clearButton.Location = new System.Drawing.Point(12, 346);
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
            this.tabControl.Controls.Add(this.excelParsingTabPage);
            this.tabControl.Controls.Add(this.imporTabPage);
            this.tabControl.Controls.Add(this.locationAliasesTabPage);
            this.tabControl.Controls.Add(this.propertyAliasesTabPage);
            this.tabControl.Controls.Add(this.csvOutputTabPage);
            this.tabControl.Location = new System.Drawing.Point(13, 13);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(479, 243);
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
            this.connectionTabPage.Size = new System.Drawing.Size(471, 217);
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
            // excelParsingTabPage
            // 
            this.excelParsingTabPage.Controls.Add(this.label4);
            this.excelParsingTabPage.Controls.Add(this.utcOffsetComboBox);
            this.excelParsingTabPage.Controls.Add(this.endDateTimePicker);
            this.excelParsingTabPage.Controls.Add(this.startDateTimePicker);
            this.excelParsingTabPage.Controls.Add(this.errorLimitNumericUpDown);
            this.excelParsingTabPage.Controls.Add(this.stopOnFirstErrorCheckBox);
            this.excelParsingTabPage.Controls.Add(this.label7);
            this.excelParsingTabPage.Controls.Add(this.label6);
            this.excelParsingTabPage.Controls.Add(this.label5);
            this.excelParsingTabPage.Controls.Add(this.label3);
            this.excelParsingTabPage.Controls.Add(this.fieldResultPrefioxTextBox);
            this.excelParsingTabPage.Controls.Add(this.label2);
            this.excelParsingTabPage.Controls.Add(this.bulkImportIndicatorTextBox);
            this.excelParsingTabPage.Location = new System.Drawing.Point(4, 22);
            this.excelParsingTabPage.Name = "excelParsingTabPage";
            this.excelParsingTabPage.Padding = new System.Windows.Forms.Padding(3);
            this.excelParsingTabPage.Size = new System.Drawing.Size(471, 217);
            this.excelParsingTabPage.TabIndex = 1;
            this.excelParsingTabPage.Text = "Excel Parsing";
            this.excelParsingTabPage.UseVisualStyleBackColor = true;
            // 
            // imporTabPage
            // 
            this.imporTabPage.Location = new System.Drawing.Point(4, 22);
            this.imporTabPage.Name = "imporTabPage";
            this.imporTabPage.Size = new System.Drawing.Size(471, 217);
            this.imporTabPage.TabIndex = 2;
            this.imporTabPage.Text = "Import Options";
            this.imporTabPage.UseVisualStyleBackColor = true;
            this.imporTabPage.Visible = false;
            // 
            // locationAliasesTabPage
            // 
            this.locationAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.locationAliasesTabPage.Name = "locationAliasesTabPage";
            this.locationAliasesTabPage.Size = new System.Drawing.Size(471, 217);
            this.locationAliasesTabPage.TabIndex = 3;
            this.locationAliasesTabPage.Text = "Location Aliases";
            this.locationAliasesTabPage.UseVisualStyleBackColor = true;
            this.locationAliasesTabPage.Visible = false;
            // 
            // propertyAliasesTabPage
            // 
            this.propertyAliasesTabPage.Location = new System.Drawing.Point(4, 22);
            this.propertyAliasesTabPage.Name = "propertyAliasesTabPage";
            this.propertyAliasesTabPage.Size = new System.Drawing.Size(471, 217);
            this.propertyAliasesTabPage.TabIndex = 4;
            this.propertyAliasesTabPage.Text = "Property Aliases";
            this.propertyAliasesTabPage.UseVisualStyleBackColor = true;
            this.propertyAliasesTabPage.Visible = false;
            // 
            // csvOutputTabPage
            // 
            this.csvOutputTabPage.Location = new System.Drawing.Point(4, 22);
            this.csvOutputTabPage.Name = "csvOutputTabPage";
            this.csvOutputTabPage.Size = new System.Drawing.Size(471, 217);
            this.csvOutputTabPage.TabIndex = 5;
            this.csvOutputTabPage.Text = "CSV Output";
            this.csvOutputTabPage.UseVisualStyleBackColor = true;
            this.csvOutputTabPage.Visible = false;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(17, 259);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(471, 25);
            this.label1.TabIndex = 3;
            this.label1.Text = "Drag and drop files here to import  them to AQUARIUS Samples";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // importButton
            // 
            this.importButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.importButton.Location = new System.Drawing.Point(417, 346);
            this.importButton.Name = "importButton";
            this.importButton.Size = new System.Drawing.Size(75, 23);
            this.importButton.TabIndex = 4;
            this.importButton.Text = "Import ...";
            this.importButton.UseVisualStyleBackColor = true;
            this.importButton.MouseClick += new System.Windows.Forms.MouseEventHandler(this.importButton_MouseClick);
            // 
            // bulkImportIndicatorTextBox
            // 
            this.bulkImportIndicatorTextBox.Location = new System.Drawing.Point(148, 18);
            this.bulkImportIndicatorTextBox.Name = "bulkImportIndicatorTextBox";
            this.bulkImportIndicatorTextBox.Size = new System.Drawing.Size(144, 20);
            this.bulkImportIndicatorTextBox.TabIndex = 0;
            this.bulkImportIndicatorTextBox.TextChanged += new System.EventHandler(this.bulkImportIndicatorTextBox_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(22, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(120, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "A6 Bulk Import Indicator";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(51, 44);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(91, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Field Result Prefix";
            // 
            // fieldResultPrefioxTextBox
            // 
            this.fieldResultPrefioxTextBox.Location = new System.Drawing.Point(148, 44);
            this.fieldResultPrefioxTextBox.Name = "fieldResultPrefioxTextBox";
            this.fieldResultPrefioxTextBox.Size = new System.Drawing.Size(144, 20);
            this.fieldResultPrefioxTextBox.TabIndex = 2;
            this.fieldResultPrefioxTextBox.TextChanged += new System.EventHandler(this.fieldResultPrefioxTextBox_TextChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(89, 96);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(53, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Error Limit";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(75, 122);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(67, 13);
            this.label6.TabIndex = 6;
            this.label6.Text = "Include After";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(66, 148);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(76, 13);
            this.label7.TabIndex = 9;
            this.label7.Text = "Include Before";
            // 
            // stopOnFirstErrorCheckBox
            // 
            this.stopOnFirstErrorCheckBox.AutoSize = true;
            this.stopOnFirstErrorCheckBox.Location = new System.Drawing.Point(149, 73);
            this.stopOnFirstErrorCheckBox.Name = "stopOnFirstErrorCheckBox";
            this.stopOnFirstErrorCheckBox.Size = new System.Drawing.Size(112, 17);
            this.stopOnFirstErrorCheckBox.TabIndex = 10;
            this.stopOnFirstErrorCheckBox.Text = "Stop On First Error";
            this.stopOnFirstErrorCheckBox.UseVisualStyleBackColor = true;
            this.stopOnFirstErrorCheckBox.CheckedChanged += new System.EventHandler(this.stopOnFirstErrorCheckBox_CheckedChanged);
            // 
            // errorLimitNumericUpDown
            // 
            this.errorLimitNumericUpDown.Location = new System.Drawing.Point(148, 96);
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
            // startDateTimePicker
            // 
            this.startDateTimePicker.Location = new System.Drawing.Point(148, 123);
            this.startDateTimePicker.Name = "startDateTimePicker";
            this.startDateTimePicker.ShowCheckBox = true;
            this.startDateTimePicker.Size = new System.Drawing.Size(200, 20);
            this.startDateTimePicker.TabIndex = 12;
            this.startDateTimePicker.ValueChanged += new System.EventHandler(this.startDateTimePicker_ValueChanged);
            // 
            // endDateTimePicker
            // 
            this.endDateTimePicker.Location = new System.Drawing.Point(148, 148);
            this.endDateTimePicker.Name = "endDateTimePicker";
            this.endDateTimePicker.ShowCheckBox = true;
            this.endDateTimePicker.Size = new System.Drawing.Size(200, 20);
            this.endDateTimePicker.TabIndex = 13;
            this.endDateTimePicker.ValueChanged += new System.EventHandler(this.endDateTimePicker_ValueChanged);
            // 
            // utcOffsetComboBox
            // 
            this.utcOffsetComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.utcOffsetComboBox.FormattingEnabled = true;
            this.utcOffsetComboBox.Location = new System.Drawing.Point(149, 175);
            this.utcOffsetComboBox.Name = "utcOffsetComboBox";
            this.utcOffsetComboBox.Size = new System.Drawing.Size(121, 21);
            this.utcOffsetComboBox.TabIndex = 14;
            this.utcOffsetComboBox.SelectionChangeCommitted += new System.EventHandler(this.utcOffsetComboBox_SelectionChangeCommitted);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(82, 176);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(60, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "UTC Offset";
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(504, 381);
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
            this.excelParsingTabPage.ResumeLayout(false);
            this.excelParsingTabPage.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorLimitNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox outputTextBox;
        private System.Windows.Forms.Button clearButton;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage connectionTabPage;
        private System.Windows.Forms.TabPage excelParsingTabPage;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button importButton;
        private System.Windows.Forms.TabPage imporTabPage;
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
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox fieldResultPrefioxTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox bulkImportIndicatorTextBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox utcOffsetComboBox;
    }
}