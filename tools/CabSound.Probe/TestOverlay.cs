using System.Drawing;
using System.Windows.Forms;

/// <summary>Test overlay: a big always-on-top box shown on every screen, to check it appears over VPX.</summary>
sealed class TestOverlay
{
    private readonly List<(Form Form, Label Label)> _windows = new();
    private readonly SynchronizationContext _ui;

    public TestOverlay()
    {
        SynchronizationContext? ctx = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            foreach (var screen in Screen.AllScreens)
            {
                var form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None, TopMost = true, ShowInTaskbar = false,
                    BackColor = Color.FromArgb(20, 20, 28), StartPosition = FormStartPosition.Manual,
                    Size = new Size(900, 560), Opacity = 0.92,
                };
                form.Location = new Point(screen.Bounds.Left + (screen.Bounds.Width - form.Width) / 2,
                                          screen.Bounds.Top + (screen.Bounds.Height - form.Height) / 2);
                var label = new Label
                {
                    Dock = DockStyle.Fill, ForeColor = Color.White, Padding = new Padding(30),
                    Font = new Font("Segoe UI", 26, FontStyle.Bold),
                };
                form.Controls.Add(label);
                _windows.Add((form, label));
            }
            ctx = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            ready.Set();
            Application.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        _ui = ctx!;
    }

    public void Show(string text) => _ui.Post(_ =>
    {
        foreach (var (form, label) in _windows) { label.Text = text; if (!form.Visible) form.Show(); form.TopMost = true; }
    }, null);

    public void Hide() => _ui.Post(_ => { foreach (var (form, _) in _windows) form.Hide(); }, null);
}
