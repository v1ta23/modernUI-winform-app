using WinFormsApp.Controllers;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WinFormsApp.Views;

internal sealed class LoginForm : Form
{
    private static readonly Color WindowBackground = Color.FromArgb(10, 10, 15);
    private static readonly Color WindowBackgroundAlt = Color.FromArgb(18, 21, 30);
    private static readonly Color ShellBorder = Color.FromArgb(76, 84, 106);
    private static readonly Color PanelFill = Color.FromArgb(234, 24, 29, 40);
    private static readonly Color PanelBorder = Color.FromArgb(92, 104, 130);
    private static readonly Color InputFill = Color.FromArgb(22, 26, 36);
    private static readonly Color InputBorder = Color.FromArgb(70, 80, 102);
    private static readonly Color InputBorderActive = Color.FromArgb(88, 130, 255);
    private static readonly Color AccentColor = Color.FromArgb(88, 130, 255);
    private static readonly Color AccentHoverColor = Color.FromArgb(104, 144, 255);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(210, 215, 230);
    private static readonly Color TextMuted = Color.FromArgb(160, 170, 190);

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtTransientWindow = 3;
    private const int WcaAccentPolicy = 19;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private const int FormClientWidth = 700;
    private const int FormClientHeight = 680;
    private const int FormMinimumWidth = 620;
    private const int FormMinimumHeight = 660;
    private const int ShellPaddingSize = 24;
    private const int CardPaddingSize = 24;
    private const int ContentColumnWidth = 336;
    private const int FormCardWidth = ContentColumnWidth + (CardPaddingSize * 2);
    private const int InputHostHeight = 48;
    private const int ButtonRowHeight = 46;
    private const int ButtonGap = 16;
    private const int SecondaryButtonWidth = 144;
    private const int PrimaryButtonWidth = ContentColumnWidth - SecondaryButtonWidth - ButtonGap;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    private sealed class SurfacePanel : Panel
    {
        private int _cornerRadius = 24;
        private Color _fillColor = PanelFill;
        private Color _strokeColor = PanelBorder;
        private Bitmap? _surfaceCache;
        private bool _surfaceCacheDirty = true;

        public SurfacePanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(4, value);
                _surfaceCacheDirty = true;
                Invalidate();
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                _surfaceCacheDirty = true;
                Invalidate();
            }
        }

        public Color StrokeColor
        {
            get => _strokeColor;
            set
            {
                _strokeColor = value;
                _surfaceCacheDirty = true;
                Invalidate();
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            _surfaceCacheDirty = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is not null)
            {
                using var parentBrush = new SolidBrush(Parent.BackColor);
                e.Graphics.FillRectangle(parentBrush, ClientRectangle);
                return;
            }

            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            EnsureSurfaceCache();
            if (_surfaceCache is not null)
            {
                e.Graphics.DrawImageUnscaled(_surfaceCache, 0, 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _surfaceCache?.Dispose();
                _surfaceCache = null;
            }

            base.Dispose(disposing);
        }

        private void EnsureSurfaceCache()
        {
            if (!_surfaceCacheDirty && _surfaceCache is not null && _surfaceCache.Width == Width && _surfaceCache.Height == Height)
            {
                return;
            }

            _surfaceCache?.Dispose();
            _surfaceCache = new Bitmap(Width, Height);

            using var graphics = Graphics.FromImage(_surfaceCache);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundRectPath(rect, CornerRadius);
            using var fillBrush = new SolidBrush(FillColor);
            using var borderPen = new Pen(StrokeColor, 1F);

            graphics.FillPath(fillBrush, path);
            graphics.DrawPath(borderPen, path);
            _surfaceCacheDirty = false;
        }
    }

    private enum DashboardCloseMode
    {
        ExitApplication,
        ReturnToLogin,
        SwitchAccount
    }

    private readonly LoginController _controller;
    private readonly AppCompositionRoot _compositionRoot;
    private readonly TextBox _accountTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly CheckBox _rememberCheckBox;
    private readonly CheckBox _showPasswordCheckBox;
    private SurfacePanel? _formCard;
    private Bitmap? _backgroundCache;
    private Bitmap? _resizeCardSnapshot;
    private bool _backgroundCacheDirty = true;
    private bool _isInteractiveResize;
    private MainForm? _currentDashboard;
    private DashboardCloseMode _dashboardCloseMode = DashboardCloseMode.ExitApplication;

    public LoginForm(LoginController controller, AppCompositionRoot compositionRoot)
    {
        _controller = controller;
        _compositionRoot = compositionRoot;

        Text = "\u767b\u5f55";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        ClientSize = new Size(FormClientWidth, FormClientHeight);
        MinimumSize = new Size(FormMinimumWidth, FormMinimumHeight);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = WindowBackground;
        Padding = new Padding(24);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _accountTextBox = CreateInputTextBox("\u8bf7\u8f93\u5165\u8d26\u53f7");
        _passwordTextBox = CreateInputTextBox("\u8bf7\u8f93\u5165\u5bc6\u7801", usePasswordChar: true);
        _rememberCheckBox = CreateOptionCheckBox("\u8bb0\u4f4f\u5bc6\u7801");
        _showPasswordCheckBox = CreateOptionCheckBox("\u663e\u793a\u5bc6\u7801");
        _showPasswordCheckBox.CheckedChanged += (_, _) =>
        {
            _passwordTextBox.UseSystemPasswordChar = !_showPasswordCheckBox.Checked;
        };

        Controls.Add(BuildShell());

        Load += OnLoad;
        Shown += (_, _) => _accountTextBox.Focus();
        HandleCreated += (_, _) => ApplyWindowChrome();
        Paint += OnPaintBackgroundGlow;
        SizeChanged += (_, _) => OnFormSizeChanged();
        FormClosed += (_, _) =>
        {
            DisposeBackgroundCache();
            DisposeResizeSnapshot();
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmSizing)
        {
            BeginInteractiveResize();
        }

        base.WndProc(ref m);

        if (m.Msg == WmExitSizeMove)
        {
            EndInteractiveResize();
        }
    }

    private Control BuildShell()
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(ShellPaddingSize)
        };
        shell.Controls.Add(BuildFormPanel());
        return shell;
    }

    private Control BuildFormPanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var card = new SurfacePanel
        {
            Anchor = AnchorStyles.None,
            CornerRadius = 24,
            FillColor = PanelFill,
            StrokeColor = PanelBorder,
            Padding = new Padding(CardPaddingSize),
            Size = new Size(FormCardWidth, 420)
        };
        _formCard = card;
        outer.Controls.Add(card);

        void CenterCard()
        {
            card.Left = Math.Max(0, (outer.ClientSize.Width - card.Width) / 2);
            card.Top = Math.Max(0, (outer.ClientSize.Height - card.Height) / 2);
        }

        outer.Resize += (_, _) => CenterCard();

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelFill
        };
        card.Controls.Add(content);

        var top = 0;
        content.Controls.Add(CreateLabel("ACCESS", new Font("Segoe UI", 9.5F, FontStyle.Bold), AccentColor, ref top, 8));
        content.Controls.Add(CreateLabel("\u6b22\u8fce\u767b\u5f55", new Font("Microsoft YaHei UI", 20F, FontStyle.Bold), TextPrimary, ref top, 8));
        content.Controls.Add(CreateWrappedLabel("\u8bf7\u8f93\u5165\u8d26\u53f7\u4e0e\u5bc6\u7801\u3002", TextSecondary, ContentColumnWidth, ref top, 20));
        content.Controls.Add(CreateFieldLabel("\u8d26\u53f7", ref top));
        content.Controls.Add(CreateInputHost(_accountTextBox, ref top));
        content.Controls.Add(CreateFieldLabel("\u5bc6\u7801", ref top));
        content.Controls.Add(CreateInputHost(_passwordTextBox, ref top));
        content.Controls.Add(CreateOptionsPanel(ref top));
        content.Controls.Add(CreateButtonsPanel(ref top));
        content.Controls.Add(CreateWrappedLabel("\u9700\u8981\u65b0\u8d26\u53f7\u65f6\uff0c\u53ef\u4ee5\u76f4\u63a5\u70b9\u51fb\u6ce8\u518c\u3002", TextMuted, ContentColumnWidth, ref top, 0));

        card.Size = new Size(card.Width, top + 52);
        CenterCard();

        return outer;
    }

    private Control CreateOptionsPanel(ref int top)
    {
        var rememberSize = _rememberCheckBox.GetPreferredSize(Size.Empty);
        var showSize = _showPasswordCheckBox.GetPreferredSize(Size.Empty);
        _rememberCheckBox.Size = rememberSize;
        _showPasswordCheckBox.Size = showSize;

        var panelHeight = Math.Max(rememberSize.Height, showSize.Height) + 6;
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(0, top + 2),
            Size = new Size(ContentColumnWidth, panelHeight)
        };

        _rememberCheckBox.Location = new Point(0, (panelHeight - rememberSize.Height) / 2);
        panel.Controls.Add(_rememberCheckBox);

        _showPasswordCheckBox.Location = new Point(panel.Width - _showPasswordCheckBox.Width, (panelHeight - showSize.Height) / 2);
        _showPasswordCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(_showPasswordCheckBox);

        top += panelHeight + 18;
        return panel;
    }

    private Control CreateButtonsPanel(ref int top)
    {
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(0, top),
            Size = new Size(ContentColumnWidth, ButtonRowHeight)
        };

        var registerButton = CreateSecondaryButton("\u6ce8\u518c");
        registerButton.Bounds = new Rectangle(0, 0, SecondaryButtonWidth, ButtonRowHeight);
        registerButton.Click += (_, _) =>
        {
            using var registerForm = _compositionRoot.CreateRegisterForm();
            registerForm.ShowDialog(this);
        };

        var loginButton = CreatePrimaryButton("\u767b\u5f55");
        loginButton.Bounds = new Rectangle(SecondaryButtonWidth + ButtonGap, 0, PrimaryButtonWidth, ButtonRowHeight);
        loginButton.Click += OnLoginClicked;
        AcceptButton = loginButton;

        panel.Controls.Add(registerButton);
        panel.Controls.Add(loginButton);
        top += ButtonRowHeight + 16;
        return panel;
    }

    private Control CreateFieldLabel(string text, ref int top)
    {
        return CreateLabel(text, new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), TextSecondary, ref top, 8);
    }

    private Control CreateInputHost(TextBox textBox, ref int top)
    {
        var border = new Panel
        {
            BackColor = InputBorder,
            Location = new Point(0, top),
            Padding = new Padding(1),
            Size = new Size(ContentColumnWidth, InputHostHeight)
        };

        var inner = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 16,
            FillColor = InputFill,
            StrokeColor = InputFill,
            Padding = new Padding(14, 12, 14, 12)
        };

        textBox.Location = new Point(14, 12);
        textBox.Size = new Size(inner.Width - 28, 24);
        textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        inner.Controls.Add(textBox);
        border.Controls.Add(inner);

        void SetActive(bool active)
        {
            border.BackColor = active ? InputBorderActive : InputBorder;
        }

        textBox.Enter += (_, _) => SetActive(true);
        textBox.Leave += (_, _) => SetActive(false);
        inner.Enter += (_, _) => textBox.Focus();

        top += 62;
        return border;
    }

    private static Control CreateLabel(string text, Font font, Color color, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            Location = new Point(0, top),
            Text = text
        };
        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static Control CreateWrappedLabel(string text, Font font, Color color, int width, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            MaximumSize = new Size(width, 0),
            Location = new Point(0, top),
            Text = text
        };
        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static Control CreateWrappedLabel(string text, Color color, int width, ref int top, int bottomSpacing)
    {
        return CreateWrappedLabel(text, new Font("Microsoft YaHei UI", 9F), color, width, ref top, bottomSpacing);
    }

    private static CheckBox CreateOptionCheckBox(string text)
    {
        return new CheckBox
        {
            AutoSize = true,
            CheckAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = text
        };
    }

    private static TextBox CreateInputTextBox(string placeholderText, bool usePasswordChar = false)
    {
        return new TextBox
        {
            BackColor = InputFill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 11F),
            ForeColor = TextPrimary,
            Margin = new Padding(0),
            PlaceholderText = placeholderText,
            UseSystemPasswordChar = usePasswordChar
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        var button = new Button
        {
            BackColor = AccentColor,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Text = text
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        button.FlatAppearance.MouseDownBackColor = AccentHoverColor;
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = new Button
        {
            BackColor = Color.FromArgb(18, 22, 30),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Text = text
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = ShellBorder;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(26, 30, 40);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(26, 30, 40);
        return button;
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        ApplyInitialState();
    }

    private void ApplyInitialState()
    {
        var state = _controller.LoadInitialState();
        _accountTextBox.Text = state.Account;
        _passwordTextBox.Text = state.Password;
        _rememberCheckBox.Checked = state.RememberPassword;
        _showPasswordCheckBox.Checked = false;
    }

    private void ClearLoginInputs()
    {
        _accountTextBox.Clear();
        _passwordTextBox.Clear();
        _rememberCheckBox.Checked = false;
        _showPasswordCheckBox.Checked = false;
    }

    private void RestoreLoginForm()
    {
        _dashboardCloseMode = DashboardCloseMode.ExitApplication;
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
        BringToFront();
        _accountTextBox.Focus();
        if (_accountTextBox.TextLength > 0)
        {
            _accountTextBox.SelectAll();
        }
    }

    private MainForm OpenDashboard(string account)
    {
        var dashboard = _compositionRoot.CreateDashboardForm(account);
        _currentDashboard = dashboard;
        _dashboardCloseMode = DashboardCloseMode.ExitApplication;
        dashboard.SwitchAccountRequested += OnDashboardSwitchAccountRequested;
        dashboard.LogoutRequested += OnDashboardLogoutRequested;
        dashboard.FormClosed += OnDashboardClosed;
        Hide();
        dashboard.Show();
        return dashboard;
    }

    private void OnDashboardSwitchAccountRequested(object? sender, EventArgs e)
    {
        _dashboardCloseMode = DashboardCloseMode.SwitchAccount;
        _currentDashboard?.Close();
    }

    private void OnDashboardLogoutRequested(object? sender, EventArgs e)
    {
        _dashboardCloseMode = DashboardCloseMode.ReturnToLogin;
        _currentDashboard?.Close();
    }

    private void OnDashboardClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is MainForm dashboard)
        {
            dashboard.SwitchAccountRequested -= OnDashboardSwitchAccountRequested;
            dashboard.LogoutRequested -= OnDashboardLogoutRequested;
            dashboard.FormClosed -= OnDashboardClosed;
            if (ReferenceEquals(_currentDashboard, dashboard))
            {
                _currentDashboard = null;
            }
        }

        if (_dashboardCloseMode == DashboardCloseMode.SwitchAccount)
        {
            ClearLoginInputs();
            RestoreLoginForm();
            return;
        }

        if (_dashboardCloseMode == DashboardCloseMode.ReturnToLogin)
        {
            _controller.Logout();
            ClearLoginInputs();
            RestoreLoginForm();
            return;
        }

        Close();
    }

    private void OnPaintBackgroundGlow(object? sender, PaintEventArgs e)
    {
        if (_isInteractiveResize || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            using var resizeBrush = new SolidBrush(WindowBackgroundAlt);
            e.Graphics.FillRectangle(resizeBrush, ClientRectangle);
            DrawResizeSnapshot(e.Graphics);
            return;
        }

        EnsureBackgroundCache();
        if (_backgroundCache is not null)
        {
            e.Graphics.DrawImageUnscaled(_backgroundCache, Point.Empty);
        }
    }

    private void ApplyWindowChrome()
    {
        try
        {
            var darkMode = 1;
            DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            var cornerPreference = 2;
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

            var borderColor = ToColorRef(ShellBorder);
            DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref borderColor, sizeof(int));

            var textColor = ToColorRef(TextPrimary);
            DwmSetWindowAttribute(Handle, DwmwaTextColor, ref textColor, sizeof(int));

            var backdropType = DwmsbtTransientWindow;
            DwmSetWindowAttribute(Handle, DwmwaSystemBackdropType, ref backdropType, sizeof(int));

            EnableAcrylicBlur();
        }
        catch
        {
            // Unsupported window effects fall back to the in-app glass shell.
        }
    }

    private void EnableAcrylicBlur()
    {
        ApplyAccentPolicy(3, Color.FromArgb(196, 12, 16, 22));
    }

    private void ApplyAccentPolicy(int accentState, Color gradientColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = accentState,
            AccentFlags = 2,
            GradientColor = ToAbgr(gradientColor)
        };

        var accentSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(accentSize);

        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPtr,
                SizeOfData = accentSize
            };

            SetWindowCompositionAttribute(Handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private void BeginInteractiveResize()
    {
        if (_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = true;
        CaptureResizeSnapshot();
        if (_resizeCardSnapshot is not null && _formCard is not null)
        {
            _formCard.Visible = false;
        }
        Invalidate();
    }

    private void EndInteractiveResize()
    {
        if (!_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = false;
        if (_formCard is not null)
        {
            _formCard.Visible = true;
        }
        DisposeResizeSnapshot();
        _backgroundCacheDirty = true;
        Invalidate(true);
    }

    private void OnFormSizeChanged()
    {
        if (!_isInteractiveResize)
        {
            _backgroundCacheDirty = true;
        }
    }

    private void EnsureBackgroundCache()
    {
        if (!_backgroundCacheDirty &&
            _backgroundCache is not null &&
            _backgroundCache.Width == ClientSize.Width &&
            _backgroundCache.Height == ClientSize.Height)
        {
            return;
        }

        DisposeBackgroundCache();
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        _backgroundCache = new Bitmap(ClientSize.Width, ClientSize.Height);
        using var graphics = Graphics.FromImage(_backgroundCache);
        using var backgroundBrush = new LinearGradientBrush(ClientRectangle, WindowBackground, WindowBackgroundAlt, 35F);
        graphics.FillRectangle(backgroundBrush, new Rectangle(Point.Empty, ClientSize));
        _backgroundCacheDirty = false;
    }

    private void DisposeBackgroundCache()
    {
        _backgroundCache?.Dispose();
        _backgroundCache = null;
    }

    private void CaptureResizeSnapshot()
    {
        DisposeResizeSnapshot();
        if (_formCard is null || _formCard.Width <= 0 || _formCard.Height <= 0)
        {
            return;
        }

        _resizeCardSnapshot = new Bitmap(_formCard.Width, _formCard.Height);
        _formCard.DrawToBitmap(_resizeCardSnapshot, new Rectangle(Point.Empty, _formCard.Size));
    }

    private void DrawResizeSnapshot(Graphics graphics)
    {
        if (_resizeCardSnapshot is null || _formCard is null || _formCard.Parent is null)
        {
            return;
        }

        var topLeft = PointToClient(_formCard.Parent.PointToScreen(_formCard.Location));
        graphics.DrawImageUnscaled(_resizeCardSnapshot, topLeft);
    }

    private void DisposeResizeSnapshot()
    {
        _resizeCardSnapshot?.Dispose();
        _resizeCardSnapshot = null;
    }

    private static uint ToAbgr(Color color)
    {
        return (uint)(color.A << 24 | color.B << 16 | color.G << 8 | color.R);
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void OnLoginClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = _controller.Login(_accountTextBox.Text, _passwordTextBox.Text, _rememberCheckBox.Checked);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "\u767b\u5f55\u5931\u8d25", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenDashboard(result.Account!);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"\u767b\u5f55\u8fc7\u7a0b\u4e2d\u53d1\u751f\u9519\u8bef\uff1a{ex.Message}", "\u9519\u8bef", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
