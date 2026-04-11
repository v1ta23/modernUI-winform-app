namespace WinFormsApp.Views;

internal interface IInteractiveResizeAware
{
    void BeginInteractiveResize();

    void EndInteractiveResize();
}

internal sealed class InteractiveResizeFreezeController : IDisposable
{
    private readonly Control _layoutRoot;
    private readonly FreezeSnapshotOverlay _overlay;

    public InteractiveResizeFreezeController(Control host, Control layoutRoot, Color backgroundColor)
    {
        _layoutRoot = layoutRoot;
        _overlay = new FreezeSnapshotOverlay
        {
            BackColor = backgroundColor,
            Dock = DockStyle.Fill,
            Visible = false
        };

        host.Controls.Add(_overlay);
        layoutRoot.BringToFront();
    }

    public bool IsActive { get; private set; }

    public void Begin()
    {
        if (IsActive)
        {
            return;
        }

        var snapshot = TryCaptureSnapshot();
        if (snapshot is null)
        {
            return;
        }

        IsActive = true;
        _overlay.SetSnapshot(snapshot);
        _layoutRoot.SuspendLayout();
        _layoutRoot.Visible = false;
        _overlay.Visible = true;
        _overlay.BringToFront();
    }

    public void End()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        _overlay.Visible = false;
        _layoutRoot.Visible = true;
        _layoutRoot.BringToFront();
        _layoutRoot.ResumeLayout(true);
        _overlay.ClearSnapshot();
    }

    public void Dispose()
    {
        _overlay.Dispose();
    }

    private Bitmap? TryCaptureSnapshot()
    {
        if (_layoutRoot.Width <= 0 || _layoutRoot.Height <= 0)
        {
            return null;
        }

        try
        {
            var snapshot = new Bitmap(_layoutRoot.Width, _layoutRoot.Height);
            _layoutRoot.DrawToBitmap(snapshot, new Rectangle(Point.Empty, _layoutRoot.Size));
            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    private sealed class FreezeSnapshotOverlay : Control
    {
        private Bitmap? _snapshot;

        public FreezeSnapshotOverlay()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }

        public void SetSnapshot(Bitmap snapshot)
        {
            ClearSnapshot();
            _snapshot = snapshot;
            Invalidate();
        }

        public void ClearSnapshot()
        {
            _snapshot?.Dispose();
            _snapshot = null;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            using var brush = new SolidBrush(BackColor);
            pevent.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_snapshot is null)
            {
                return;
            }

            e.Graphics.DrawImageUnscaled(_snapshot, Point.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearSnapshot();
            }

            base.Dispose(disposing);
        }
    }
}
