using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.CoreAudioApi;

public class AudioLoopbackForm : Form
{
    private Button btnMicStart;
    private Button btnSpeakerStart;
    private Button btnStopAll;
    private Button btnDJEffects;
    private Panel pnlDJEffects;
    private Button btnEcho;
    private Button btnReverb;
    private Button btnDistortion;
    private Button btnChorus;
    private Button btnFlanger;
    private Button btnPitchShift;
    private Button btnNoEffect;
    private Button btnVocalRemover;
    private Button btnCombo1; // Instrumental + Reverb
    private Button btnCombo2; // Instrumental + Echo  
    private Button btnCombo3; // Instrumental + Chorus
    private TrackBar tbEffectIntensity;
    private Label lblEffectIntensity;
    private CheckBox chkDarkTheme;
    private CheckBox chkRunStartup;
    private ComboBox cmbMic;
    private ComboBox cmbSpeaker;
    private Label lblMic;
    private Label lblSpeaker;
    private Thread? micThread;
    private Thread? dupThread;
    private CancellationTokenSource? micCts;
    private CancellationTokenSource? dupCts;
    private bool micRunning = false;
    private bool dupRunning = false;
    private bool djEffectsPanelVisible = false;
    private string currentEffect = "None";
    private float effectIntensity = 0.5f;
    private HashSet<string> activeEffects = new HashSet<string>();
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private bool allowExit = false;
    // Fade animation
    private System.Windows.Forms.Timer fadeTimer = new System.Windows.Forms.Timer();
    private bool fadeIn = false;
    private bool fadeOut = false;
    private double fadeStep = 0.12; // opacity step per tick

    public AudioLoopbackForm()
    {
        // Form properties
        this.Text = "Audio Device Router";
        this.Width = 450;
        this.Height = 250;
        this.FormBorderStyle = FormBorderStyle.None; // borderless, non-movable
        this.DoubleBuffered = true;
    this.ShowInTaskbar = false; // behave like a tray flyout
        this.StartPosition = FormStartPosition.Manual; // we'll position it near tray
    this.BackColor = Color.FromArgb(245, 246, 248); // muted light gray (not bright white)
        this.Padding = new Padding(12);
    this.TopMost = true; // stay on top (no toggle)

        // Tray icon and menu
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Restore", null, (s, e) => { BeginFadeInAndShowNearTray(); });
        trayMenu.Items.Add("Exit", null, (s, e) => { allowExit = true; trayIcon.Visible = false; Application.Exit(); });
        trayIcon = new NotifyIcon()
        {
            Text = "Audio Routing",
            Icon = CreateTrayIcon(),
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (s, e) => { BeginFadeInAndShowNearTray(); };
        trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                if (this.Visible && this.WindowState != FormWindowState.Minimized)
                    BeginFadeOutAndHide();
                else
                    BeginFadeInAndShowNearTray();
            }
        };

        // Form events
        this.Resize += AudioLoopbackForm_Resize;
        this.FormClosing += AudioLoopbackForm_FormClosing;
        this.Deactivate += (s, e) => { if (this.Visible) BeginFadeOutAndHide(); }; // hide when clicking away
    this.Load += (s, e) => { ApplyRoundedCorners(0); ApplyTheme(false); BeginFadeInAndShowNearTray(); };

        // Fade timer setup
        fadeTimer.Interval = 20; // ~50fps
        fadeTimer.Tick += (s, e) =>
        {
            if (fadeIn)
            {
                this.Opacity = Math.Min(1.0, this.Opacity + fadeStep);
                if (this.Opacity >= 0.999)
                {
                    fadeIn = false;
                    fadeTimer.Stop();
                    this.Opacity = 1.0;
                }
            }
            else if (fadeOut)
            {
                this.Opacity = Math.Max(0.0, this.Opacity - fadeStep);
                if (this.Opacity <= 0.01)
                {
                    fadeOut = false;
                    fadeTimer.Stop();
                    this.Opacity = 0.0;
                    this.Hide();
                }
            }
            else
            {
                fadeTimer.Stop();
            }
        };

        // Controls
    lblMic = new Label { Text = "Microphone:", Left = 16, Top = 22, Width = 90 };
    cmbMic = new ComboBox { Left = 110, Top = 18, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
    btnMicStart = new Button { Text = "Enable", Left = 370, Top = 18, Width = 60, Height = 28 };
    lblSpeaker = new Label { Text = "Speaker:", Left = 16, Top = 62, Width = 90 };
    cmbSpeaker = new ComboBox { Left = 110, Top = 58, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
    btnSpeakerStart = new Button { Text = "Enable", Left = 370, Top = 58, Width = 60, Height = 28 };
    btnStopAll = new Button { Text = "Disable All", Left = 16, Top = 100, Width = 100, Height = 28 };
    var btnRefreshDevices = new Button { Text = "Refresh Devices", Left = 130, Top = 100, Width = 120, Height = 28 };
    btnDJEffects = new Button { Text = "Effects", Left = 260, Top = 100, Width = 80, Height = 28 };
    chkRunStartup = new CheckBox { Text = "Run at startup", Left = 16, Top = 140, Width = 120, Height = 24, Checked = IsStartupEnabled() };
    chkDarkTheme = new CheckBox { Text = "Dark Theme", Left = 150, Top = 140, Width = 100, Height = 24, Checked = false, Enabled = true, Visible = true };

    // Create DJ Effects Panel (initially hidden)
    pnlDJEffects = new Panel { Left = 16, Top = 170, Width = 400, Height = 160, Visible = false, BorderStyle = BorderStyle.FixedSingle };
    pnlDJEffects.BackColor = Color.FromArgb(220, 220, 220);
    
    // DJ Effect buttons
    btnEcho = new Button { Text = "Echo", Left = 10, Top = 10, Width = 60, Height = 25, Parent = pnlDJEffects };
    btnReverb = new Button { Text = "Reverb", Left = 80, Top = 10, Width = 60, Height = 25, Parent = pnlDJEffects };
    btnDistortion = new Button { Text = "Distortion", Left = 150, Top = 10, Width = 70, Height = 25, Parent = pnlDJEffects };
    btnChorus = new Button { Text = "Chorus", Left = 10, Top = 45, Width = 60, Height = 25, Parent = pnlDJEffects };
    btnFlanger = new Button { Text = "Flanger", Left = 80, Top = 45, Width = 60, Height = 25, Parent = pnlDJEffects };
    btnPitchShift = new Button { Text = "Pitch Shift", Left = 150, Top = 45, Width = 70, Height = 25, Parent = pnlDJEffects };
    btnNoEffect = new Button { Text = "None", Left = 10, Top = 80, Width = 60, Height = 25, Parent = pnlDJEffects };
    btnVocalRemover = new Button { Text = "Instrumental", Left = 80, Top = 80, Width = 80, Height = 25, Parent = pnlDJEffects };
    
    // Combination buttons
    btnCombo1 = new Button { Text = "Karaoke Reverb", Left = 10, Top = 115, Width = 100, Height = 25, Parent = pnlDJEffects };
    btnCombo2 = new Button { Text = "Karaoke Echo", Left = 120, Top = 115, Width = 90, Height = 25, Parent = pnlDJEffects };
    btnCombo3 = new Button { Text = "Karaoke Chorus", Left = 220, Top = 115, Width = 95, Height = 25, Parent = pnlDJEffects };
    
    // Effect intensity control
    lblEffectIntensity = new Label { Text = "Intensity:", Left = 240, Top = 15, Width = 60, Height = 20, Parent = pnlDJEffects };
    tbEffectIntensity = new TrackBar { Left = 240, Top = 35, Width = 120, Height = 45, Parent = pnlDJEffects };
    tbEffectIntensity.Minimum = 0;
    tbEffectIntensity.Maximum = 100;
    tbEffectIntensity.Value = 50;
    tbEffectIntensity.TickFrequency = 25;
    // Set label colors to black for visibility
    lblMic.ForeColor = Color.Black;
    lblSpeaker.ForeColor = Color.Black;

    btnMicStart.Click += BtnMicLoop_Click;
    btnSpeakerStart.Click += BtnSpeakerLoopback_Click;
    btnStopAll.Click += BtnStopAll_Click;
    btnRefreshDevices.Click += BtnRefreshDevices_Click;
    btnDJEffects.Click += BtnDJEffects_Click;
    
    // DJ Effects event handlers
    btnEcho.Click += (s, e) => ApplyDJEffect("Echo");
    btnReverb.Click += (s, e) => ApplyDJEffect("Reverb");
    btnDistortion.Click += (s, e) => ApplyDJEffect("Distortion");
    btnChorus.Click += (s, e) => ApplyDJEffect("Chorus");
    btnFlanger.Click += (s, e) => ApplyDJEffect("Flanger");
    btnPitchShift.Click += (s, e) => ApplyDJEffect("Pitch Shift");
    btnNoEffect.Click += (s, e) => ApplyDJEffect("None");
    btnVocalRemover.Click += (s, e) => ApplyDJEffect("Vocal Remover");
    
    // Combination button handlers
    btnCombo1.Click += (s, e) => ApplyEffectCombination("Vocal Remover", "Reverb");
    btnCombo2.Click += (s, e) => ApplyEffectCombination("Vocal Remover", "Echo");
    btnCombo3.Click += (s, e) => ApplyEffectCombination("Vocal Remover", "Chorus");
    tbEffectIntensity.ValueChanged += (s, e) => { 
        effectIntensity = tbEffectIntensity.Value / 100.0f; 
        MicLoopback.EffectIntensity = effectIntensity;
    };
    
    chkRunStartup.CheckedChanged += (s, e) => { SetRunAtStartup(chkRunStartup.Checked); };
    this.Controls.Add(cmbMic);
    this.Controls.Add(lblMic);
    this.Controls.Add(btnMicStart);
    this.Controls.Add(lblSpeaker);
    this.Controls.Add(cmbSpeaker);
    this.Controls.Add(btnSpeakerStart);
    this.Controls.Add(btnStopAll);
    this.Controls.Add(btnRefreshDevices);
    this.Controls.Add(btnDJEffects);
    this.Controls.Add(pnlDJEffects);
    this.Controls.Add(chkRunStartup);
    this.Controls.Add(chkDarkTheme);
    chkDarkTheme.BringToFront();
    chkDarkTheme.CheckedChanged += (s, e) => { ApplyTheme(chkDarkTheme.Checked); };
    this.Controls.Add(chkDarkTheme);
    ApplyTheme(true);

        // Populate device lists
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var deviceInfo = WaveInEvent.GetCapabilities(i);
            cmbMic.Items.Add($"{i}: {deviceInfo.ProductName}");
        }
        if (cmbMic.Items.Count > 0) cmbMic.SelectedIndex = 0;

        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (int i = 0; i < devices.Count; i++)
        {
            cmbSpeaker.Items.Add($"{i}: {devices[i].FriendlyName}");
        }
        if (cmbSpeaker.Items.Count > 0) cmbSpeaker.SelectedIndex = 0;
    } // End of constructor

    private void RefreshDeviceLists()
    {
        // Save current selections
        string selectedMic = cmbMic.SelectedItem?.ToString() ?? "";
        string selectedSpeaker = cmbSpeaker.SelectedItem?.ToString() ?? "";

        // Clear and repopulate microphone list
        cmbMic.Items.Clear();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var deviceInfo = WaveInEvent.GetCapabilities(i);
            string itemText = $"{i}: {deviceInfo.ProductName}";
            cmbMic.Items.Add(itemText);
            // Restore selection if device still exists
            if (itemText == selectedMic)
                cmbMic.SelectedIndex = i;
        }
        if (cmbMic.SelectedIndex < 0 && cmbMic.Items.Count > 0) cmbMic.SelectedIndex = 0;

        // Clear and repopulate speaker list
        cmbSpeaker.Items.Clear();
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (int i = 0; i < devices.Count; i++)
        {
            string itemText = $"{i}: {devices[i].FriendlyName}";
            cmbSpeaker.Items.Add(itemText);
            // Restore selection if device still exists
            if (itemText == selectedSpeaker)
                cmbSpeaker.SelectedIndex = i;
        }
        if (cmbSpeaker.SelectedIndex < 0 && cmbSpeaker.Items.Count > 0) cmbSpeaker.SelectedIndex = 0;
    }

    private void BtnRefreshDevices_Click(object? sender, EventArgs e)
    {
        try
        {
            RefreshDeviceLists();
        }
        catch
        {
            // Silently handle errors - could add logging here if needed
        }
    }

    // Theming methods
    private void ApplyTheme(bool dark)
    {
    if (dark)
    {
        var text = Color.FromArgb(220, 220, 220);
        var subtle = Color.FromArgb(180, 180, 180);
        var fieldBg = Color.FromArgb(40, 40, 45);
        var back = Color.FromArgb(28, 28, 34);
        var buttonBg = Color.FromArgb(50, 50, 60);
        var buttonText = Color.FromArgb(220, 220, 220);
        StyleControls(text, subtle, fieldBg, back, buttonBg, buttonText);
    }
    else
    {
        var text = Color.Black;
        var subtle = Color.DarkGray;
        var fieldBg = Color.White;
        var back = Color.FromArgb(245, 246, 248);
        var buttonBg = Color.LightGray;
        var buttonText = Color.Black;
        StyleControls(text, subtle, fieldBg, back, buttonBg, buttonText);
    }
    }

    private void StyleControls(Color text, Color subtle, Color fieldBg, Color back, Color buttonBg, Color buttonText)
    {
        this.Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, 9f, FontStyle.Regular);
        this.BackColor = back;
        foreach (Control c in this.Controls)
        {
            if (c is Label l)
            {
                l.ForeColor = subtle;
                l.BackColor = back;
            }
            else if (c is ComboBox cb)
            {
                cb.BackColor = fieldBg;
                cb.ForeColor = text;
                cb.FlatStyle = FlatStyle.Standard;
            }
            else if (c is Button b)
            {
                b.FlatStyle = FlatStyle.System;
                b.BackColor = buttonBg;
                b.ForeColor = buttonText;
            }
            else if (c is CheckBox ch)
            {
                ch.ForeColor = text;
                ch.BackColor = back;
            }
        }
    }

    // Removed duplicate StyleControls method outside class

    private void ApplyRoundedCorners(int radius)
    {
        try
        {
            if (radius <= 0 || this.Width <= 2 || this.Height <= 2)
            {
                this.Region = null;
                return;
            }
            int r = Math.Min(radius * 2, Math.Min(this.Width, this.Height) - 2);
            if (r <= 0) { this.Region = null; return; }
            using var path = new GraphicsPath();
            var rect = new Rectangle(0, 0, this.Width, this.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r - 1, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r - 1, rect.Bottom - r - 1, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r - 1, r, r, 90, 90);
            path.CloseFigure();
            this.Region = new Region(path);
        }
        catch { this.Region = null; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    using var brush = new SolidBrush(this.BackColor);
    g.FillRectangle(brush, this.ClientRectangle);
    // Draw clear 1px border around the window
    var outer = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
    ControlPaint.DrawBorder(g, outer, Color.FromArgb(210, 213, 218), ButtonBorderStyle.Solid);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
    ApplyRoundedCorners(0);
    }

    private void WireButtonHover(Button b, Color baseColor)
    {
        var hover = Brighten(baseColor, 0.1f);
        var pressed = Darken(baseColor, 0.1f);
        b.MouseEnter += (s, e) => b.BackColor = hover;
        b.MouseLeave += (s, e) => b.BackColor = baseColor;
        b.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) b.BackColor = pressed; };
        b.MouseUp += (s, e) => b.BackColor = b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)) ? hover : baseColor;
    }

    private static Color Brighten(Color c, float amount)
    {
        float a = Math.Clamp(amount, 0f, 1f);
        int r = (int)(c.R + (255 - c.R) * a);
        int g = (int)(c.G + (255 - c.G) * a);
        int b = (int)(c.B + (255 - c.B) * a);
        return Color.FromArgb(c.A, r, g, b);
    }

    private static Color Darken(Color c, float amount)
    {
        float a = Math.Clamp(amount, 0f, 1f);
        int r = (int)(c.R * (1 - a));
        int g = (int)(c.G * (1 - a));
        int b = (int)(c.B * (1 - a));
        return Color.FromArgb(c.A, r, g, b);
    }

    private Color? GetWindowsAccentColor()
    {
        try
        {
            uint color;
            bool opaque;
            if (DwmGetColorizationColor(out color, out opaque) == 0)
            {
                // ARGB
                byte a = (byte)((color >> 24) & 0xFF);
                byte r = (byte)((color >> 16) & 0xFF);
                byte g = (byte)((color >> 8) & 0xFF);
                byte b = (byte)(color & 0xFF);
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return null;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

    // Prevent moving/resizing by swallowing hit-tests
    protected override void WndProc(ref Message m)
    {
    const int WM_NCHITTEST = 0x84;
    const int HTCLIENT = 1;
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTCLIENT;
            return;
        }
        base.WndProc(ref m);
    }

    private void ShowNearTray()
    {
        // Approximate bottom-right of the primary screen working area (like volume flyout)
        var wa = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromControl(this).WorkingArea;
        int margin = 8;
        int x = wa.Right - this.Width - margin;
        int y = wa.Bottom - this.Height - margin;
    this.Location = new Point(Math.Max(wa.Left + margin, x), Math.Max(wa.Top + margin, y));
    this.WindowState = FormWindowState.Normal;
        this.Show();
        this.Activate();
    }

    private void BeginFadeInAndShowNearTray()
    {
        fadeOut = false;
        fadeIn = true;
        this.Opacity = 0.0;
        ShowNearTray();
        fadeTimer.Start();
    }

    private void BeginFadeOutAndHide()
    {
        if (!this.Visible) return;
        fadeIn = false;
        fadeOut = true;
        fadeTimer.Start();
    }

    private Icon CreateTrayIcon()
    {
        // Create a small 16x16 icon with a blue circle and white play triangle
        var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var brush = new SolidBrush(Color.DeepSkyBlue))
            {
                g.FillEllipse(brush, 0, 0, 15, 15);
            }

            var triangle = new[] { new Point(6, 4), new Point(11, 8), new Point(6, 12) };
            using (var white = new SolidBrush(Color.White))
            {
                g.FillPolygon(white, triangle);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            var name = GetStartupValueName();
            var value = key?.GetValue(name) as string;
            if (string.IsNullOrEmpty(value)) return false;
            // Compare path ignoring quotes
            string exe = Application.ExecutablePath;
            string normalized = value.Trim().Trim('"');
            return normalized.StartsWith(exe, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true)
                           ?? Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            var name = GetStartupValueName();
            if (enable)
            {
                string exe = Application.ExecutablePath;
                key.SetValue(name, $"\"{exe}\"");
            }
            else
            {
                if (key.GetValue(name) != null)
                    key.DeleteValue(name, false);
            }
        }
        catch
        {
            // Best-effort; ignore errors (user might not have perms)
            chkRunStartup.CheckedChanged -= (s, e) => { SetRunAtStartup(chkRunStartup.Checked); };
            chkRunStartup.Checked = !enable; // revert UI state
            chkRunStartup.CheckedChanged += (s, e) => { SetRunAtStartup(chkRunStartup.Checked); };
        }
    }

    private static string GetStartupValueName() => "AudioLoopback";

    private void AudioLoopbackForm_Resize(object? sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
            trayIcon.BalloonTipTitle = "Audio Loopback";
            trayIcon.BalloonTipText = "Application minimized to tray.";
            trayIcon.ShowBalloonTip(1000);
        }
    }

    private void AudioLoopbackForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Minimize to tray instead of closing
        if (!allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.WindowState = FormWindowState.Minimized;
        }
    }

    private void BtnStopAll_Click(object? sender, EventArgs e)
    {
        if (micRunning)
        {
            micRunning = false;
            btnMicStart.Text = "Enable";
            try { micCts?.Cancel(); } catch { }
            try { micThread?.Join(500); } catch { }
            micCts?.Dispose();
            micCts = null;
            micThread = null;
        }
        if (dupRunning)
        {
            dupRunning = false;
            btnSpeakerStart.Text = "Enable";
            try { dupCts?.Cancel(); } catch { }
            try { dupThread?.Join(500); } catch { }
            dupCts?.Dispose();
            dupCts = null;
            dupThread = null;
        }
    }

    private void BtnMicLoop_Click(object? sender, EventArgs e)
    {
        if (!micRunning)
        {
            micRunning = true;
            btnMicStart.Text = "Disable";
            int micIndex = cmbMic.SelectedIndex;
            micCts?.Cancel();
            micCts?.Dispose();
            micCts = new CancellationTokenSource();
            micThread = new Thread(() => MicLoopback.LoopMicrophone(micIndex, micCts.Token)) { IsBackground = true };
            micThread.Start();
        }
        else
        {
            micRunning = false;
            btnMicStart.Text = "Enable";
            try { micCts?.Cancel(); } catch { }
            try { micThread?.Join(500); } catch { }
            micCts?.Dispose();
            micCts = null;
            micThread = null;
        }
    }

    private void BtnSpeakerLoopback_Click(object? sender, EventArgs e)
    {
        if (!dupRunning)
        {
            dupRunning = true;
            btnSpeakerStart.Text = "Disable";
            int speakerIndex = cmbSpeaker.SelectedIndex;
            dupCts?.Cancel();
            dupCts?.Dispose();
            dupCts = new CancellationTokenSource();
            dupThread = new Thread(() => MicLoopback.DuplicateAudio(speakerIndex, dupCts.Token)) { IsBackground = true };
            dupThread.Start();
        }
        else
        {
            dupRunning = false;
            btnSpeakerStart.Text = "Enable";
            try { dupCts?.Cancel(); } catch { }
            try { dupThread?.Join(500); } catch { }
            dupCts?.Dispose();
            dupCts = null;
            dupThread = null;
        }
    }

    private void BtnDJEffects_Click(object? sender, EventArgs e)
    {
        djEffectsPanelVisible = !djEffectsPanelVisible;
        pnlDJEffects.Visible = djEffectsPanelVisible;
        btnDJEffects.Text = djEffectsPanelVisible ? "Hide Effects" : "DJ Effects";
        
        // Adjust form height based on panel visibility
        this.Height = djEffectsPanelVisible ? 410 : 250;
    }

    private void ApplyDJEffect(string effectName)
    {
        // Handle "None" effect - clear all effects
        if (effectName == "None")
        {
            activeEffects.Clear();
            ResetEffectButtons();
            btnNoEffect.BackColor = Color.LightBlue;
            MicLoopback.ActiveEffects = activeEffects;
            return;
        }
        
        // Toggle effect on/off
        Button? selectedButton = effectName switch
        {
            "Echo" => btnEcho,
            "Reverb" => btnReverb,
            "Distortion" => btnDistortion,
            "Chorus" => btnChorus,
            "Flanger" => btnFlanger,
            "Pitch Shift" => btnPitchShift,
            "Vocal Remover" => btnVocalRemover,
            _ => null
        };
        
        if (selectedButton != null)
        {
            if (activeEffects.Contains(effectName))
            {
                // Effect is active, turn it off
                activeEffects.Remove(effectName);
                selectedButton.BackColor = SystemColors.Control;
            }
            else
            {
                // Effect is not active, turn it on
                activeEffects.Add(effectName);
                selectedButton.BackColor = Color.LightBlue;
                // Reset "None" button when any effect is activated
                btnNoEffect.BackColor = SystemColors.Control;
            }
            
            // Update the audio processing with current active effects
            MicLoopback.ActiveEffects = activeEffects;
            MicLoopback.EffectIntensity = effectIntensity;
        }
        
        // Effect applied silently - visual feedback through button highlighting
    }

    private void ApplyEffectCombination(string effect1, string effect2)
    {
        // Clear all effects first
        activeEffects.Clear();
        ResetEffectButtons();
        ResetComboButtons();
        
        // Add the two effects
        activeEffects.Add(effect1);
        activeEffects.Add(effect2);
        
        // Highlight the appropriate individual effect buttons
        HighlightEffectButton(effect1);
        HighlightEffectButton(effect2);
        
        // Highlight the combination button
        var comboButton = (effect1 == "Vocal Remover" && effect2 == "Reverb") ? btnCombo1 :
                         (effect1 == "Vocal Remover" && effect2 == "Echo") ? btnCombo2 :
                         (effect1 == "Vocal Remover" && effect2 == "Chorus") ? btnCombo3 : null;
        
        if (comboButton != null)
        {
            comboButton.BackColor = Color.LightGreen; // Different color for combinations
        }
        
        // Update the audio processing
        MicLoopback.ActiveEffects = activeEffects;
        MicLoopback.EffectIntensity = effectIntensity;
    }

    private void HighlightEffectButton(string effectName)
    {
        Button? button = effectName switch
        {
            "Echo" => btnEcho,
            "Reverb" => btnReverb,
            "Distortion" => btnDistortion,
            "Chorus" => btnChorus,
            "Flanger" => btnFlanger,
            "Pitch Shift" => btnPitchShift,
            "Vocal Remover" => btnVocalRemover,
            _ => null
        };
        
        if (button != null)
        {
            button.BackColor = Color.LightBlue;
        }
    }

    private void ResetComboButtons()
    {
        btnCombo1.BackColor = SystemColors.Control;
        btnCombo2.BackColor = SystemColors.Control;
        btnCombo3.BackColor = SystemColors.Control;
    }

    private void ResetEffectButtons()
    {
        btnEcho.BackColor = SystemColors.Control;
        btnReverb.BackColor = SystemColors.Control;
        btnDistortion.BackColor = SystemColors.Control;
        btnChorus.BackColor = SystemColors.Control;
        btnFlanger.BackColor = SystemColors.Control;
        btnPitchShift.BackColor = SystemColors.Control;
        btnNoEffect.BackColor = SystemColors.Control;
        btnVocalRemover.BackColor = SystemColors.Control;
        ResetComboButtons();
    }

    private string GetEffectDescription(string effectName)
    {
        return effectName switch
        {
            "Echo" => "Creates delayed repetitions of the audio signal, perfect for dramatic vocal effects.",
            "Reverb" => "Adds spatial depth and ambience, simulating different room acoustics.",
            "Distortion" => "Adds harmonic saturation and grit to make sounds more aggressive.",
            "Chorus" => "Creates a thick, lush sound by adding slightly delayed and pitch-modulated copies.",
            "Flanger" => "Creates a sweeping, jet-like effect by mixing the signal with a short, varying delay.",
            "Pitch Shift" => "Changes the pitch of the audio without affecting its duration.",
            _ => "Unknown effect."
        };
    }
}

public static class ProgramUI
{
    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new AudioLoopbackForm());
    }
}
