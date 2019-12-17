namespace SamplesTripScheduler
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
            this.serverTextBox = new System.Windows.Forms.TextBox();
            this.serverLabel = new System.Windows.Forms.Label();
            this.pictureBoxAqTs = new System.Windows.Forms.PictureBox();
            this.scheduleButton = new System.Windows.Forms.Button();
            this.loadButton = new System.Windows.Forms.Button();
            this.clearButton = new System.Windows.Forms.Button();
            this.outputTextBox = new System.Windows.Forms.TextBox();
            this.tripListBox = new System.Windows.Forms.ListBox();
            this.disconnectButton = new System.Windows.Forms.Button();
            this.connectButton = new System.Windows.Forms.Button();
            this.connectionLabel = new System.Windows.Forms.Label();
            this.apiTokenTextBox = new System.Windows.Forms.TextBox();
            this.apiTokenLinkLabel = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAqTs)).BeginInit();
            this.SuspendLayout();
            // 
            // serverTextBox
            // 
            this.serverTextBox.Location = new System.Drawing.Point(75, 6);
            this.serverTextBox.Name = "serverTextBox";
            this.serverTextBox.Size = new System.Drawing.Size(144, 20);
            this.serverTextBox.TabIndex = 44;
            this.serverTextBox.TextChanged += new System.EventHandler(this.serverTextBox_TextChanged);
            // 
            // serverLabel
            // 
            this.serverLabel.AutoSize = true;
            this.serverLabel.Location = new System.Drawing.Point(12, 8);
            this.serverLabel.Name = "serverLabel";
            this.serverLabel.Size = new System.Drawing.Size(41, 13);
            this.serverLabel.TabIndex = 43;
            this.serverLabel.Text = "Server:";
            // 
            // pictureBoxAqTs
            // 
            this.pictureBoxAqTs.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxAqTs.Image = ((System.Drawing.Image)(resources.GetObject("pictureBoxAqTs.Image")));
            this.pictureBoxAqTs.Location = new System.Drawing.Point(306, 5);
            this.pictureBoxAqTs.Name = "pictureBoxAqTs";
            this.pictureBoxAqTs.Padding = new System.Windows.Forms.Padding(2);
            this.pictureBoxAqTs.Size = new System.Drawing.Size(145, 52);
            this.pictureBoxAqTs.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxAqTs.TabIndex = 42;
            this.pictureBoxAqTs.TabStop = false;
            // 
            // scheduleButton
            // 
            this.scheduleButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.scheduleButton.Location = new System.Drawing.Point(306, 201);
            this.scheduleButton.Name = "scheduleButton";
            this.scheduleButton.Size = new System.Drawing.Size(145, 23);
            this.scheduleButton.TabIndex = 39;
            this.scheduleButton.Text = "Schedule selected trips ...";
            this.scheduleButton.UseVisualStyleBackColor = true;
            this.scheduleButton.Click += new System.EventHandler(this.scheduleButton_Click);
            // 
            // loadButton
            // 
            this.loadButton.Location = new System.Drawing.Point(15, 201);
            this.loadButton.Name = "loadButton";
            this.loadButton.Size = new System.Drawing.Size(128, 23);
            this.loadButton.TabIndex = 38;
            this.loadButton.Text = "Load planned trips ...";
            this.loadButton.UseVisualStyleBackColor = true;
            this.loadButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // clearButton
            // 
            this.clearButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.clearButton.Location = new System.Drawing.Point(12, 295);
            this.clearButton.Name = "clearButton";
            this.clearButton.Size = new System.Drawing.Size(75, 23);
            this.clearButton.TabIndex = 37;
            this.clearButton.Text = "Clear";
            this.clearButton.UseVisualStyleBackColor = true;
            this.clearButton.Click += new System.EventHandler(this.clearButton_Click);
            // 
            // outputTextBox
            // 
            this.outputTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.outputTextBox.Location = new System.Drawing.Point(12, 230);
            this.outputTextBox.Multiline = true;
            this.outputTextBox.Name = "outputTextBox";
            this.outputTextBox.ReadOnly = true;
            this.outputTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.outputTextBox.Size = new System.Drawing.Size(440, 59);
            this.outputTextBox.TabIndex = 36;
            // 
            // tripListBox
            // 
            this.tripListBox.AllowDrop = true;
            this.tripListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tripListBox.FormattingEnabled = true;
            this.tripListBox.Location = new System.Drawing.Point(12, 87);
            this.tripListBox.Name = "tripListBox";
            this.tripListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.tripListBox.Size = new System.Drawing.Size(440, 108);
            this.tripListBox.TabIndex = 35;
            this.tripListBox.SelectedIndexChanged += new System.EventHandler(this.tripListBox_SelectedIndexChanged);
            // 
            // disconnectButton
            // 
            this.disconnectButton.Location = new System.Drawing.Point(225, 31);
            this.disconnectButton.Name = "disconnectButton";
            this.disconnectButton.Size = new System.Drawing.Size(75, 23);
            this.disconnectButton.TabIndex = 34;
            this.disconnectButton.Text = "Disconnect";
            this.disconnectButton.UseVisualStyleBackColor = true;
            this.disconnectButton.Click += new System.EventHandler(this.disconnectButton_Click);
            // 
            // connectButton
            // 
            this.connectButton.Location = new System.Drawing.Point(225, 6);
            this.connectButton.Name = "connectButton";
            this.connectButton.Size = new System.Drawing.Size(75, 23);
            this.connectButton.TabIndex = 33;
            this.connectButton.Text = "Connect";
            this.connectButton.UseVisualStyleBackColor = true;
            this.connectButton.Click += new System.EventHandler(this.connectButton_Click);
            // 
            // connectionLabel
            // 
            this.connectionLabel.AutoSize = true;
            this.connectionLabel.Location = new System.Drawing.Point(12, 59);
            this.connectionLabel.Name = "connectionLabel";
            this.connectionLabel.Size = new System.Drawing.Size(0, 13);
            this.connectionLabel.TabIndex = 31;
            // 
            // apiTokenTextBox
            // 
            this.apiTokenTextBox.Location = new System.Drawing.Point(75, 31);
            this.apiTokenTextBox.Name = "apiTokenTextBox";
            this.apiTokenTextBox.PasswordChar = '*';
            this.apiTokenTextBox.Size = new System.Drawing.Size(144, 20);
            this.apiTokenTextBox.TabIndex = 30;
            this.apiTokenTextBox.TextChanged += new System.EventHandler(this.apiTokenTextBox_TextChanged);
            // 
            // apiTokenLinkLabel
            // 
            this.apiTokenLinkLabel.AutoSize = true;
            this.apiTokenLinkLabel.Location = new System.Drawing.Point(12, 34);
            this.apiTokenLinkLabel.Name = "apiTokenLinkLabel";
            this.apiTokenLinkLabel.Size = new System.Drawing.Size(61, 13);
            this.apiTokenLinkLabel.TabIndex = 45;
            this.apiTokenLinkLabel.TabStop = true;
            this.apiTokenLinkLabel.Text = "API Token:";
            this.apiTokenLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.apiTokenLinkLabel_LinkClicked);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(468, 336);
            this.Controls.Add(this.apiTokenLinkLabel);
            this.Controls.Add(this.serverTextBox);
            this.Controls.Add(this.serverLabel);
            this.Controls.Add(this.pictureBoxAqTs);
            this.Controls.Add(this.scheduleButton);
            this.Controls.Add(this.loadButton);
            this.Controls.Add(this.clearButton);
            this.Controls.Add(this.outputTextBox);
            this.Controls.Add(this.tripListBox);
            this.Controls.Add(this.disconnectButton);
            this.Controls.Add(this.connectButton);
            this.Controls.Add(this.connectionLabel);
            this.Controls.Add(this.apiTokenTextBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(484, 375);
            this.Name = "MainForm";
            this.Text = "Sample Specimen Instantiator";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxAqTs)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox serverTextBox;
        private System.Windows.Forms.Label serverLabel;
        private System.Windows.Forms.PictureBox pictureBoxAqTs;
        private System.Windows.Forms.Button scheduleButton;
        private System.Windows.Forms.Button loadButton;
        private System.Windows.Forms.Button clearButton;
        private System.Windows.Forms.TextBox outputTextBox;
        private System.Windows.Forms.ListBox tripListBox;
        private System.Windows.Forms.Button disconnectButton;
        private System.Windows.Forms.Button connectButton;
        private System.Windows.Forms.Label connectionLabel;
        private System.Windows.Forms.TextBox apiTokenTextBox;
        private System.Windows.Forms.LinkLabel apiTokenLinkLabel;
    }
}

