using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using AudioEffectsStudio.Audio;

namespace AudioEffectsStudio
{
    /// <summary>
    /// Main application window for Audio Effects Studio
    /// </summary>
    public partial class MainForm : Form
    {
        #region Private Fields
        
        // Dynamic routing panels
        private Panel inputRoutingPanel;
        private Panel outputRoutingPanel;
        private Button addInputRoutingButton;
        private Button addOutputRoutingButton;
        
        // Controls for effects
        private Panel effectsPanel;
        private Panel combinationsPanel;
        private TrackBar intensityTrackBar;
        private Label intensityLabel;
        
        // System tray components
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayContextMenu;
        
        // Dynamic routing instances
        private List<InputRoutingInstance> inputRoutingInstances = new List<InputRoutingInstance>();
        private List<OutputRoutingInstance> outputRoutingInstances = new List<OutputRoutingInstance>();
        private List<DeviceToDeviceRoutingInstance> deviceToDeviceRoutingInstances = new List<DeviceToDeviceRoutingInstance>();
        private int nextInputId = 1;
        private int nextOutputId = 1;
        private int nextDeviceToDeviceId = 1;
        
        private readonly Dictionary<string, CheckBox> effectCheckboxes = new Dictionary<string, CheckBox>();
        private readonly string[] availableEffects = { "Echo", "Reverb", "Distortion", "Chorus", "Flanger", "PitchShift", "VocalRemoval" };
        
        #endregion
        
        #region Routing Instance Classes
        
        public class InputRoutingInstance
        {
            public int Id { get; set; }
            public Panel Panel { get; set; }
            public ComboBox DeviceComboBox { get; set; }
            public Button StartButton { get; set; }
            public Button StopButton { get; set; }
            public Button RemoveButton { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Thread AudioThread { get; set; }
        }
        
        public class OutputRoutingInstance
        {
            public int Id { get; set; }
            public Panel Panel { get; set; }
            public ComboBox DeviceComboBox { get; set; }
            public Button StartButton { get; set; }
            public Button StopButton { get; set; }
            public Button RemoveButton { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Thread AudioThread { get; set; }
        }
        
        public class DeviceToDeviceRoutingInstance
        {
            public int Id { get; set; }
            public Panel Panel { get; set; }
            public ComboBox InputDeviceComboBox { get; set; }
            public ComboBox OutputDeviceComboBox { get; set; }
            public Button StartButton { get; set; }
            public Button StopButton { get; set; }
            public Button RemoveButton { get; set; }
            public Label StatusLabel { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Thread AudioThread { get; set; }
        }
        
        #endregion
        
        #region Constructor
        
        public MainForm()
        {
            InitializeComponent();
            LoadAudioDevices();
            
            // Subscribe to form events for system tray functionality
            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;
        }
        
        #endregion
        
        #region Form Designer Generated Code
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Form settings
            this.Text = "Audio Effects Studio - Multi-Device Routing";
            this.Size = new Size(850, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
            this.AutoScroll = true;
            
            // Input Routing Section
            var inputMainGroupBox = new GroupBox
            {
                Text = "Input Source Routing (To System Default Output)",
                Location = new Point(20, 20),
                Size = new Size(800, 200),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            addInputRoutingButton = new Button
            {
                Text = "+ Add Input Route",
                Location = new Point(10, 20),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            addInputRoutingButton.Click += AddInputRoutingButton_Click;
            inputMainGroupBox.Controls.Add(addInputRoutingButton);
            
            var refreshDevicesButton = new Button
            {
                Text = "🔄 Refresh Devices",
                Location = new Point(140, 20),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(163, 190, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            refreshDevicesButton.Click += RefreshDevicesButton_Click;
            inputMainGroupBox.Controls.Add(refreshDevicesButton);
            
            inputRoutingPanel = new Panel
            {
                Location = new Point(10, 60),
                Size = new Size(780, 130),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(60, 60, 60)
            };
            inputMainGroupBox.Controls.Add(inputRoutingPanel);
            this.Controls.Add(inputMainGroupBox);
            
            // Output Routing Section
            var outputMainGroupBox = new GroupBox
            {
                Text = "Output Device Routing (From System Default Input)",
                Location = new Point(20, 240),
                Size = new Size(800, 200),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            addOutputRoutingButton = new Button
            {
                Text = "+ Add Output Route",
                Location = new Point(10, 20),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            addOutputRoutingButton.Click += AddOutputRoutingButton_Click;
            outputMainGroupBox.Controls.Add(addOutputRoutingButton);
            
            outputRoutingPanel = new Panel
            {
                Location = new Point(10, 60),
                Size = new Size(780, 130),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(60, 60, 60)
            };
            outputMainGroupBox.Controls.Add(outputRoutingPanel);
            this.Controls.Add(outputMainGroupBox);
            
            CreateEffectsControls();
            
            // Add initial routing instances
            AddInputRoutingInstance();
            AddOutputRoutingInstance();
            
            // Initialize system tray
            InitializeSystemTray();
            
            this.ResumeLayout();
        }
        
        private void CreateEffectsControls()
        {
            // Effects intensity control
            var intensityGroupBox = new GroupBox
            {
                Text = "Effects Intensity",
                Location = new Point(20, 460),
                Size = new Size(800, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            intensityTrackBar = new TrackBar
            {
                Location = new Point(20, 25),
                Size = new Size(650, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickFrequency = 10
            };
            intensityTrackBar.ValueChanged += IntensityTrackBar_ValueChanged;
            intensityGroupBox.Controls.Add(intensityTrackBar);
            
            intensityLabel = new Label
            {
                Text = "50%",
                Location = new Point(680, 35),
                Size = new Size(40, 23),
                ForeColor = Color.White
            };
            intensityGroupBox.Controls.Add(intensityLabel);
            this.Controls.Add(intensityGroupBox);
            
            // Effects panel
            var effectsGroupBox = new GroupBox
            {
                Text = "Individual Effects",
                Location = new Point(20, 540),
                Size = new Size(800, 150),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            effectsPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(780, 115),
                AutoScroll = true
            };
            
            CreateEffectCheckboxes();
            effectsGroupBox.Controls.Add(effectsPanel);
            this.Controls.Add(effectsGroupBox);
            
            // Effect combinations panel
            var combinationsGroupBox = new GroupBox
            {
                Text = "Effect Combinations",
                Location = new Point(20, 710),
                Size = new Size(800, 180),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            
            combinationsPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(780, 145),
                AutoScroll = true
            };
            
            CreateCombinationButtons();
            combinationsGroupBox.Controls.Add(combinationsPanel);
            this.Controls.Add(combinationsGroupBox);
        }
        
        private void InitializeSystemTray()
        {
            // Create context menu for system tray
            trayContextMenu = new ContextMenuStrip();
            
            var showMenuItem = new ToolStripMenuItem("Show Audio Effects Studio");
            showMenuItem.Click += (s, e) => {
                Show();
                WindowState = FormWindowState.Normal;
                BringToFront();
            };
            trayContextMenu.Items.Add(showMenuItem);
            
            var hideMenuItem = new ToolStripMenuItem("Hide to System Tray");
            hideMenuItem.Click += (s, e) => {
                Hide();
            };
            trayContextMenu.Items.Add(hideMenuItem);
            
            trayContextMenu.Items.Add(new ToolStripSeparator());
            
            var stopAllMenuItem = new ToolStripMenuItem("Stop All Routing");
            stopAllMenuItem.Click += (s, e) => {
                StopAllRouting();
            };
            trayContextMenu.Items.Add(stopAllMenuItem);
            
            trayContextMenu.Items.Add(new ToolStripSeparator());
            
            var exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => {
                notifyIcon.Visible = false;
                Application.Exit();
            };
            trayContextMenu.Items.Add(exitMenuItem);
            
            // Create system tray icon
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = CreateTrayIcon();
            notifyIcon.Text = "Audio Effects Studio - Multi-Device Routing";
            notifyIcon.ContextMenuStrip = trayContextMenu;
            notifyIcon.Visible = true;
            
            // Double-click to show/hide
            notifyIcon.DoubleClick += (s, e) => {
                if (Visible)
                {
                    Hide();
                }
                else
                {
                    Show();
                    WindowState = FormWindowState.Normal;
                    BringToFront();
                }
            };
        }
        
        private Icon CreateTrayIcon()
        {
            // Create a simple audio icon using drawing
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Draw speaker/audio icon
                using (var brush = new SolidBrush(Color.Black))
                {
                    // Speaker body
                    g.FillRectangle(brush, 2, 6, 4, 4);
                    // Speaker cone
                    g.FillPolygon(brush, new Point[] {
                        new Point(6, 5),
                        new Point(6, 11),
                        new Point(10, 9),
                        new Point(10, 7)
                    });
                    // Sound waves
                    using (var pen = new Pen(Color.Black, 1))
                    {
                        g.DrawArc(pen, 11, 4, 4, 8, -45, 90);
                        g.DrawArc(pen, 12, 3, 6, 10, -45, 90);
                    }
                }
            }
            
            return Icon.FromHandle(bitmap.GetHicon());
        }
        
        private void StopAllRouting()
        {
            // Stop all input routing instances
            foreach (var instance in inputRoutingInstances)
            {
                StopInputRouting(instance);
            }
            
            // Stop all output routing instances
            foreach (var instance in outputRoutingInstances)
            {
                StopOutputRouting(instance);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void CreateEffectCheckboxes()
        {
            int x = 10, y = 10;
            const int checkboxWidth = 120;
            const int checkboxHeight = 25;
            const int spacing = 10;
            
            foreach (string effect in availableEffects)
            {
                var checkBox = new CheckBox
                {
                    Text = GetEffectDisplayName(effect),
                    Location = new Point(x, y),
                    Size = new Size(checkboxWidth, checkboxHeight),
                    ForeColor = Color.White,
                    Tag = effect
                };
                checkBox.CheckedChanged += EffectCheckBox_CheckedChanged;
                
                effectCheckboxes[effect] = checkBox;
                effectsPanel.Controls.Add(checkBox);
                
                x += checkboxWidth + spacing;
                if (x + checkboxWidth > effectsPanel.Width)
                {
                    x = 10;
                    y += checkboxHeight + spacing;
                }
            }
        }
        
        private void CreateCombinationButtons()
        {
            var combinations = new Dictionary<string, string[]>
            {
                ["Karaoke Classic"] = new[] { "VocalRemoval", "Reverb" },
                ["Concert Hall"] = new[] { "Reverb", "Echo" },
                ["Rock Guitar"] = new[] { "Distortion", "Echo" },
                ["Vintage Vocal"] = new[] { "Chorus", "Reverb" },
                ["Space Echo"] = new[] { "Echo", "Flanger", "Reverb" },
                ["Dream Vocal"] = new[] { "Chorus", "Reverb", "PitchShift" },
                ["Instrumental + Reverb"] = new[] { "VocalRemoval", "Reverb", "Chorus" },
                ["Full Studio"] = new[] { "Echo", "Reverb", "Chorus", "Flanger" }
            };
            
            int x = 10, y = 10;
            const int buttonWidth = 140;
            const int buttonHeight = 35;
            const int spacingX = 15;
            const int spacingY = 10;
            
            foreach (var combination in combinations)
            {
                var button = new Button
                {
                    Text = combination.Key,
                    Location = new Point(x, y),
                    Size = new Size(buttonWidth, buttonHeight),
                    BackColor = Color.FromArgb(76, 86, 106),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Tag = combination.Value
                };
                button.Click += CombinationButton_Click;
                
                combinationsPanel.Controls.Add(button);
                
                x += buttonWidth + spacingX;
                if (x + buttonWidth > combinationsPanel.Width)
                {
                    x = 10;
                    y += buttonHeight + spacingY;
                }
            }
            
            // Clear all button
            var clearButton = new Button
            {
                Text = "Clear All",
                Location = new Point(x, y),
                Size = new Size(buttonWidth, buttonHeight),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            clearButton.Click += ClearAllButton_Click;
            combinationsPanel.Controls.Add(clearButton);
        }
        
        private string GetEffectDisplayName(string effectName)
        {
            return effectName switch
            {
                "PitchShift" => "Pitch Shift",
                "VocalRemoval" => "Vocal Removal",
                _ => effectName
            };
        }
        
        private void AddInputRoutingInstance()
        {
            var instance = new InputRoutingInstance
            {
                Id = nextInputId++,
                Panel = new Panel
                {
                    Size = new Size(750, 40),
                    BackColor = Color.FromArgb(80, 80, 80),
                    BorderStyle = BorderStyle.FixedSingle
                }
            };
            
            // Position this instance
            int yPosition = inputRoutingInstances.Count * 45 + 5;
            instance.Panel.Location = new Point(5, yPosition);
            
            // Device label
            var deviceLabel = new Label
            {
                Text = $"Input {instance.Id}:",
                Location = new Point(10, 10),
                Size = new Size(60, 20),
                ForeColor = Color.White
            };
            instance.Panel.Controls.Add(deviceLabel);
            
            // Device ComboBox
            instance.DeviceComboBox = new ComboBox
            {
                Location = new Point(80, 8),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            LoadInputDevicesIntoComboBox(instance.DeviceComboBox);
            instance.Panel.Controls.Add(instance.DeviceComboBox);
            
            // Start Button
            instance.StartButton = new Button
            {
                Text = "Start",
                Location = new Point(290, 5),
                Size = new Size(60, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            instance.StartButton.Click += (s, e) => StartInputRouting(instance);
            instance.Panel.Controls.Add(instance.StartButton);
            
            // Stop Button
            instance.StopButton = new Button
            {
                Text = "Stop",
                Location = new Point(360, 5),
                Size = new Size(60, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            instance.StopButton.Click += (s, e) => StopInputRouting(instance);
            instance.Panel.Controls.Add(instance.StopButton);
            
            // Remove Button
            instance.RemoveButton = new Button
            {
                Text = "✖",
                Location = new Point(430, 5),
                Size = new Size(30, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            instance.RemoveButton.Click += (s, e) => RemoveInputRouting(instance);
            instance.Panel.Controls.Add(instance.RemoveButton);
            
            inputRoutingInstances.Add(instance);
            inputRoutingPanel.Controls.Add(instance.Panel);
        }
        
        private void AddOutputRoutingInstance()
        {
            var instance = new OutputRoutingInstance
            {
                Id = nextOutputId++,
                Panel = new Panel
                {
                    Size = new Size(750, 40),
                    BackColor = Color.FromArgb(80, 80, 80),
                    BorderStyle = BorderStyle.FixedSingle
                }
            };
            
            // Position this instance
            int yPosition = outputRoutingInstances.Count * 45 + 5;
            instance.Panel.Location = new Point(5, yPosition);
            
            // Device label
            var deviceLabel = new Label
            {
                Text = $"Output {instance.Id}:",
                Location = new Point(10, 10),
                Size = new Size(70, 20),
                ForeColor = Color.White
            };
            instance.Panel.Controls.Add(deviceLabel);
            
            // Device ComboBox
            instance.DeviceComboBox = new ComboBox
            {
                Location = new Point(90, 8),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            LoadOutputDevicesIntoComboBox(instance.DeviceComboBox);
            instance.Panel.Controls.Add(instance.DeviceComboBox);
            
            // Start Button
            instance.StartButton = new Button
            {
                Text = "Start",
                Location = new Point(300, 5),
                Size = new Size(60, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            instance.StartButton.Click += (s, e) => StartOutputRouting(instance);
            instance.Panel.Controls.Add(instance.StartButton);
            
            // Stop Button
            instance.StopButton = new Button
            {
                Text = "Stop",
                Location = new Point(370, 5),
                Size = new Size(60, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            instance.StopButton.Click += (s, e) => StopOutputRouting(instance);
            instance.Panel.Controls.Add(instance.StopButton);
            
            // Remove Button
            instance.RemoveButton = new Button
            {
                Text = "✖",
                Location = new Point(440, 5),
                Size = new Size(30, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            instance.RemoveButton.Click += (s, e) => RemoveOutputRouting(instance);
            instance.Panel.Controls.Add(instance.RemoveButton);
            
            outputRoutingInstances.Add(instance);
            outputRoutingPanel.Controls.Add(instance.Panel);
        }
        
        private void LoadInputDevicesIntoComboBox(ComboBox comboBox)
        {
            comboBox.Items.Clear();
            comboBox.Items.Add("System Default Input");
            
            try
            {
                // Use WASAPI for better device enumeration including Bluetooth devices
                var deviceEnumerator = new MMDeviceEnumerator();
                var captureDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                foreach (var device in captureDevices)
                {
                    comboBox.Items.Add($"{device.FriendlyName}");
                }
            }
            catch
            {
                // Fallback to legacy API if WASAPI fails
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var deviceInfo = WaveInEvent.GetCapabilities(i);
                    comboBox.Items.Add($"{deviceInfo.ProductName}");
                }
            }
            
            comboBox.SelectedIndex = 0;
        }
        
        private void LoadOutputDevicesIntoComboBox(ComboBox comboBox)
        {
            comboBox.Items.Clear();
            comboBox.Items.Add("System Default Output");
            
            try
            {
                // Use WASAPI for better device enumeration including Bluetooth devices
                var deviceEnumerator = new MMDeviceEnumerator();
                var renderDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                
                foreach (var device in renderDevices)
                {
                    comboBox.Items.Add($"{device.FriendlyName}");
                }
            }
            catch
            {
                // Fallback to legacy API if WASAPI fails
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var deviceInfo = WaveOut.GetCapabilities(i);
                    comboBox.Items.Add($"{deviceInfo.ProductName}");
                }
            }
            
            comboBox.SelectedIndex = 0;
        }
        
        private void LoadAudioDevices()
        {
            // Refresh all existing ComboBoxes
            foreach (var instance in inputRoutingInstances)
            {
                LoadInputDevicesIntoComboBox(instance.DeviceComboBox);
            }
            foreach (var instance in outputRoutingInstances)
            {
                LoadOutputDevicesIntoComboBox(instance.DeviceComboBox);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void AddInputRoutingButton_Click(object sender, EventArgs e)
        {
            AddInputRoutingInstance();
        }
        
        private void AddOutputRoutingButton_Click(object sender, EventArgs e)
        {
            AddOutputRoutingInstance();
        }
        
        private void RefreshDevicesButton_Click(object sender, EventArgs e)
        {
            LoadAudioDevices();
            MessageBox.Show("Audio devices refreshed successfully!", "Devices Refreshed", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Minimize to system tray when minimized
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.ShowBalloonTip(2000, "Audio Effects Studio", 
                    "Application minimized to system tray. Double-click the tray icon to restore.", 
                    ToolTipIcon.Info);
            }
        }
        
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Minimize to tray instead of closing when user clicks X
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                notifyIcon.ShowBalloonTip(2000, "Audio Effects Studio", 
                    "Application minimized to system tray. Right-click the tray icon to exit.", 
                    ToolTipIcon.Info);
            }
        }
        
        private void StartInputRouting(InputRoutingInstance instance)
        {
            if (instance.CancellationTokenSource != null)
                return; // Already running
                
            int selectedIndex = instance.DeviceComboBox.SelectedIndex;
            if (selectedIndex < 0) return;
            
            instance.CancellationTokenSource = new CancellationTokenSource();
            
            instance.AudioThread = new Thread(() =>
            {
                try
                {
                    if (selectedIndex == 0)
                    {
                        // System Default Input to System Default Output
                        AudioEngine.StartSystemInputToSystemOutputRouting(instance.CancellationTokenSource.Token);
                    }
                    else
                    {
                        // Selected Input Device to System Default Output
                        AudioEngine.StartInputDeviceToSystemOutputRouting(selectedIndex - 1, instance.CancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)(() =>
                    {
                        string errorMessage = ex.Message;
                        
                        // Provide user-friendly messages for common Bluetooth issues
                        if (ex.Message.Contains("0x8889000F") || ex.Message.Contains("disconnected"))
                        {
                            errorMessage = $"Bluetooth device for Input {instance.Id} was disconnected.\n\nPlease:\n1. Reconnect your Bluetooth device\n2. Click 'Refresh Devices' button\n3. Try starting the routing again";
                        }
                        else if (ex.Message.Contains("0x88890008") || ex.Message.Contains("in use"))
                        {
                            errorMessage = $"Audio device for Input {instance.Id} is being used by another application.\n\nPlease close other audio applications and try again.";
                        }
                        
                        MessageBox.Show(errorMessage, "Audio Routing Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        StopInputRouting(instance);
                    }));
                }
            })
            { IsBackground = true };
            
            instance.AudioThread.Start();
            instance.StartButton.Enabled = false;
            instance.StopButton.Enabled = true;
            instance.DeviceComboBox.Enabled = false;
        }
        
        private void StopInputRouting(InputRoutingInstance instance)
        {
            instance.CancellationTokenSource?.Cancel();
            instance.AudioThread?.Join(2000);
            instance.CancellationTokenSource?.Dispose();
            instance.CancellationTokenSource = null;
            instance.AudioThread = null;
            
            instance.StartButton.Enabled = true;
            instance.StopButton.Enabled = false;
            instance.DeviceComboBox.Enabled = true;
        }
        
        private void StartOutputRouting(OutputRoutingInstance instance)
        {
            if (instance.CancellationTokenSource != null)
                return; // Already running
                
            int selectedIndex = instance.DeviceComboBox.SelectedIndex;
            if (selectedIndex < 0) return;
            
            instance.CancellationTokenSource = new CancellationTokenSource();
            
            instance.AudioThread = new Thread(() =>
            {
                try
                {
                    if (selectedIndex == 0)
                    {
                        // System Default Input to System Default Output
                        AudioEngine.StartSystemInputToSystemOutputRouting(instance.CancellationTokenSource.Token);
                    }
                    else
                    {
                        // System Default Input to Selected Output Device
                        AudioEngine.StartSystemInputToOutputDeviceRouting(selectedIndex - 1, instance.CancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)(() =>
                    {
                        string errorMessage = ex.Message;
                        
                        // Provide user-friendly messages for common Bluetooth issues
                        if (ex.Message.Contains("0x8889000F") || ex.Message.Contains("disconnected"))
                        {
                            errorMessage = $"Bluetooth device for Output {instance.Id} was disconnected.\n\nPlease:\n1. Reconnect your Bluetooth device\n2. Click 'Refresh Devices' button\n3. Try starting the routing again";
                        }
                        else if (ex.Message.Contains("0x88890008") || ex.Message.Contains("in use"))
                        {
                            errorMessage = $"Audio device for Output {instance.Id} is being used by another application.\n\nPlease close other audio applications and try again.";
                        }
                        
                        MessageBox.Show(errorMessage, "Audio Routing Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        StopOutputRouting(instance);
                    }));
                }
            })
            { IsBackground = true };
            
            instance.AudioThread.Start();
            instance.StartButton.Enabled = false;
            instance.StopButton.Enabled = true;
            instance.DeviceComboBox.Enabled = false;
        }
        
        private void StopOutputRouting(OutputRoutingInstance instance)
        {
            instance.CancellationTokenSource?.Cancel();
            instance.AudioThread?.Join(2000);
            instance.CancellationTokenSource?.Dispose();
            instance.CancellationTokenSource = null;
            instance.AudioThread = null;
            
            instance.StartButton.Enabled = true;
            instance.StopButton.Enabled = false;
            instance.DeviceComboBox.Enabled = true;
        }
        
        private void RemoveInputRouting(InputRoutingInstance instance)
        {
            // Stop routing if active
            StopInputRouting(instance);
            
            // Remove from panel and list
            inputRoutingPanel.Controls.Remove(instance.Panel);
            inputRoutingInstances.Remove(instance);
            
            // Reposition remaining instances
            for (int i = 0; i < inputRoutingInstances.Count; i++)
            {
                inputRoutingInstances[i].Panel.Location = new Point(5, i * 45 + 5);
            }
        }
        
        private void RemoveOutputRouting(OutputRoutingInstance instance)
        {
            // Stop routing if active
            StopOutputRouting(instance);
            
            // Remove from panel and list
            outputRoutingPanel.Controls.Remove(instance.Panel);
            outputRoutingInstances.Remove(instance);
            
            // Reposition remaining instances
            for (int i = 0; i < outputRoutingInstances.Count; i++)
            {
                outputRoutingInstances[i].Panel.Location = new Point(5, i * 45 + 5);
            }
        }
        
        private void IntensityTrackBar_ValueChanged(object sender, EventArgs e)
        {
            float intensity = intensityTrackBar.Value / 100.0f;
            AudioEngine.EffectIntensity = intensity;
            intensityLabel.Text = $"{intensityTrackBar.Value}%";
        }
        
        private void EffectCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            string effectName = checkBox.Tag.ToString();
            
            if (checkBox.Checked)
            {
                AudioEngine.ActiveEffects.Add(effectName);
                checkBox.BackColor = Color.FromArgb(0, 122, 204);
            }
            else
            {
                AudioEngine.ActiveEffects.Remove(effectName);
                checkBox.BackColor = Color.Transparent;
            }
        }
        
        private void CombinationButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            var effects = button.Tag as string[];
            
            // Clear current effects
            AudioEngine.ActiveEffects.Clear();
            foreach (var checkBox in effectCheckboxes.Values)
            {
                checkBox.Checked = false;
                checkBox.BackColor = Color.Transparent;
            }
            
            // Apply combination
            foreach (string effect in effects)
            {
                AudioEngine.ActiveEffects.Add(effect);
                if (effectCheckboxes.TryGetValue(effect, out var checkBox))
                {
                    checkBox.Checked = true;
                    checkBox.BackColor = Color.FromArgb(0, 122, 204);
                }
            }
            
            // Visual feedback for combination button
            foreach (Control control in combinationsPanel.Controls)
            {
                if (control is Button btn)
                    btn.BackColor = Color.FromArgb(76, 86, 106);
            }
            button.BackColor = Color.FromArgb(0, 122, 204);
        }
        
        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            AudioEngine.ActiveEffects.Clear();
            foreach (var checkBox in effectCheckboxes.Values)
            {
                checkBox.Checked = false;
                checkBox.BackColor = Color.Transparent;
            }
            
            foreach (Control control in combinationsPanel.Controls)
            {
                if (control is Button btn)
                    btn.BackColor = Color.FromArgb(76, 86, 106);
            }
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up system tray icon
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            
            // Stop all input routing instances
            foreach (var instance in inputRoutingInstances)
            {
                StopInputRouting(instance);
            }
            
            // Stop all output routing instances
            foreach (var instance in outputRoutingInstances)
            {
                StopOutputRouting(instance);
            }
            
            base.OnFormClosing(e);
        }
        
        #endregion
    }
}
