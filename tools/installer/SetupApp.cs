using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CabSoundSetup
{
    internal static class Program
    {
        private const string Version = "1.0.0";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }

        private sealed class SetupForm : Form
        {
            private readonly TextBox _txtDir;
            private readonly CheckBox _chkDesktop;
            private readonly CheckBox _chkStartMenu;
            private readonly CheckBox _chkStartup;
            private readonly CheckBox _chkLaunch;
            private readonly Button _btnInstall;

            public SetupForm()
            {
                Text = "CabSound " + Version + " Setup";
                ClientSize = new Size(492, 312);
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                Font = new Font("Segoe UI", 9.5f);
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

                var defaultDir = Directory.Exists(@"C:\vPinball")
                    ? @"C:\vPinball\CabSound"
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CabSound");

                Controls.Add(new Label
                {
                    Text = "Install CabSound " + Version,
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                    Location = new Point(20, 16),
                    AutoSize = true
                });

                Controls.Add(new Label
                {
                    Text = "Per-table volume, 7.1 SSF levels, and Equalizer APO front end for virtual pinball cabinets.",
                    Location = new Point(20, 48),
                    Size = new Size(452, 34)
                });

                Controls.Add(new Label
                {
                    Text = "Install folder (your CabSound.json and backups are kept if upgrading):",
                    Location = new Point(20, 88),
                    AutoSize = true
                });

                _txtDir = new TextBox
                {
                    Text = defaultDir,
                    Location = new Point(20, 112),
                    Size = new Size(364, 25)
                };
                Controls.Add(_txtDir);

                var btnBrowse = new Button
                {
                    Text = "Browse...",
                    Location = new Point(392, 111),
                    Size = new Size(80, 27)
                };
                btnBrowse.Click += delegate
                {
                    using (var dlg = new FolderBrowserDialog { SelectedPath = _txtDir.Text })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK) _txtDir.Text = dlg.SelectedPath;
                    }
                };
                Controls.Add(btnBrowse);

                _chkDesktop = Check("Create Desktop shortcut", 152, true);
                _chkStartMenu = Check("Create Start Menu shortcut", 178, true);
                _chkStartup = Check("Start CabSound automatically with Windows", 204, true);
                _chkLaunch = Check("Launch CabSound after installation", 230, true);

                _btnInstall = new Button
                {
                    Text = "Install",
                    Location = new Point(304, 266),
                    Size = new Size(80, 30)
                };
                _btnInstall.Click += OnInstall;
                AcceptButton = _btnInstall;
                Controls.Add(_btnInstall);

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    Location = new Point(392, 266),
                    Size = new Size(80, 30)
                };
                btnCancel.Click += delegate { Close(); };
                CancelButton = btnCancel;
                Controls.Add(btnCancel);
            }

            private CheckBox Check(string text, int y, bool isChecked)
            {
                var cb = new CheckBox
                {
                    Text = text,
                    Checked = isChecked,
                    Location = new Point(22, y),
                    AutoSize = true
                };
                Controls.Add(cb);
                return cb;
            }

            private void OnInstall(object sender, EventArgs e)
            {
                var targetDir = _txtDir.Text.Trim();
                if (string.IsNullOrEmpty(targetDir))
                {
                    MessageBox.Show(this, "Please choose an install folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    var running = Process.GetProcessesByName("CabSound");
                    if (running.Length > 0)
                    {
                        var ans = MessageBox.Show(this,
                            "CabSound is currently running. Close it now to continue installing?",
                            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (ans != DialogResult.Yes) return;
                        foreach (var p in running)
                        {
                            try { p.Kill(); p.WaitForExit(3000); } catch { }
                            p.Dispose();
                        }
                        Thread.Sleep(400);
                    }

                    _btnInstall.Enabled = false;
                    Cursor = Cursors.WaitCursor;

                    Directory.CreateDirectory(targetDir);
                    var exeDest = Path.Combine(targetDir, "CabSound.exe");
                    var readmeDest = Path.Combine(targetDir, "README.txt");
                    var uninstDest = Path.Combine(targetDir, "Uninstall-CabSound.ps1");

                    ExtractResource("CabSound.exe", exeDest);
                    ExtractResource("README.txt", readmeDest);
                    ExtractResource("Uninstall-CabSound.ps1", uninstDest);

                    if (_chkDesktop.Checked)
                    {
                        var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        CreateShortcut(Path.Combine(desk, "CabSound.lnk"), exeDest, targetDir, "CabSound for Virtual Pinball");
                    }
                    if (_chkStartMenu.Checked)
                    {
                        var sm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "CabSound");
                        Directory.CreateDirectory(sm);
                        CreateShortcut(Path.Combine(sm, "CabSound.lnk"), exeDest, targetDir, "CabSound for Virtual Pinball");
                        CreateShortcut(Path.Combine(sm, "CabSound README.lnk"), readmeDest, targetDir, "CabSound User Guide");
                    }
                    if (_chkStartup.Checked)
                    {
                        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                        CreateShortcut(Path.Combine(startup, "CabSound.lnk"), exeDest, targetDir, "CabSound for Virtual Pinball");
                    }

                    RegisterUninstaller(targetDir, exeDest, uninstDest);

                    Cursor = Cursors.Default;
                    MessageBox.Show(this,
                        "CabSound " + Version + " has been installed to:\n" + targetDir,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (_chkLaunch.Checked)
                    {
                        Process.Start(new ProcessStartInfo(exeDest) { WorkingDirectory = targetDir, UseShellExecute = true });
                    }
                    Close();
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    _btnInstall.Enabled = true;
                    MessageBox.Show(this, "Installation failed:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private static void ExtractResource(string resourceName, string destPath)
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var src = asm.GetManifestResourceStream(resourceName))
                {
                    if (src == null) throw new InvalidOperationException("Missing embedded file: " + resourceName);
                    using (var dst = File.Create(destPath))
                    {
                        src.CopyTo(dst);
                    }
                }
            }

            private static void CreateShortcut(string linkPath, string targetPath, string workingDir, string description)
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                    try
                    {
                        var st = shortcut.GetType();
                        st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                        st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                        st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                        st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                    }
                    finally { Marshal.FinalReleaseComObject(shortcut); }
                }
                finally { Marshal.FinalReleaseComObject(shell); }
            }

            private static void RegisterUninstaller(string targetDir, string exeDest, string uninstDest)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\CabSound"))
                {
                    if (key == null) return;
                    key.SetValue("DisplayName", "CabSound");
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", "CabSound");
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("DisplayIcon", exeDest);
                    key.SetValue("UninstallString", "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + uninstDest + "\"");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
        }
    }
}
