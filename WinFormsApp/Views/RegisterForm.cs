using System.Drawing.Drawing2D;
using WinFormsApp.Controllers;

namespace WinFormsApp.Views;

internal sealed class RegisterForm : Form
{
    private static readonly Color WindowBackground = Color.FromArgb(10, 10, 15);
    private static readonly Color WindowBackgroundAlt = Color.FromArgb(18, 21, 30);
    private static readonly Color CardFill = Color.FromArgb(236, 22, 28, 40);
    private static readonly Color CardBorder = Color.FromArgb(84, 95, 120);
    private static readonly Color HeroFill = Color.FromArgb(255, 18, 24, 38);
    private static readonly Color InputFill = Color.FromArgb(18, 22, 32);
    private static readonly Color InputBorder = Color.FromArgb(70, 80, 102);
    private static readonly Color InputBorderActive = Color.FromArgb(88, 130, 255);
    private static readonly Color AccentColor = Color.FromArgb(88, 130, 255);
    private static readonly Color AccentHoverColor = Color.FromArgb(106, 146, 255);
    private static readonly Color SecondaryButtonFill = Color.FromArgb(18, 22, 30);
    private static readonly Color SecondaryButtonHover = Color.FromArgb(26, 30, 40);
    private static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    private static readonly Color TextSecondary = Color.FromArgb(216, 221, 234);
    private static readonly Color TextMuted = Color.FromArgb(158, 168, 188);

    private readonly RegisterController _controller;
    private readonly TextBox _accountTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBox _confirmTextBox;
    private readonly CheckBox _showPasswordCheckBox;

    private sealed class SurfacePanel : Panel
    {
        private int _cornerRadius = 28;
        private Color _fillColor = CardFill;
        private Color _strokeColor = CardBorder;

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
                _cornerRadius = Math.Max(6, value);
                Invalidate();
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                _fillColor = value;
                Invalidate();
            }
        }

        public Color StrokeColor
        {
            get => _strokeColor;
            set
            {
                _strokeColor = value;
                Invalidate();
            }
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

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundRectPath(rect, CornerRadius);
            using var fillBrush = new SolidBrush(FillColor);
            using var strokePen = new Pen(StrokeColor, 1F);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(strokePen, path);
        }
    }

    public RegisterForm(RegisterController controller)
    {
        _controller = controller;

        Text = "注册账号";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(900, 620);
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = WindowBackground;
        Padding = new Padding(24);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _accountTextBox = CreateInputTextBox("4 到 9 位账号");
        _passwordTextBox = CreateInputTextBox("6 到 9 位密码", usePasswordChar: true);
        _confirmTextBox = CreateInputTextBox("再次输入密码", usePasswordChar: true);
        _showPasswordCheckBox = CreateOptionCheckBox("显示密码");
        _showPasswordCheckBox.CheckedChanged += (_, _) => UpdatePasswordVisibility();

        Controls.Add(BuildShell());
        Shown += (_, _) => _accountTextBox.Focus();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var backgroundBrush = new LinearGradientBrush(ClientRectangle, WindowBackground, WindowBackgroundAlt, 35F);
        e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

        using var glowBrushTop = new SolidBrush(Color.FromArgb(34, AccentColor));
        using var glowBrushBottom = new SolidBrush(Color.FromArgb(24, 68, 176, 255));
        e.Graphics.FillEllipse(glowBrushTop, new Rectangle(-80, -110, 300, 300));
        e.Graphics.FillEllipse(glowBrushBottom, new Rectangle(ClientSize.Width - 260, ClientSize.Height - 220, 320, 320));
    }

    private Control BuildShell()
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(12)
        };

        var card = new SurfacePanel
        {
            Anchor = AnchorStyles.None,
            CornerRadius = 30,
            FillColor = CardFill,
            StrokeColor = CardBorder,
            Size = new Size(820, 548)
        };
        shell.Controls.Add(card);

        void CenterCard()
        {
            card.Left = Math.Max(0, (shell.ClientSize.Width - card.Width) / 2);
            card.Top = Math.Max(0, (shell.ClientSize.Height - card.Height) / 2);
        }

        shell.Resize += (_, _) => CenterCard();

        var formPanel = BuildFormPanel();
        formPanel.Dock = DockStyle.Fill;
        card.Controls.Add(formPanel);

        var heroPanel = BuildHeroPanel();
        heroPanel.Dock = DockStyle.Left;
        heroPanel.Width = 270;
        card.Controls.Add(heroPanel);

        CenterCard();
        return shell;
    }

    private Control BuildHeroPanel()
    {
        var panel = new Panel
        {
            BackColor = HeroFill,
            Padding = new Padding(34, 36, 34, 36)
        };

        var top = 0;
        panel.Controls.Add(CreateLabel("REGISTER", new Font("Segoe UI", 10F, FontStyle.Bold), AccentColor, ref top, 14));
        panel.Controls.Add(CreateWrappedLabel("创建一个能马上登录的新账号", new Font("Microsoft YaHei UI", 20F, FontStyle.Bold), TextPrimary, 220, ref top, 12));
        panel.Controls.Add(CreateWrappedLabel("把三项填好，点一下主按钮就能完成注册。", TextSecondary, 220, ref top, 28));

        panel.Controls.Add(CreateFeatureItem("账号长度", "4 到 9 位，简短好记就行。", ref top));
        panel.Controls.Add(CreateFeatureItem("密码长度", "6 到 9 位，两次输入必须一样。", ref top));
        panel.Controls.Add(CreateFeatureItem("完成后", "注册成功会直接提示你回去登录。", ref top));

        var footer = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMuted,
            Location = new Point(0, top + 16),
            Text = "别担心，主按钮就在右边正中间。"
        };
        panel.Controls.Add(footer);

        return panel;
    }

    private Control BuildFormPanel()
    {
        var panel = new Panel
        {
            BackColor = CardFill,
            Padding = new Padding(32, 32, 32, 30)
        };

        var content = new Panel
        {
            BackColor = CardFill,
            Dock = DockStyle.Fill
        };
        panel.Controls.Add(content);

        const int contentWidth = 452;
        var top = 0;
        content.Controls.Add(CreateLabel("填写注册信息", new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), TextPrimary, ref top, 8));
        content.Controls.Add(CreateWrappedLabel("账号、密码、确认密码都在下面，回车也能直接注册。", TextSecondary, contentWidth, ref top, 18));

        content.Controls.Add(CreateFieldLabel("账号", ref top));
        content.Controls.Add(CreateInputHost(_accountTextBox, contentWidth, ref top));

        content.Controls.Add(CreateFieldLabel("密码", ref top));
        content.Controls.Add(CreateInputHost(_passwordTextBox, contentWidth, ref top));

        content.Controls.Add(CreateFieldLabel("确认密码", ref top));
        content.Controls.Add(CreateInputHost(_confirmTextBox, contentWidth, ref top));

        content.Controls.Add(CreateOptionsPanel(contentWidth, ref top));
        content.Controls.Add(CreateButtonsPanel(contentWidth, ref top));
        content.Controls.Add(CreateWrappedLabel("如果已经有账号，点取消回到登录就行。", TextMuted, contentWidth, ref top, 0));

        return panel;
    }

    private Control CreateFeatureItem(string title, string description, ref int top)
    {
        var container = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(0, top),
            Size = new Size(220, 66)
        };

        var dot = new Panel
        {
            BackColor = AccentColor,
            Location = new Point(0, 7),
            Size = new Size(10, 10)
        };
        container.Controls.Add(dot);

        var titleLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(22, 0),
            Text = title
        };
        container.Controls.Add(titleLabel);

        var descriptionLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextSecondary,
            MaximumSize = new Size(190, 0),
            Location = new Point(22, 24),
            Text = description
        };
        container.Controls.Add(descriptionLabel);

        top += container.Height + 14;
        return container;
    }

    private Control CreateOptionsPanel(int width, ref int top)
    {
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(0, top),
            Size = new Size(width, 28)
        };

        _showPasswordCheckBox.Location = new Point(0, 2);
        panel.Controls.Add(_showPasswordCheckBox);

        var hintLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMuted,
            Text = "按 Enter 直接注册"
        };
        hintLabel.Location = new Point(width - hintLabel.GetPreferredSize(Size.Empty).Width, 4);
        panel.Controls.Add(hintLabel);

        top += panel.Height + 16;
        return panel;
    }

    private Control CreateButtonsPanel(int width, ref int top)
    {
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(0, top),
            Size = new Size(width, 50)
        };

        var submitButton = CreatePrimaryButton("立即注册");
        submitButton.Bounds = new Rectangle(0, 0, 300, 50);
        submitButton.Click += OnSubmitClicked;
        AcceptButton = submitButton;

        var cancelButton = CreateSecondaryButton("取消");
        cancelButton.Bounds = new Rectangle(width - 120, 0, 120, 50);
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Click += (_, _) => Close();
        CancelButton = cancelButton;

        panel.Controls.Add(submitButton);
        panel.Controls.Add(cancelButton);

        top += panel.Height + 14;
        return panel;
    }

    private Control CreateFieldLabel(string text, ref int top)
    {
        return CreateLabel(text, new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold), TextSecondary, ref top, 8);
    }

    private Control CreateInputHost(TextBox textBox, int width, ref int top)
    {
        var border = new Panel
        {
            BackColor = InputBorder,
            Location = new Point(0, top),
            Padding = new Padding(1),
            Size = new Size(width, 52)
        };

        var inner = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 18,
            FillColor = InputFill,
            StrokeColor = InputFill,
            Padding = new Padding(16, 14, 16, 14)
        };
        border.Controls.Add(inner);

        textBox.Location = new Point(16, 14);
        textBox.Size = new Size(width - 34, 24);
        textBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        inner.Controls.Add(textBox);

        void SetActive(bool active)
        {
            border.BackColor = active ? InputBorderActive : InputBorder;
        }

        textBox.Enter += (_, _) => SetActive(true);
        textBox.Leave += (_, _) => SetActive(false);
        inner.Enter += (_, _) => textBox.Focus();

        top += border.Height + 12;
        return border;
    }

    private static Label CreateLabel(string text, Font font, Color color, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = font,
            ForeColor = color,
            Location = new Point(0, top),
            Text = text
        };

        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static Label CreateWrappedLabel(string text, Font font, Color color, int width, ref int top, int bottomSpacing)
    {
        var label = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = font,
            ForeColor = color,
            MaximumSize = new Size(width, 0),
            Location = new Point(0, top),
            Text = text
        };

        top += label.PreferredHeight + bottomSpacing;
        return label;
    }

    private static Label CreateWrappedLabel(string text, Color color, int width, ref int top, int bottomSpacing)
    {
        return CreateWrappedLabel(text, new Font("Microsoft YaHei UI", 9F), color, width, ref top, bottomSpacing);
    }

    private static CheckBox CreateOptionCheckBox(string text)
    {
        return new CheckBox
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            CheckAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMuted,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 2),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
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
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
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
            BackColor = SecondaryButtonFill,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Text = text
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = CardBorder;
        button.FlatAppearance.MouseOverBackColor = SecondaryButtonHover;
        button.FlatAppearance.MouseDownBackColor = SecondaryButtonHover;
        return button;
    }

    private void UpdatePasswordVisibility()
    {
        var usePasswordChar = !_showPasswordCheckBox.Checked;
        _passwordTextBox.UseSystemPasswordChar = usePasswordChar;
        _confirmTextBox.UseSystemPasswordChar = usePasswordChar;
    }

    private void OnSubmitClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = _controller.Register(_accountTextBox.Text, _passwordTextBox.Text, _confirmTextBox.Text);
            if (!result.Success)
            {
                MessageBox.Show(this, result.Message, "注册失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(this, result.Message, "注册成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"注册过程中发生错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
}
