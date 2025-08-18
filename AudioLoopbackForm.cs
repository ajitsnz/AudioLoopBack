using System;
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
    private Button btnMicLoop;
    private Button btnAudioDup;
        private Button btnSpeakerLoopback;
    private Button btnStopAll;
    private Button btnExit;
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
        chkDarkTheme = new CheckBox { Text = "Dark", Left = 250, Top = 155, Width = 70, Height = 24, Checked = true, Enabled = false, Visible = false };
        this.Controls.Add(chkDarkTheme);
    {
    // Removed invalid field declaration from constructor
    this.Text = "Audio Device Router";
        this.Width = 400;
        this.Height = 200;
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
            Text = "Audio Loopback & Duplicate",
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
    lblSpeaker = new Label { Text = "Speaker:", Left = 16, Top = 62, Width = 90 };
    cmbSpeaker = new ComboBox { Left = 110, Top = 58, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
    btnMicLoop = new Button { Text = "Start Mic", Left = 16, Top = 105, Width = 150, Height = 28 };
    btnSpeakerLoopback = new Button { Text = "Start Speaker", Left = 174, Top = 105, Width = 160, Height = 28 };
    btnStopAll = new Button { Text = "Stop", Left = 16, Top = 150, Width = 70, Height = 28 };
    chkRunStartup = new CheckBox { Text = "Run at startup", Left = 100, Top = 155, Width = 140, Height = 24, Checked = IsStartupEnabled() };

    chkDarkTheme = new CheckBox { Text = "Dark Theme", Left = 250, Top = 150, Width = 100, Height = 24, Checked = false, Enabled = true, Visible = true };
    // Set label colors to black for visibility
    lblMic.ForeColor = Color.Black;
    lblSpeaker.ForeColor = Color.Black;
    // Align dark theme checkbox with other controls
    chkDarkTheme.Top = btnStopAll.Top;
    chkDarkTheme.Left = btnStopAll.Right + 30; // add more space to avoid overlap

    btnMicLoop.Click += BtnMicLoop_Click;
    btnSpeakerLoopback.Click += BtnSpeakerLoopback_Click;
    btnStopAll.Click += BtnStopAll_Click;
    chkRunStartup.CheckedChanged += (s, e) => { SetRunAtStartup(chkRunStartup.Checked); };
    this.Controls.Add(cmbMic);
    this.Controls.Add(lblMic);
    this.Controls.Add(lblSpeaker);
    this.Controls.Add(cmbSpeaker);
    this.Controls.Add(btnMicLoop);
    this.Controls.Add(btnSpeakerLoopback);
    this.Controls.Add(btnStopAll);
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
            btnMicLoop.Text = "Start Mic";
            try { micCts?.Cancel(); } catch { }
            try { micThread?.Join(500); } catch { }
            micCts?.Dispose();
            micCts = null;
            micThread = null;
        }
        if (dupRunning)
        {
            dupRunning = false;
            btnSpeakerLoopback.Text = "Start Speaker";
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
            btnMicLoop.Text = "Stop Mic";
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
            btnMicLoop.Text = "Start Mic";
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
            btnSpeakerLoopback.Text = "Stop Speaker";
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
            btnSpeakerLoopback.Text = "Start Speaker";
            try { dupCts?.Cancel(); } catch { }
            try { dupThread?.Join(500); } catch { }
            dupCts?.Dispose();
            dupCts = null;
            dupThread = null;
        }
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
