namespace EsgSignalCreator
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabControl = new System.Windows.Forms.TabPage();
            this.tabArb = new System.Windows.Forms.TabPage();
            this.grpConnection = new System.Windows.Forms.GroupBox();
            this.lblInterface = new System.Windows.Forms.Label();
            this.cmbInterface = new System.Windows.Forms.ComboBox();
            this.lblResource = new System.Windows.Forms.Label();
            this.cmbResource = new System.Windows.Forms.ComboBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.lblBoard = new System.Windows.Forms.Label();
            this.numBoard = new System.Windows.Forms.NumericUpDown();
            this.lblAddress = new System.Windows.Forms.Label();
            this.numAddress = new System.Windows.Forms.NumericUpDown();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.grpSignal = new System.Windows.Forms.GroupBox();
            this.lblFrequency = new System.Windows.Forms.Label();
            this.numFrequency = new System.Windows.Forms.NumericUpDown();
            this.cmbFreqUnit = new System.Windows.Forms.ComboBox();
            this.lblAmplitude = new System.Windows.Forms.Label();
            this.numAmplitude = new System.Windows.Forms.NumericUpDown();
            this.lblDbm = new System.Windows.Forms.Label();
            this.chkRfOn = new System.Windows.Forms.CheckBox();
            this.chkModOn = new System.Windows.Forms.CheckBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnReadback = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnIdn = new System.Windows.Forms.Button();
            this.grpScpi = new System.Windows.Forms.GroupBox();
            this.txtCommand = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.btnQuery = new System.Windows.Forms.Button();
            this.grpArb = new System.Windows.Forms.GroupBox();
            this.lblSigType = new System.Windows.Forms.Label();
            this.cmbSigType = new System.Windows.Forms.ComboBox();
            this.lblParam1 = new System.Windows.Forms.Label();
            this.numParam1 = new System.Windows.Forms.NumericUpDown();
            this.lblParam1Unit = new System.Windows.Forms.Label();
            this.lblParam2 = new System.Windows.Forms.Label();
            this.numParam2 = new System.Windows.Forms.NumericUpDown();
            this.lblParam2Unit = new System.Windows.Forms.Label();
            this.lblSampleRate = new System.Windows.Forms.Label();
            this.numSampleRate = new System.Windows.Forms.NumericUpDown();
            this.cmbSampleRateUnit = new System.Windows.Forms.ComboBox();
            this.lblLength = new System.Windows.Forms.Label();
            this.numLength = new System.Windows.Forms.NumericUpDown();
            this.lblSegName = new System.Windows.Forms.Label();
            this.txtSegName = new System.Windows.Forms.TextBox();
            this.lblScaling = new System.Windows.Forms.Label();
            this.numScaling = new System.Windows.Forms.NumericUpDown();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.btnDownloadPlay = new System.Windows.Forms.Button();
            this.btnArbOff = new System.Windows.Forms.Button();
            this.lblArbInfo = new System.Windows.Forms.Label();
            this.chartIq = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.chartSpectrum = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.btnClearLog = new System.Windows.Forms.Button();
            this.tabMain.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabArb.SuspendLayout();
            this.grpConnection.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBoard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAddress)).BeginInit();
            this.grpSignal.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFrequency)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAmplitude)).BeginInit();
            this.grpScpi.SuspendLayout();
            this.grpArb.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numParam1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numParam2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSampleRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numScaling)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartIq)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartSpectrum)).BeginInit();
            this.grpLog.SuspendLayout();
            this.SuspendLayout();
            //
            // tabMain
            //
            this.tabMain.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabMain.Controls.Add(this.tabControl);
            this.tabMain.Controls.Add(this.tabArb);
            this.tabMain.Location = new System.Drawing.Point(12, 12);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(912, 508);
            this.tabMain.TabIndex = 0;
            //
            // tabControl
            //
            this.tabControl.Controls.Add(this.grpConnection);
            this.tabControl.Controls.Add(this.grpSignal);
            this.tabControl.Controls.Add(this.grpScpi);
            this.tabControl.Location = new System.Drawing.Point(4, 22);
            this.tabControl.Name = "tabControl";
            this.tabControl.Padding = new System.Windows.Forms.Padding(3);
            this.tabControl.Size = new System.Drawing.Size(904, 482);
            this.tabControl.TabIndex = 0;
            this.tabControl.Text = "Connection && CW Control";
            this.tabControl.UseVisualStyleBackColor = true;
            //
            // tabArb
            //
            this.tabArb.Controls.Add(this.grpArb);
            this.tabArb.Controls.Add(this.chartIq);
            this.tabArb.Controls.Add(this.chartSpectrum);
            this.tabArb.Location = new System.Drawing.Point(4, 22);
            this.tabArb.Name = "tabArb";
            this.tabArb.Padding = new System.Windows.Forms.Padding(3);
            this.tabArb.Size = new System.Drawing.Size(904, 482);
            this.tabArb.TabIndex = 1;
            this.tabArb.Text = "ARB Signal Builder";
            this.tabArb.UseVisualStyleBackColor = true;
            //
            // grpConnection
            //
            this.grpConnection.Controls.Add(this.lblInterface);
            this.grpConnection.Controls.Add(this.cmbInterface);
            this.grpConnection.Controls.Add(this.lblResource);
            this.grpConnection.Controls.Add(this.cmbResource);
            this.grpConnection.Controls.Add(this.btnRefresh);
            this.grpConnection.Controls.Add(this.lblBoard);
            this.grpConnection.Controls.Add(this.numBoard);
            this.grpConnection.Controls.Add(this.lblAddress);
            this.grpConnection.Controls.Add(this.numAddress);
            this.grpConnection.Controls.Add(this.btnConnect);
            this.grpConnection.Controls.Add(this.btnDisconnect);
            this.grpConnection.Controls.Add(this.lblStatus);
            this.grpConnection.Location = new System.Drawing.Point(8, 8);
            this.grpConnection.Name = "grpConnection";
            this.grpConnection.Size = new System.Drawing.Size(560, 130);
            this.grpConnection.TabIndex = 0;
            this.grpConnection.TabStop = false;
            this.grpConnection.Text = "Connection";
            //
            // lblInterface
            //
            this.lblInterface.AutoSize = true;
            this.lblInterface.Location = new System.Drawing.Point(15, 28);
            this.lblInterface.Name = "lblInterface";
            this.lblInterface.Size = new System.Drawing.Size(52, 13);
            this.lblInterface.TabIndex = 0;
            this.lblInterface.Text = "Interface:";
            //
            // cmbInterface
            //
            this.cmbInterface.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbInterface.Location = new System.Drawing.Point(90, 25);
            this.cmbInterface.Name = "cmbInterface";
            this.cmbInterface.Size = new System.Drawing.Size(180, 21);
            this.cmbInterface.TabIndex = 1;
            this.cmbInterface.SelectedIndexChanged += new System.EventHandler(this.cmbInterface_SelectedIndexChanged);
            //
            // lblResource
            //
            this.lblResource.AutoSize = true;
            this.lblResource.Location = new System.Drawing.Point(15, 61);
            this.lblResource.Name = "lblResource";
            this.lblResource.Size = new System.Drawing.Size(58, 13);
            this.lblResource.TabIndex = 2;
            this.lblResource.Text = "Resource:";
            //
            // cmbResource
            //
            this.cmbResource.Location = new System.Drawing.Point(90, 58);
            this.cmbResource.Name = "cmbResource";
            this.cmbResource.Size = new System.Drawing.Size(340, 21);
            this.cmbResource.TabIndex = 3;
            //
            // btnRefresh
            //
            this.btnRefresh.Location = new System.Drawing.Point(440, 57);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(105, 23);
            this.btnRefresh.TabIndex = 4;
            this.btnRefresh.Text = "Find Instruments";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            //
            // lblBoard
            //
            this.lblBoard.AutoSize = true;
            this.lblBoard.Location = new System.Drawing.Point(15, 94);
            this.lblBoard.Name = "lblBoard";
            this.lblBoard.Size = new System.Drawing.Size(67, 13);
            this.lblBoard.TabIndex = 5;
            this.lblBoard.Text = "GPIB Board:";
            //
            // numBoard
            //
            this.numBoard.Location = new System.Drawing.Point(90, 92);
            this.numBoard.Maximum = new decimal(new int[] { 31, 0, 0, 0 });
            this.numBoard.Name = "numBoard";
            this.numBoard.Size = new System.Drawing.Size(50, 20);
            this.numBoard.TabIndex = 6;
            //
            // lblAddress
            //
            this.lblAddress.AutoSize = true;
            this.lblAddress.Location = new System.Drawing.Point(155, 94);
            this.lblAddress.Name = "lblAddress";
            this.lblAddress.Size = new System.Drawing.Size(85, 13);
            this.lblAddress.TabIndex = 7;
            this.lblAddress.Text = "Primary Address:";
            //
            // numAddress
            //
            this.numAddress.Location = new System.Drawing.Point(246, 92);
            this.numAddress.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
            this.numAddress.Name = "numAddress";
            this.numAddress.Size = new System.Drawing.Size(50, 20);
            this.numAddress.TabIndex = 8;
            this.numAddress.Value = new decimal(new int[] { 19, 0, 0, 0 });
            //
            // btnConnect
            //
            this.btnConnect.Location = new System.Drawing.Point(330, 90);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(100, 23);
            this.btnConnect.TabIndex = 9;
            this.btnConnect.Text = "Connect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            //
            // btnDisconnect
            //
            this.btnDisconnect.Enabled = false;
            this.btnDisconnect.Location = new System.Drawing.Point(440, 90);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(105, 23);
            this.btnDisconnect.TabIndex = 10;
            this.btnDisconnect.Text = "Disconnect";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.Color.Firebrick;
            this.lblStatus.Location = new System.Drawing.Point(285, 28);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(80, 13);
            this.lblStatus.TabIndex = 11;
            this.lblStatus.Text = "Disconnected";
            //
            // grpSignal
            //
            this.grpSignal.Controls.Add(this.lblFrequency);
            this.grpSignal.Controls.Add(this.numFrequency);
            this.grpSignal.Controls.Add(this.cmbFreqUnit);
            this.grpSignal.Controls.Add(this.lblAmplitude);
            this.grpSignal.Controls.Add(this.numAmplitude);
            this.grpSignal.Controls.Add(this.lblDbm);
            this.grpSignal.Controls.Add(this.chkRfOn);
            this.grpSignal.Controls.Add(this.chkModOn);
            this.grpSignal.Controls.Add(this.btnApply);
            this.grpSignal.Controls.Add(this.btnReadback);
            this.grpSignal.Controls.Add(this.btnReset);
            this.grpSignal.Controls.Add(this.btnIdn);
            this.grpSignal.Enabled = false;
            this.grpSignal.Location = new System.Drawing.Point(8, 144);
            this.grpSignal.Name = "grpSignal";
            this.grpSignal.Size = new System.Drawing.Size(560, 130);
            this.grpSignal.TabIndex = 1;
            this.grpSignal.TabStop = false;
            this.grpSignal.Text = "RF Carrier && CW Control";
            //
            // lblFrequency
            //
            this.lblFrequency.AutoSize = true;
            this.lblFrequency.Location = new System.Drawing.Point(15, 30);
            this.lblFrequency.Name = "lblFrequency";
            this.lblFrequency.Size = new System.Drawing.Size(60, 13);
            this.lblFrequency.TabIndex = 0;
            this.lblFrequency.Text = "Frequency:";
            //
            // numFrequency
            //
            this.numFrequency.DecimalPlaces = 6;
            this.numFrequency.Location = new System.Drawing.Point(90, 28);
            this.numFrequency.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            this.numFrequency.Name = "numFrequency";
            this.numFrequency.Size = new System.Drawing.Size(120, 20);
            this.numFrequency.TabIndex = 1;
            this.numFrequency.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            //
            // cmbFreqUnit
            //
            this.cmbFreqUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFreqUnit.Location = new System.Drawing.Point(216, 27);
            this.cmbFreqUnit.Name = "cmbFreqUnit";
            this.cmbFreqUnit.Size = new System.Drawing.Size(60, 21);
            this.cmbFreqUnit.TabIndex = 2;
            //
            // lblAmplitude
            //
            this.lblAmplitude.AutoSize = true;
            this.lblAmplitude.Location = new System.Drawing.Point(15, 63);
            this.lblAmplitude.Name = "lblAmplitude";
            this.lblAmplitude.Size = new System.Drawing.Size(58, 13);
            this.lblAmplitude.TabIndex = 3;
            this.lblAmplitude.Text = "Amplitude:";
            //
            // numAmplitude
            //
            this.numAmplitude.DecimalPlaces = 2;
            this.numAmplitude.Location = new System.Drawing.Point(90, 61);
            this.numAmplitude.Minimum = new decimal(new int[] { 144, 0, 0, -2147483648 });
            this.numAmplitude.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
            this.numAmplitude.Name = "numAmplitude";
            this.numAmplitude.Size = new System.Drawing.Size(120, 20);
            this.numAmplitude.TabIndex = 4;
            //
            // lblDbm
            //
            this.lblDbm.AutoSize = true;
            this.lblDbm.Location = new System.Drawing.Point(216, 63);
            this.lblDbm.Name = "lblDbm";
            this.lblDbm.Size = new System.Drawing.Size(30, 13);
            this.lblDbm.TabIndex = 5;
            this.lblDbm.Text = "dBm";
            //
            // chkRfOn
            //
            this.chkRfOn.AutoSize = true;
            this.chkRfOn.Location = new System.Drawing.Point(90, 95);
            this.chkRfOn.Name = "chkRfOn";
            this.chkRfOn.Size = new System.Drawing.Size(86, 17);
            this.chkRfOn.TabIndex = 6;
            this.chkRfOn.Text = "RF Output On";
            this.chkRfOn.UseVisualStyleBackColor = true;
            //
            // chkModOn
            //
            this.chkModOn.AutoSize = true;
            this.chkModOn.Location = new System.Drawing.Point(186, 95);
            this.chkModOn.Name = "chkModOn";
            this.chkModOn.Size = new System.Drawing.Size(91, 17);
            this.chkModOn.TabIndex = 7;
            this.chkModOn.Text = "Modulation On";
            this.chkModOn.UseVisualStyleBackColor = true;
            //
            // btnApply
            //
            this.btnApply.Location = new System.Drawing.Point(330, 25);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(100, 23);
            this.btnApply.TabIndex = 8;
            this.btnApply.Text = "Apply Settings";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // btnReadback
            //
            this.btnReadback.Location = new System.Drawing.Point(440, 25);
            this.btnReadback.Name = "btnReadback";
            this.btnReadback.Size = new System.Drawing.Size(105, 23);
            this.btnReadback.TabIndex = 9;
            this.btnReadback.Text = "Read Back";
            this.btnReadback.UseVisualStyleBackColor = true;
            this.btnReadback.Click += new System.EventHandler(this.btnReadback_Click);
            //
            // btnReset
            //
            this.btnReset.Location = new System.Drawing.Point(330, 58);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(100, 23);
            this.btnReset.TabIndex = 10;
            this.btnReset.Text = "Reset (*RST)";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            //
            // btnIdn
            //
            this.btnIdn.Location = new System.Drawing.Point(440, 58);
            this.btnIdn.Name = "btnIdn";
            this.btnIdn.Size = new System.Drawing.Size(105, 23);
            this.btnIdn.TabIndex = 11;
            this.btnIdn.Text = "Identify (*IDN?)";
            this.btnIdn.UseVisualStyleBackColor = true;
            this.btnIdn.Click += new System.EventHandler(this.btnIdn_Click);
            //
            // grpScpi
            //
            this.grpScpi.Controls.Add(this.txtCommand);
            this.grpScpi.Controls.Add(this.btnSend);
            this.grpScpi.Controls.Add(this.btnQuery);
            this.grpScpi.Enabled = false;
            this.grpScpi.Location = new System.Drawing.Point(8, 280);
            this.grpScpi.Name = "grpScpi";
            this.grpScpi.Size = new System.Drawing.Size(560, 65);
            this.grpScpi.TabIndex = 2;
            this.grpScpi.TabStop = false;
            this.grpScpi.Text = "Manual SCPI";
            //
            // txtCommand
            //
            this.txtCommand.Location = new System.Drawing.Point(15, 28);
            this.txtCommand.Name = "txtCommand";
            this.txtCommand.Size = new System.Drawing.Size(300, 20);
            this.txtCommand.TabIndex = 0;
            //
            // btnSend
            //
            this.btnSend.Location = new System.Drawing.Point(330, 26);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(100, 23);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Send (Write)";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            //
            // btnQuery
            //
            this.btnQuery.Location = new System.Drawing.Point(440, 26);
            this.btnQuery.Name = "btnQuery";
            this.btnQuery.Size = new System.Drawing.Size(105, 23);
            this.btnQuery.TabIndex = 2;
            this.btnQuery.Text = "Query (Write+Read)";
            this.btnQuery.UseVisualStyleBackColor = true;
            this.btnQuery.Click += new System.EventHandler(this.btnQuery_Click);
            //
            // grpArb
            //
            this.grpArb.Controls.Add(this.lblSigType);
            this.grpArb.Controls.Add(this.cmbSigType);
            this.grpArb.Controls.Add(this.lblParam1);
            this.grpArb.Controls.Add(this.numParam1);
            this.grpArb.Controls.Add(this.lblParam1Unit);
            this.grpArb.Controls.Add(this.lblParam2);
            this.grpArb.Controls.Add(this.numParam2);
            this.grpArb.Controls.Add(this.lblParam2Unit);
            this.grpArb.Controls.Add(this.lblSampleRate);
            this.grpArb.Controls.Add(this.numSampleRate);
            this.grpArb.Controls.Add(this.cmbSampleRateUnit);
            this.grpArb.Controls.Add(this.lblLength);
            this.grpArb.Controls.Add(this.numLength);
            this.grpArb.Controls.Add(this.lblSegName);
            this.grpArb.Controls.Add(this.txtSegName);
            this.grpArb.Controls.Add(this.lblScaling);
            this.grpArb.Controls.Add(this.numScaling);
            this.grpArb.Controls.Add(this.btnGenerate);
            this.grpArb.Controls.Add(this.btnDownloadPlay);
            this.grpArb.Controls.Add(this.btnArbOff);
            this.grpArb.Controls.Add(this.lblArbInfo);
            this.grpArb.Location = new System.Drawing.Point(8, 8);
            this.grpArb.Name = "grpArb";
            this.grpArb.Size = new System.Drawing.Size(300, 466);
            this.grpArb.TabIndex = 0;
            this.grpArb.TabStop = false;
            this.grpArb.Text = "Waveform Definition";
            //
            // lblSigType
            //
            this.lblSigType.AutoSize = true;
            this.lblSigType.Location = new System.Drawing.Point(15, 31);
            this.lblSigType.Name = "lblSigType";
            this.lblSigType.Size = new System.Drawing.Size(64, 13);
            this.lblSigType.TabIndex = 0;
            this.lblSigType.Text = "Signal type:";
            //
            // cmbSigType
            //
            this.cmbSigType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSigType.Location = new System.Drawing.Point(130, 28);
            this.cmbSigType.Name = "cmbSigType";
            this.cmbSigType.Size = new System.Drawing.Size(150, 21);
            this.cmbSigType.TabIndex = 1;
            this.cmbSigType.SelectedIndexChanged += new System.EventHandler(this.cmbSigType_SelectedIndexChanged);
            //
            // lblParam1
            //
            this.lblParam1.AutoSize = true;
            this.lblParam1.Location = new System.Drawing.Point(15, 64);
            this.lblParam1.Name = "lblParam1";
            this.lblParam1.Size = new System.Drawing.Size(40, 13);
            this.lblParam1.TabIndex = 2;
            this.lblParam1.Text = "Param1";
            //
            // numParam1
            //
            this.numParam1.DecimalPlaces = 3;
            this.numParam1.Location = new System.Drawing.Point(130, 62);
            this.numParam1.Maximum = new decimal(new int[] { 100000000, 0, 0, 0 });
            this.numParam1.Minimum = new decimal(new int[] { 100000000, 0, 0, -2147483648 });
            this.numParam1.Name = "numParam1";
            this.numParam1.Size = new System.Drawing.Size(110, 20);
            this.numParam1.TabIndex = 3;
            //
            // lblParam1Unit
            //
            this.lblParam1Unit.AutoSize = true;
            this.lblParam1Unit.Location = new System.Drawing.Point(246, 64);
            this.lblParam1Unit.Name = "lblParam1Unit";
            this.lblParam1Unit.Size = new System.Drawing.Size(21, 13);
            this.lblParam1Unit.TabIndex = 4;
            this.lblParam1Unit.Text = "Hz";
            //
            // lblParam2
            //
            this.lblParam2.AutoSize = true;
            this.lblParam2.Location = new System.Drawing.Point(15, 97);
            this.lblParam2.Name = "lblParam2";
            this.lblParam2.Size = new System.Drawing.Size(40, 13);
            this.lblParam2.TabIndex = 5;
            this.lblParam2.Text = "Param2";
            //
            // numParam2
            //
            this.numParam2.DecimalPlaces = 3;
            this.numParam2.Location = new System.Drawing.Point(130, 95);
            this.numParam2.Maximum = new decimal(new int[] { 100000000, 0, 0, 0 });
            this.numParam2.Name = "numParam2";
            this.numParam2.Size = new System.Drawing.Size(110, 20);
            this.numParam2.TabIndex = 6;
            //
            // lblParam2Unit
            //
            this.lblParam2Unit.AutoSize = true;
            this.lblParam2Unit.Location = new System.Drawing.Point(246, 97);
            this.lblParam2Unit.Name = "lblParam2Unit";
            this.lblParam2Unit.Size = new System.Drawing.Size(15, 13);
            this.lblParam2Unit.TabIndex = 7;
            this.lblParam2Unit.Text = "%";
            //
            // lblSampleRate
            //
            this.lblSampleRate.AutoSize = true;
            this.lblSampleRate.Location = new System.Drawing.Point(15, 130);
            this.lblSampleRate.Name = "lblSampleRate";
            this.lblSampleRate.Size = new System.Drawing.Size(96, 13);
            this.lblSampleRate.TabIndex = 8;
            this.lblSampleRate.Text = "ARB sample clock:";
            //
            // numSampleRate
            //
            this.numSampleRate.DecimalPlaces = 6;
            this.numSampleRate.Location = new System.Drawing.Point(130, 128);
            this.numSampleRate.Maximum = new decimal(new int[] { 100000000, 0, 0, 0 });
            this.numSampleRate.Name = "numSampleRate";
            this.numSampleRate.Size = new System.Drawing.Size(110, 20);
            this.numSampleRate.TabIndex = 9;
            this.numSampleRate.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // cmbSampleRateUnit
            //
            this.cmbSampleRateUnit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSampleRateUnit.Location = new System.Drawing.Point(246, 127);
            this.cmbSampleRateUnit.Name = "cmbSampleRateUnit";
            this.cmbSampleRateUnit.Size = new System.Drawing.Size(48, 21);
            this.cmbSampleRateUnit.TabIndex = 10;
            //
            // lblLength
            //
            this.lblLength.AutoSize = true;
            this.lblLength.Location = new System.Drawing.Point(15, 163);
            this.lblLength.Name = "lblLength";
            this.lblLength.Size = new System.Drawing.Size(101, 13);
            this.lblLength.TabIndex = 11;
            this.lblLength.Text = "Length (samples ~):";
            //
            // numLength
            //
            this.numLength.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            this.numLength.Location = new System.Drawing.Point(130, 161);
            this.numLength.Maximum = new decimal(new int[] { 8000000, 0, 0, 0 });
            this.numLength.Minimum = new decimal(new int[] { 60, 0, 0, 0 });
            this.numLength.Name = "numLength";
            this.numLength.Size = new System.Drawing.Size(110, 20);
            this.numLength.TabIndex = 12;
            this.numLength.Value = new decimal(new int[] { 4000, 0, 0, 0 });
            //
            // lblSegName
            //
            this.lblSegName.AutoSize = true;
            this.lblSegName.Location = new System.Drawing.Point(15, 196);
            this.lblSegName.Name = "lblSegName";
            this.lblSegName.Size = new System.Drawing.Size(89, 13);
            this.lblSegName.TabIndex = 13;
            this.lblSegName.Text = "Segment name:";
            //
            // txtSegName
            //
            this.txtSegName.Location = new System.Drawing.Point(130, 193);
            this.txtSegName.MaxLength = 23;
            this.txtSegName.Name = "txtSegName";
            this.txtSegName.Size = new System.Drawing.Size(150, 20);
            this.txtSegName.TabIndex = 14;
            this.txtSegName.Text = "SIGCREATE";
            //
            // lblScaling
            //
            this.lblScaling.AutoSize = true;
            this.lblScaling.Location = new System.Drawing.Point(15, 229);
            this.lblScaling.Name = "lblScaling";
            this.lblScaling.Size = new System.Drawing.Size(100, 13);
            this.lblScaling.TabIndex = 15;
            this.lblScaling.Text = "Runtime scaling (%):";
            //
            // numScaling
            //
            this.numScaling.DecimalPlaces = 1;
            this.numScaling.Location = new System.Drawing.Point(130, 227);
            this.numScaling.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numScaling.Name = "numScaling";
            this.numScaling.Size = new System.Drawing.Size(110, 20);
            this.numScaling.TabIndex = 16;
            this.numScaling.Value = new decimal(new int[] { 70, 0, 0, 0 });
            //
            // btnGenerate
            //
            this.btnGenerate.Location = new System.Drawing.Point(18, 262);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(262, 28);
            this.btnGenerate.TabIndex = 17;
            this.btnGenerate.Text = "Generate && Preview";
            this.btnGenerate.UseVisualStyleBackColor = true;
            this.btnGenerate.Click += new System.EventHandler(this.btnGenerate_Click);
            //
            // btnDownloadPlay
            //
            this.btnDownloadPlay.Enabled = false;
            this.btnDownloadPlay.Location = new System.Drawing.Point(18, 296);
            this.btnDownloadPlay.Name = "btnDownloadPlay";
            this.btnDownloadPlay.Size = new System.Drawing.Size(262, 28);
            this.btnDownloadPlay.TabIndex = 18;
            this.btnDownloadPlay.Text = "Download && Play on E4438C";
            this.btnDownloadPlay.UseVisualStyleBackColor = true;
            this.btnDownloadPlay.Click += new System.EventHandler(this.btnDownloadPlay_Click);
            //
            // btnArbOff
            //
            this.btnArbOff.Enabled = false;
            this.btnArbOff.Location = new System.Drawing.Point(18, 330);
            this.btnArbOff.Name = "btnArbOff";
            this.btnArbOff.Size = new System.Drawing.Size(262, 24);
            this.btnArbOff.TabIndex = 19;
            this.btnArbOff.Text = "ARB Off";
            this.btnArbOff.UseVisualStyleBackColor = true;
            this.btnArbOff.Click += new System.EventHandler(this.btnArbOff_Click);
            //
            // lblArbInfo
            //
            this.lblArbInfo.Location = new System.Drawing.Point(15, 364);
            this.lblArbInfo.Name = "lblArbInfo";
            this.lblArbInfo.Size = new System.Drawing.Size(270, 90);
            this.lblArbInfo.TabIndex = 20;
            this.lblArbInfo.Text = "No waveform generated.";
            //
            // chartIq
            //
            this.chartIq.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chartIq.BorderlineColor = System.Drawing.Color.LightGray;
            this.chartIq.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            this.chartIq.Location = new System.Drawing.Point(316, 8);
            this.chartIq.Name = "chartIq";
            this.chartIq.Size = new System.Drawing.Size(580, 228);
            this.chartIq.TabIndex = 1;
            //
            // chartSpectrum
            //
            this.chartSpectrum.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chartSpectrum.BorderlineColor = System.Drawing.Color.LightGray;
            this.chartSpectrum.BorderlineDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            this.chartSpectrum.Location = new System.Drawing.Point(316, 244);
            this.chartSpectrum.Name = "chartSpectrum";
            this.chartSpectrum.Size = new System.Drawing.Size(580, 230);
            this.chartSpectrum.TabIndex = 2;
            //
            // grpLog
            //
            this.grpLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpLog.Controls.Add(this.txtLog);
            this.grpLog.Controls.Add(this.btnClearLog);
            this.grpLog.Location = new System.Drawing.Point(12, 528);
            this.grpLog.Name = "grpLog";
            this.grpLog.Size = new System.Drawing.Size(912, 210);
            this.grpLog.TabIndex = 1;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "Log";
            //
            // txtLog
            //
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.BackColor = System.Drawing.Color.Black;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtLog.ForeColor = System.Drawing.Color.Lime;
            this.txtLog.Location = new System.Drawing.Point(15, 22);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(882, 150);
            this.txtLog.TabIndex = 0;
            //
            // btnClearLog
            //
            this.btnClearLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearLog.Location = new System.Drawing.Point(797, 178);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(100, 23);
            this.btnClearLog.TabIndex = 1;
            this.btnClearLog.Text = "Clear";
            this.btnClearLog.UseVisualStyleBackColor = true;
            this.btnClearLog.Click += new System.EventHandler(this.btnClearLog_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(936, 750);
            this.Controls.Add(this.tabMain);
            this.Controls.Add(this.grpLog);
            this.MinimumSize = new System.Drawing.Size(952, 789);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ESG-SignalCreator";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tabMain.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabArb.ResumeLayout(false);
            this.grpConnection.ResumeLayout(false);
            this.grpConnection.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBoard)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAddress)).EndInit();
            this.grpSignal.ResumeLayout(false);
            this.grpSignal.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFrequency)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAmplitude)).EndInit();
            this.grpScpi.ResumeLayout(false);
            this.grpScpi.PerformLayout();
            this.grpArb.ResumeLayout(false);
            this.grpArb.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numParam1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numParam2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSampleRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numScaling)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartIq)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartSpectrum)).EndInit();
            this.grpLog.ResumeLayout(false);
            this.grpLog.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabControl;
        private System.Windows.Forms.TabPage tabArb;
        private System.Windows.Forms.GroupBox grpConnection;
        private System.Windows.Forms.Label lblInterface;
        private System.Windows.Forms.ComboBox cmbInterface;
        private System.Windows.Forms.Label lblResource;
        private System.Windows.Forms.ComboBox cmbResource;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Label lblBoard;
        private System.Windows.Forms.NumericUpDown numBoard;
        private System.Windows.Forms.Label lblAddress;
        private System.Windows.Forms.NumericUpDown numAddress;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox grpSignal;
        private System.Windows.Forms.Label lblFrequency;
        private System.Windows.Forms.NumericUpDown numFrequency;
        private System.Windows.Forms.ComboBox cmbFreqUnit;
        private System.Windows.Forms.Label lblAmplitude;
        private System.Windows.Forms.NumericUpDown numAmplitude;
        private System.Windows.Forms.Label lblDbm;
        private System.Windows.Forms.CheckBox chkRfOn;
        private System.Windows.Forms.CheckBox chkModOn;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnReadback;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnIdn;
        private System.Windows.Forms.GroupBox grpScpi;
        private System.Windows.Forms.TextBox txtCommand;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Button btnQuery;
        private System.Windows.Forms.GroupBox grpArb;
        private System.Windows.Forms.Label lblSigType;
        private System.Windows.Forms.ComboBox cmbSigType;
        private System.Windows.Forms.Label lblParam1;
        private System.Windows.Forms.NumericUpDown numParam1;
        private System.Windows.Forms.Label lblParam1Unit;
        private System.Windows.Forms.Label lblParam2;
        private System.Windows.Forms.NumericUpDown numParam2;
        private System.Windows.Forms.Label lblParam2Unit;
        private System.Windows.Forms.Label lblSampleRate;
        private System.Windows.Forms.NumericUpDown numSampleRate;
        private System.Windows.Forms.ComboBox cmbSampleRateUnit;
        private System.Windows.Forms.Label lblLength;
        private System.Windows.Forms.NumericUpDown numLength;
        private System.Windows.Forms.Label lblSegName;
        private System.Windows.Forms.TextBox txtSegName;
        private System.Windows.Forms.Label lblScaling;
        private System.Windows.Forms.NumericUpDown numScaling;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.Button btnDownloadPlay;
        private System.Windows.Forms.Button btnArbOff;
        private System.Windows.Forms.Label lblArbInfo;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartIq;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartSpectrum;
        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Button btnClearLog;
    }
}
