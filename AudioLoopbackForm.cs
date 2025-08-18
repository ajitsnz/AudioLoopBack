using System;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.CoreAudioApi;

public class AudioLoopbackForm : Form
{
    private Button btnMicLoop;
    private Button btnAudioDup;
    private Button btnStopAll;
    private ComboBox cmbMic;
    private ComboBox cmbSpeaker;
    private Label lblMic;
    private Label lblSpeaker;
    private Thread? micThread;
    private Thread? dupThread;
    private bool micRunning = false;
    private bool dupRunning = false;
    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;

    public AudioLoopbackForm()
    {
        this.Text = "Audio Loopback & Duplicate";
        this.Width = 400;
        this.Height = 200;

        // Tray icon and menu
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Restore", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
        trayMenu.Items.Add("Exit", null, (s, e) => { trayIcon.Visible = false; Application.Exit(); });
        trayIcon = new NotifyIcon()
        {
            Text = "Audio Loopback & Duplicate",
            Icon = SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
        this.Resize += AudioLoopbackForm_Resize;
        this.FormClosing += AudioLoopbackForm_FormClosing;

        lblMic = new Label { Text = "Microphone:", Left = 10, Top = 20, Width = 80 };
        cmbMic = new ComboBox { Left = 100, Top = 18, Width = 200 };
        lblSpeaker = new Label { Text = "Speaker:", Left = 10, Top = 60, Width = 80 };
        cmbSpeaker = new ComboBox { Left = 100, Top = 58, Width = 200 };
        btnMicLoop = new Button { Text = "Start Mic Loopback", Left = 10, Top = 100, Width = 120 };
        btnAudioDup = new Button { Text = "Start Audio Duplicate", Left = 140, Top = 100, Width = 120 };
        btnStopAll = new Button { Text = "Stop All", Left = 270, Top = 100, Width = 100 };

        btnMicLoop.Click += BtnMicLoop_Click;
        btnAudioDup.Click += BtnAudioDup_Click;
        btnStopAll.Click += BtnStopAll_Click;

        this.Controls.Add(lblMic);
        this.Controls.Add(cmbMic);
        this.Controls.Add(lblSpeaker);
        this.Controls.Add(cmbSpeaker);
        this.Controls.Add(btnMicLoop);
        this.Controls.Add(btnAudioDup);
        this.Controls.Add(btnStopAll);

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
            cmbSpeaker.Items.Add($"{i}: {devices[i].FriendlyName}");
        if (cmbSpeaker.Items.Count > 0) cmbSpeaker.SelectedIndex = 0;
    }

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
        if (e.CloseReason == CloseReason.UserClosing)
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
            btnMicLoop.Text = "Start Mic Loopback";
            micThread?.Interrupt();
        }
        if (dupRunning)
        {
            dupRunning = false;
            btnAudioDup.Text = "Start Audio Duplicate";
            dupThread?.Interrupt();
        }
    }

    private void BtnMicLoop_Click(object? sender, EventArgs e)
    {
        if (!micRunning)
        {
            micRunning = true;
            btnMicLoop.Text = "Stop Mic Loopback";
            int micIndex = cmbMic.SelectedIndex;
            micThread = new Thread(() => MicLoopback.LoopMicrophone(micIndex));
            micThread.Start();
        }
        else
        {
            micRunning = false;
            btnMicLoop.Text = "Start Mic Loopback";
            micThread?.Interrupt();
        }
    }

    private void BtnAudioDup_Click(object? sender, EventArgs e)
    {
        if (!dupRunning)
        {
            dupRunning = true;
            btnAudioDup.Text = "Stop Audio Duplicate";
            int speakerIndex = cmbSpeaker.SelectedIndex;
            dupThread = new Thread(() => MicLoopback.DuplicateAudio(speakerIndex));
            dupThread.Start();
        }
        else
        {
            dupRunning = false;
            btnAudioDup.Text = "Start Audio Duplicate";
            dupThread?.Interrupt();
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
