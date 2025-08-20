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
        
        private ComboBox inputSourceComboBox;
        private ComboBox outputDeviceComboBox;
        private Button startInputRoutingButton;
        private Button stopInputRoutingButton;
        private Button startOutputRoutingButton;
        private Button stopOutputRoutingButton;
        private Button refreshButton;
        private Panel effectsPanel;
        private Panel combinationsPanel;
        private TrackBar intensityTrackBar;
        private Label intensityLabel;
        
        private CancellationTokenSource inputCancellationTokenSource;
        private CancellationTokenSource outputCancellationTokenSource;
        private Thread inputAudioThread;
        private Thread outputAudioThread;
        
        private readonly Dictionary<string, CheckBox> effectCheckboxes = new Dictionary<string, CheckBox>();
        private readonly string[] availableEffects = { "Echo", "Reverb", "Distortion", "Chorus", "Flanger", "PitchShift", "VocalRemoval" };
        
        #endregion
        
        #region Constructor
        
        public MainForm()
        {
            InitializeComponent();
            LoadAudioDevices();
        }
        
        #endregion
        
        #region Form Designer Generated Code
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Form settings
            this.Text = "Audio Effects Studio";
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
            
            // Input Source Section
            var inputGroupBox = new GroupBox
            {
                Text = "Input Source Routing (To System Default Output)",
                Location = new Point(20, 20),
                Size = new Size(760, 80),
                ForeColor = Color.White
            };
            
            var inputSourceLabel = new Label
            {
                Text = "Input Source:",
                Location = new Point(10, 30),
                Size = new Size(80, 23),
                ForeColor = Color.White
            };
            inputGroupBox.Controls.Add(inputSourceLabel);
            
            inputSourceComboBox = new ComboBox
            {
                Location = new Point(100, 30),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            inputGroupBox.Controls.Add(inputSourceComboBox);
            
            startInputRoutingButton = new Button
            {
                Text = "Start Input Routing",
                Location = new Point(370, 25),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            startInputRoutingButton.Click += StartInputRoutingButton_Click;
            inputGroupBox.Controls.Add(startInputRoutingButton);
            
            stopInputRoutingButton = new Button
            {
                Text = "Stop Input Routing",
                Location = new Point(500, 25),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            stopInputRoutingButton.Click += StopInputRoutingButton_Click;
            inputGroupBox.Controls.Add(stopInputRoutingButton);
            
            this.Controls.Add(inputGroupBox);
            
            // Output Device Section
            var outputGroupBox = new GroupBox
            {
                Text = "Output Device Routing (From System Default Input)",
                Location = new Point(20, 110),
                Size = new Size(760, 80),
                ForeColor = Color.White
            };
            
            var outputDeviceLabel = new Label
            {
                Text = "Output Device:",
                Location = new Point(10, 30),
                Size = new Size(80, 23),
                ForeColor = Color.White
            };
            outputGroupBox.Controls.Add(outputDeviceLabel);
            
            outputDeviceComboBox = new ComboBox
            {
                Location = new Point(100, 30),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            outputGroupBox.Controls.Add(outputDeviceComboBox);
            
            startOutputRoutingButton = new Button
            {
                Text = "Start Output Routing",
                Location = new Point(370, 25),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            startOutputRoutingButton.Click += StartOutputRoutingButton_Click;
            outputGroupBox.Controls.Add(startOutputRoutingButton);
            
            stopOutputRoutingButton = new Button
            {
                Text = "Stop Output Routing",
                Location = new Point(500, 25),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(191, 97, 106),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            stopOutputRoutingButton.Click += StopOutputRoutingButton_Click;
            outputGroupBox.Controls.Add(stopOutputRoutingButton);
            
            refreshButton = new Button
            {
                Text = "Refresh Devices",
                Location = new Point(630, 30),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(163, 190, 140),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            refreshButton.Click += RefreshButton_Click;
            outputGroupBox.Controls.Add(refreshButton);
            
            this.Controls.Add(outputGroupBox);
            
            // Effects intensity control
            var intensityGroupBox = new GroupBox
            {
                Text = "Effects Intensity",
                Location = new Point(20, 210),
                Size = new Size(760, 60),
                ForeColor = Color.White
            };
            
            intensityTrackBar = new TrackBar
            {
                Location = new Point(20, 25),
                Size = new Size(600, 45),
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
                Location = new Point(630, 35),
                Size = new Size(40, 23),
                ForeColor = Color.White
            };
            intensityGroupBox.Controls.Add(intensityLabel);
            
            this.Controls.Add(intensityGroupBox);
            
            // Effects panel
            var effectsGroupBox = new GroupBox
            {
                Text = "Individual Effects",
                Location = new Point(20, 290),
                Size = new Size(760, 150),
                ForeColor = Color.White
            };
            
            effectsPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(740, 115),
                AutoScroll = true
            };
            
            CreateEffectCheckboxes();
            effectsGroupBox.Controls.Add(effectsPanel);
            this.Controls.Add(effectsGroupBox);
            
            // Effect combinations panel
            var combinationsGroupBox = new GroupBox
            {
                Text = "Effect Combinations",
                Location = new Point(20, 460),
                Size = new Size(760, 180),
                ForeColor = Color.White
            };
            
            combinationsPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(740, 145),
                AutoScroll = true
            };
            
            CreateCombinationButtons();
            combinationsGroupBox.Controls.Add(combinationsPanel);
            this.Controls.Add(combinationsGroupBox);
            
            this.ResumeLayout();
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
        
        private void LoadAudioDevices()
        {
            try
            {
                // Load input sources (system default + microphones, line-in, virtual cables)
                inputSourceComboBox.Items.Clear();
                inputSourceComboBox.Items.Add("0: System Default Input");
                
                // Add microphone and other input devices
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var deviceInfo = WaveInEvent.GetCapabilities(i);
                    inputSourceComboBox.Items.Add($"{i + 1}: {deviceInfo.ProductName}");
                }
                
                // Default to system default input
                inputSourceComboBox.SelectedIndex = 0;
                
                // Load output devices (system default + speakers, headphones, virtual cables)
                outputDeviceComboBox.Items.Clear();
                outputDeviceComboBox.Items.Add("0: System Default Output");
                
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var deviceInfo = WaveOut.GetCapabilities(i);
                    outputDeviceComboBox.Items.Add($"{i + 1}: {deviceInfo.ProductName}");
                }
                
                // Default to system default output
                outputDeviceComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audio devices: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
        
        #endregion
        
        #region Event Handlers
        
        private void StartInputRoutingButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (inputSourceComboBox.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select an input source.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                inputCancellationTokenSource = new CancellationTokenSource();
                
                inputAudioThread = new Thread(() =>
                {
                    try
                    {
                        if (inputSourceComboBox.SelectedIndex == 0)
                        {
                            // Route system default input to system default output with effects
                            AudioEngine.StartSystemInputToSystemOutputRouting(inputCancellationTokenSource.Token);
                        }
                        else
                        {
                            // Route selected input device to system default output with effects
                            var deviceIndex = inputSourceComboBox.SelectedIndex - 1;
                            AudioEngine.StartInputDeviceToSystemOutputRouting(deviceIndex, inputCancellationTokenSource.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            MessageBox.Show($"Input routing error: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            StopInputRouting();
                        });
                    }
                })
                {
                    IsBackground = true
                };
                
                inputAudioThread.Start();
                
                startInputRoutingButton.Enabled = false;
                stopInputRoutingButton.Enabled = true;
                inputSourceComboBox.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting input routing: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void StopInputRoutingButton_Click(object sender, EventArgs e)
        {
            StopInputRouting();
        }
        
        private void StartOutputRoutingButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (outputDeviceComboBox.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select an output device.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                outputCancellationTokenSource = new CancellationTokenSource();
                
                outputAudioThread = new Thread(() =>
                {
                    try
                    {
                        if (outputDeviceComboBox.SelectedIndex == 0)
                        {
                            // Route system default input to system default output with effects
                            AudioEngine.StartSystemInputToSystemOutputRouting(outputCancellationTokenSource.Token);
                        }
                        else
                        {
                            // Route system default input to selected output device with effects
                            var deviceIndex = outputDeviceComboBox.SelectedIndex - 1;
                            AudioEngine.StartSystemInputToOutputDeviceRouting(deviceIndex, outputCancellationTokenSource.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            MessageBox.Show($"Output routing error: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            StopOutputRouting();
                        });
                    }
                })
                {
                    IsBackground = true
                };
                
                outputAudioThread.Start();
                
                startOutputRoutingButton.Enabled = false;
                stopOutputRoutingButton.Enabled = true;
                outputDeviceComboBox.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting output routing: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void StopOutputRoutingButton_Click(object sender, EventArgs e)
        {
            StopOutputRouting();
        }
        
        private void StopInputRouting()
        {
            try
            {
                inputCancellationTokenSource?.Cancel();
                inputAudioThread?.Join(5000);
                
                startInputRoutingButton.Enabled = true;
                stopInputRoutingButton.Enabled = false;
                inputSourceComboBox.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping input routing: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void StopOutputRouting()
        {
            try
            {
                outputCancellationTokenSource?.Cancel();
                outputAudioThread?.Join(5000);
                
                startOutputRoutingButton.Enabled = true;
                stopOutputRoutingButton.Enabled = false;
                outputDeviceComboBox.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping output routing: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadAudioDevices();
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
            StopInputRouting();
            StopOutputRouting();
            base.OnFormClosing(e);
        }
        
        #endregion
    }
}
