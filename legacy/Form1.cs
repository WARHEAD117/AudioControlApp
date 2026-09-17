using System.Diagnostics;
using System.ServiceProcess;
using System.Xml.Linq;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.MediaFoundation;
using NAudio.Utils;
using static System.Net.Mime.MediaTypeNames;

namespace AudioControlApp
{
    public partial class Form1 : Form
    {
        enum SpeakerConfig
        {
            SPEAKER_FRONT_LEFT = 0x1,
            SPEAKER_FRONT_RIGHT = 0x2,
            SPEAKER_FRONT_CENTER = 0x4,
            SPEAKER_LOW_FREQUENCY = 0x8,
            SPEAKER_BACK_LEFT = 0x10,
            SPEAKER_BACK_RIGHT = 0x20,
            SPEAKER_FRONT_LEFT_OF_CENTER = 0x40,
            SPEAKER_FRONT_RIGHT_OF_CENTER = 0x80,
            SPEAKER_BACK_CENTER = 0x100,
            SPEAKER_SIDE_LEFT = 0x200,
            SPEAKER_SIDE_RIGHT = 0x400,
            SPEAKER_TOP_CENTER = 0x800,
            SPEAKER_TOP_FRONT_LEFT = 0x1000,
            SPEAKER_TOP_FRONT_CENTER = 0x2000,
            SPEAKER_TOP_FRONT_RIGHT = 0x4000,
            SPEAKER_TOP_BACK_LEFT = 0x8000,
            SPEAKER_TOP_BACK_CENTER = 0x10000,
            SPEAKER_TOP_BACK_RIGHT = 0x20000
        }

        ServiceController service = new ServiceController("Audiosrv");
        public Form1()
        {
            InitializeComponent();

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 2000;
            timer.Tick += new EventHandler(UpdateData);
            timer.Enabled = true;
            timer.Start();



            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }


        private void UpdateData(object sender, EventArgs e)
        {
            UpdateDeviceInfo();
        }

        bool CheckStartupExist()
        {
            bool isExit = false;
            RegistryKey key = Registry.CurrentUser.OpenSubKey(
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            //RegistryKey key2 = key.OpenSubKey("AudioControlApp", true);
            string[] subkeyNames = key.GetValueNames();
            foreach (string keyName in subkeyNames)
            {
                if (keyName == "AudioControlApp")
                {
                    isExit = true;
                }
            }
            return isExit;
        }
        private void UpdateDeviceInfo()
        {
            
            autoStartupItem.Checked = CheckStartupExist();

            var deviceEnum = new MMDeviceEnumerator();
            {
                var device = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                label1.Text = device.FriendlyName;
                if (device.Properties.Contains(PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers))
                {
                    var value = device.Properties[PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers];

                    if(value.Value.ToString() == "3")
                    {
                        label2.Text = "当前配置：2.0";
                        menuCurrent.Text = "当前配置：2.0";
                        menuItem20.Checked = true;
                        menuItem51.Checked = false;
                        string path = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                        notifyIcon1.Icon = new Icon(path + "/2.0.ico");
                    }
                    else if(value.Value.ToString() == "63")
                    {
                        label2.Text = "当前配置：5.1";
                        menuCurrent.Text = "当前配置：5.1";
                        menuItem20.Checked = false;
                        menuItem51.Checked = true;
                        string path = System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                        notifyIcon1.Icon = new Icon(path + "/5.1.ico");
                    }
                }
                device.Properties.Commit();
            }
        }
        private void RestartServer()
        {
            try
            {
                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped);
                }

                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running);
            }

            catch (Exception)
            {
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var proc = new Process();
            var pi = new ProcessStartInfo();
            pi.UseShellExecute = true;
            pi.FileName = "mmsys.cpl";
            proc.StartInfo = pi;
            proc.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateDeviceInfo();
        }

        /*
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {

            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowInTaskbar = true;
            this.Activate();
        }
        */
        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var deviceEnum = new MMDeviceEnumerator();
                {
                    var device = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                    label1.Text = device.FriendlyName;
                    if (device.Properties.Contains(PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers))
                    {
                        var value = device.Properties[PropertyKeys.PKEY_AudioEndpoint_PhysicalSpeakers];

                        if (value.Value.ToString() == "3")
                        {
                            System.Diagnostics.Process.Start("SoundVolumeView.exe", "/SetSpeakersConfig \"DENON-AVR\" 0x3f 0x3f 0x3f");

                        }
                        else if (value.Value.ToString() == "63")
                        {
                            System.Diagnostics.Process.Start("SoundVolumeView.exe", "/SetSpeakersConfig \"DENON-AVR\" 0x3 0x3 0x3");

                        }
                    }
                }
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }

        private void menuItem51_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("SoundVolumeView.exe", "/SetSpeakersConfig \"DENON-AVR\" 0x3f 0x3f 0x3f");
            Thread.Sleep(1000); //Wait 1 seconds
            RestartServer();
        }

        private void menuItem20_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("SoundVolumeView.exe", "/SetSpeakersConfig \"DENON-AVR\" 0x3 0x3 0x3");

            Thread.Sleep(1000); //Wait 1 seconds
            RestartServer();
        }

        private void menuShow_Click(object sender, EventArgs e)
        {
            var proc = new Process();
            var pi = new ProcessStartInfo();
            pi.UseShellExecute = true;
            pi.FileName = "mmsys.cpl";
            proc.StartInfo = pi;
            proc.Start();
        }

        private void AutoStartup_Click(object sender, EventArgs e)
        {
            bool isExit = CheckStartupExist();

            if (isExit)
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.DeleteValue("AudioControlApp");
            }
            else
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.SetValue("AudioControlApp", System.Windows.Forms.Application.ExecutablePath);
            }
        }

        private void showInterface_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
            else
            {

                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.ShowInTaskbar = true;
                this.Activate();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            e.Cancel = true;

        }
    }
}