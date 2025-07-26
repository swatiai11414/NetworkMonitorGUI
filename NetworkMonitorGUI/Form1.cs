using System;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Windows.Forms;

namespace NetworkMonitorGUI
{
    public partial class Form1 : Form
    {
        private Process psProcess;
        private readonly SpeechSynthesizer synth = new SpeechSynthesizer();
        private string scriptPath = "";

        private readonly string[] suspiciousKeywords = new string[]
        {
            "Remove-Item", "Stop-Process", "Invoke-Expression", "Start-Process",
            "New-Object System.Net.WebClient", "DownloadFile", "Set-MpPreference",
            "Get-CimInstance", "Invoke-WebRequest", "Add-MpPreference", "Set-ExecutionPolicy",
            "Get-WmiObject", "Remove-WmiObject", "Format-Hex", "Invoke-Command",
            "HideWindow", "ScheduleTask", ":\\windows\\", "base64", "certutil",
            "PowerShell -EncodedCommand"
        };

        public Form1()
        {
            InitializeComponent();
            SetupHackerTheme();
            btnStop.Enabled = false;
            lblStatus.Text = "Status: Idle";
        }

        private void SetupHackerTheme()
        {
            this.BackColor = System.Drawing.Color.Black;
            rtbOutput.BackColor = System.Drawing.Color.Black;
            rtbOutput.ForeColor = System.Drawing.Color.LimeGreen;
            rtbOutput.Font = new System.Drawing.Font("Consolas", 10);
            lblStatus.ForeColor = System.Drawing.Color.LimeGreen;

            btnBrowseScript.BackColor = System.Drawing.Color.DarkSlateGray;
            btnBrowseScript.ForeColor = System.Drawing.Color.White;
            btnStart.BackColor = System.Drawing.Color.DarkGreen;
            btnStart.ForeColor = System.Drawing.Color.Black;
            btnStop.BackColor = System.Drawing.Color.DarkRed;
            btnStop.ForeColor = System.Drawing.Color.Black;
        }

        private bool ValidateScriptFile(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                MessageBox.Show("Selected script file does not exist.", "Error");
                return false;
            }
            if (!string.Equals(System.IO.Path.GetExtension(path), ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please select a valid PowerShell script (*.ps1).", "Error");
                return false;
            }
            return true;
        }

        private bool IsScriptSafe(string scriptFilePath)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(scriptFilePath);
                if (fileInfo.Length > 5 * 1024 * 1024)
                {
                    MessageBox.Show("Selected script file size is very large; please verify before running.", "Warning");
                    return false;
                }

                var lines = System.IO.File.ReadAllLines(scriptFilePath);
                foreach (var line in lines)
                {
                    foreach (var keyword in suspiciousKeywords)
                    {
                        if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MessageBox.Show($"Warning: Script contains suspicious content: '{keyword}'", "Warning");
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Script safety check failed: {ex.Message}", "Error");
                return false;
            }
        }

        private void btnBrowseScript_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog()
            {
                Filter = "PowerShell Scripts (*.ps1)|*.ps1",
                Title = "Select your PowerShell script"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (ValidateScriptFile(ofd.FileName) && IsScriptSafe(ofd.FileName))
                {
                    scriptPath = ofd.FileName;
                    lblStatus.Text = $"Script selected: {scriptPath}";
                    AppendOutput($"[Info] Selected script path: {scriptPath}");
                }
                else
                {
                    AppendOutput("[Warning] Selected script flagged as potentially unsafe.");
                }
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(scriptPath))
            {
                MessageBox.Show("कृपया पहले .ps1 स्क्रिप्ट चुनें।", "Error");
                return;
            }
            if (psProcess != null && !psProcess.HasExited)
            {
                MessageBox.Show("Script पहले से ही चल रही है!", "Info");
                return;
            }

            psProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };
            psProcess.OutputDataReceived += PsProcess_OutputDataReceived;
            psProcess.ErrorDataReceived += PsProcess_ErrorDataReceived;
            psProcess.Exited += PsProcess_Exited;

            try
            {
                psProcess.Start();
                psProcess.BeginOutputReadLine();
                psProcess.BeginErrorReadLine();

                lblStatus.Text = "Status: Running";
                AppendOutput("[Info] PowerShell script started.");
                btnStart.Enabled = false;
                btnStop.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Script start करने मे समस्या आई। " + ex.Message, "Error");
                lblStatus.Text = "Status: Error";
            }
        }

        private void PsProcess_Exited(object sender, EventArgs e)
        {
            this.Invoke(new Action(() =>
            {
                lblStatus.Text = "Status: Stopped";
                AppendOutput("[Info] PowerShell script exited.");
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }));
        }

        private void PsProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                this.Invoke(new Action(() => AppendOutput("[ERROR] " + e.Data)));
            }
        }

        private void PsProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            this.Invoke(new Action(() =>
            {
                AppendOutput(e.Data);
                if (e.Data.Contains("Alert") || e.Data.Contains("🚨"))
                {
                    MessageBox.Show(e.Data, "Network Alert");
                    try { synth.SpeakAsync("Network speed alert detected."); } catch { }
                }
            }));
        }

        private void AppendOutput(string line)
        {
            if (line.Contains("ERROR"))
                rtbOutput.SelectionColor = System.Drawing.Color.Red;
            else if (line.Contains("Alert") || line.Contains("🚨"))
                rtbOutput.SelectionColor = System.Drawing.Color.Yellow;
            else
                rtbOutput.SelectionColor = System.Drawing.Color.LimeGreen;

            rtbOutput.AppendText(line + Environment.NewLine);
            rtbOutput.ScrollToCaret();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (psProcess != null && !psProcess.HasExited)
            {
                try
                {
                    psProcess.Kill();
                    lblStatus.Text = "Status: Stopped";
                    AppendOutput("[Info] PowerShell script stopped by user.");
                    btnStart.Enabled = true;
                    btnStop.Enabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Script stop करने मे त्रुटि: " + ex.Message, "Error");
                }
            }
            else
            {
                MessageBox.Show("कोई चलती हुई स्क्रिप्ट नहीं मिली।", "Info");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (psProcess != null && !psProcess.HasExited)
            {
                try { psProcess.Kill(); } catch { }
            }
            synth.Dispose();
            base.OnFormClosing(e);
        }
    }
}
