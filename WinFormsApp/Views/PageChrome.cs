using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace WinFormsApp.Views;

internal static class PageChrome
{
    internal static readonly Color PageBackground = Color.FromArgb(10, 10, 15);
    internal static readonly Color SurfaceBackground = Color.FromArgb(28, 30, 40);
    internal static readonly Color SurfaceRaised = Color.FromArgb(22, 24, 33);
    internal static readonly Color InputBackground = Color.FromArgb(18, 22, 30);
    internal static readonly Color SurfaceBorder = Color.FromArgb(80, 85, 110);
    internal static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    internal static readonly Color TextSecondary = Color.FromArgb(210, 215, 230);
    internal static readonly Color TextMuted = Color.FromArgb(160, 170, 190);
    internal static readonly Color AccentBlue = Color.FromArgb(88, 130, 255);
    internal static readonly Color AccentGreen = Color.FromArgb(76, 217, 140);
    internal static readonly Color AccentOrange = Color.FromArgb(255, 165, 70);
    internal static readonly Color AccentRed = Color.FromArgb(231, 76, 60);
    internal static readonly Color AccentPurple = Color.FromArgb(148, 90, 255);
    internal static readonly Color AccentPink = Color.FromArgb(255, 100, 150);
    internal static readonly Color AccentCyan = Color.FromArgb(50, 210, 220);

    internal static readonly Padding PagePadding = new(30, 20, 30, 20);
    internal static readonly Padding SectionMargin = new(0, 0, 0, 12);
    internal static readonly Padding SectionPadding = new(20, 16, 20, 20);
    internal static readonly Padding SectionHeaderPadding = new(20, 14, 20, 0);
    internal const int HeaderHeight = 112;
    internal const int GridHeaderHeight = 38;
    internal const int GridRowHeight = 38;

    internal class ChromePanel : Panel
    {
        public ChromePanel(int radius = 16)
        {
            Radius = radius;
            FillColor = SurfaceBackground;
            BorderColor = SurfaceBorder;
            BackColor = Color.Transparent;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }

        public int Radius { get; }

        public Color FillColor { get; init; }

        public Color BorderColor { get; init; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var parentBackColor = Parent?.BackColor ?? PageBackground;
            using var backgroundBrush = new SolidBrush(parentBackColor);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            using var fillBrush = new SolidBrush(FillColor);
            e.Graphics.FillPath(fillBrush, path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            using var borderPen = new Pen(BorderColor, 1F);
            e.Graphics.DrawPath(borderPen, path);
        }
    }

    internal sealed class ReadOnlyTextBlock : Panel
    {
        private readonly Label _label;

        public ReadOnlyTextBlock()
        {
            AutoScroll = true;
            BackColor = SurfaceBackground;
            Margin = Padding.Empty;
            Padding = new Padding(0);

            _label = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = TextSecondary,
                Margin = Padding.Empty
            };

            Controls.Add(_label);
            Resize += (_, _) => UpdateLabelBounds();
        }

        [AllowNull]
        public override string Text
        {
            get => _label.Text;
            set
            {
                _label.Text = value ?? string.Empty;
                UpdateLabelBounds();
            }
        }

        [AllowNull]
        public override Font Font
        {
            get => _label.Font;
            set
            {
                _label.Font = value ?? base.Font;
                UpdateLabelBounds();
            }
        }

        public Color TextColor
        {
            get => _label.ForeColor;
            set => _label.ForeColor = value;
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            UpdateLabelBounds();
        }

        private void UpdateLabelBounds()
        {
            if (!IsHandleCreated && Width <= 0)
            {
                return;
            }

            var availableWidth = Math.Max(80, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
            _label.MaximumSize = new Size(availableWidth, 0);
            _label.Location = new Point(Padding.Left, Padding.Top);
        }
    }

    private sealed class PageHeaderShell : ChromePanel
    {
        private readonly TableLayoutPanel _layout;
        private readonly TableLayoutPanel _titlePanel;
        private readonly FlowLayoutPanel _actionPanel;
        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Label _infoLabel;
        private int _lastLayoutWidth = -1;
        private int _lastHeight = -1;
        private bool _lastStacked;

        public PageHeaderShell(string title, string subtitle, Label infoLabel, IReadOnlyList<Control> actions)
            : base()
        {
            Dock = DockStyle.Top;
            Margin = SectionMargin;
            Padding = new Padding(20, 14, 20, 14);
            MinimumSize = new Size(0, HeaderHeight);
            FillColor = SurfaceBackground;
            BorderColor = SurfaceBorder;

            _layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _titleLabel = CreateTextLabel(title, 16F, FontStyle.Bold, TextPrimary, new Padding(0, 0, 0, 4));
            _subtitleLabel = CreateTextLabel(subtitle, 9F, FontStyle.Regular, TextSecondary, new Padding(0, 0, 0, 4));
            _infoLabel = infoLabel;
            _infoLabel.Margin = Padding.Empty;
            _infoLabel.Dock = DockStyle.Top;

            _titlePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _titlePanel.Controls.Add(_titleLabel, 0, 0);
            _titlePanel.Controls.Add(_subtitleLabel, 0, 1);
            _titlePanel.Controls.Add(_infoLabel, 0, 2);

            _actionPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = true
            };

            foreach (var action in actions)
            {
                action.Margin = new Padding(0, 0, 10, 10);
                _actionPanel.Controls.Add(action);
            }

            Controls.Add(_layout);
            Resize += (_, _) => UpdateLayoutMode();
            VisibleChanged += (_, _) => UpdateLayoutMode();
            EnsureLayoutStructure(false);
        }

        private void UpdateLayoutMode()
        {
            var outerWidth = ClientSize.Width > 0
                ? ClientSize.Width
                : Width;
            if (outerWidth <= 0)
            {
                return;
            }

            var preferredHeight = EnsureLayoutForWidth(outerWidth);
            if (_lastHeight != preferredHeight)
            {
                _lastHeight = preferredHeight;
                Height = preferredHeight;
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var outerWidth = proposedSize.Width > 0
                ? proposedSize.Width
                : Math.Max(Width, MinimumSize.Width);
            return new Size(outerWidth, EnsureLayoutForWidth(outerWidth));
        }

        private int EnsureLayoutForWidth(int outerWidth)
        {
            var rawWidth = Math.Max(1, outerWidth - Padding.Horizontal);
            var availableWidth = Math.Max(240, rawWidth);
            var actionWidth = _actionPanel.Controls.Count == 0 ? 0 : _actionPanel.GetPreferredSize(Size.Empty).Width;
            var stacked = _actionPanel.Controls.Count > 0 && availableWidth < Math.Max(620, actionWidth + 320);
            var textWidth = stacked
                ? availableWidth
                : Math.Max(240, availableWidth - actionWidth - 24);

            EnsureLayoutStructure(stacked);
            if (_lastLayoutWidth != availableWidth)
            {
                _lastLayoutWidth = availableWidth;
            }

            PrepareWrappingLabel(_titleLabel, textWidth);
            PrepareWrappingLabel(_subtitleLabel, textWidth);
            PrepareWrappingLabel(_infoLabel, textWidth);
            return Math.Max(
                HeaderHeight,
                _layout.GetPreferredSize(new Size(Math.Max(1, availableWidth), 0)).Height + Padding.Vertical);
        }

        private void EnsureLayoutStructure(bool stacked)
        {
            if (_lastStacked == stacked && _layout.Controls.Count > 0)
            {
                return;
            }

            _lastStacked = stacked;
            _layout.SuspendLayout();
            _layout.Controls.Clear();
            _layout.ColumnStyles.Clear();
            _layout.RowStyles.Clear();

            if (stacked)
            {
                _layout.ColumnCount = 1;
                _layout.RowCount = _actionPanel.Controls.Count == 0 ? 1 : 2;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                if (_actionPanel.Controls.Count > 0)
                {
                    _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _actionPanel.Dock = DockStyle.Top;
                    _actionPanel.FlowDirection = FlowDirection.LeftToRight;
                    _actionPanel.WrapContents = true;
                    _actionPanel.Margin = new Padding(0, 12, 0, 0);
                }

                _layout.Controls.Add(_titlePanel, 0, 0);
                if (_actionPanel.Controls.Count > 0)
                {
                    _layout.Controls.Add(_actionPanel, 0, 1);
                }
            }
            else
            {
                _layout.ColumnCount = _actionPanel.Controls.Count == 0 ? 1 : 2;
                _layout.RowCount = 1;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                if (_actionPanel.Controls.Count > 0)
                {
                    _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    _actionPanel.Dock = DockStyle.Fill;
                    _actionPanel.FlowDirection = FlowDirection.RightToLeft;
                    _actionPanel.WrapContents = false;
                    _actionPanel.Margin = Padding.Empty;
                }

                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.Controls.Add(_titlePanel, 0, 0);
                if (_actionPanel.Controls.Count > 0)
                {
                    _layout.Controls.Add(_actionPanel, 1, 0);
                }
            }

            _layout.ResumeLayout(true);
        }
    }

    private sealed class SectionShellPanel : ChromePanel
    {
        private readonly TableLayoutPanel _layout;
        private readonly TableLayoutPanel _headerLayout;
        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private int _lastHeaderWidth = -1;

        public SectionShellPanel(string title, Label subtitleLabel, Control body, Padding margin)
            : base()
        {
            Dock = DockStyle.Fill;
            Margin = margin;
            FillColor = SurfaceBackground;
            BorderColor = SurfaceBorder;

            _titleLabel = CreateTextLabel(title, 11F, FontStyle.Bold, TextPrimary, new Padding(0, 0, 0, 6));
            _subtitleLabel = subtitleLabel;
            _subtitleLabel.Dock = DockStyle.Top;

            _headerLayout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = SectionHeaderPadding
            };
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _headerLayout.Controls.Add(_titleLabel, 0, 0);
            _headerLayout.Controls.Add(_subtitleLabel, 0, 1);

            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(16, 0, 16, 16);

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _layout.Controls.Add(_headerLayout, 0, 0);
            _layout.Controls.Add(body, 0, 1);
            Controls.Add(_layout);

            Resize += (_, _) => UpdateWrapping();
            UpdateWrapping();
        }

        private void UpdateWrapping()
        {
            var headerWidth = Math.Max(160, ClientSize.Width - SectionHeaderPadding.Horizontal - 8);
            if (_lastHeaderWidth == headerWidth)
            {
                return;
            }

            _lastHeaderWidth = headerWidth;
            PrepareWrappingLabel(_titleLabel, headerWidth);
            PrepareWrappingLabel(_subtitleLabel, headerWidth);
        }
    }

    private sealed class MetricCardShell : ChromePanel
    {
        private readonly TableLayoutPanel _layout;
        private readonly Label _titleLabel;
        private readonly Label _valueLabel;
        private readonly Label _noteLabel;
        private readonly Color _accentColor;
        private int _lastContentWidth = -1;
        private bool _lastStacked;

        public MetricCardShell(string title, Color accentColor, Label valueLabel, Label noteLabel, Padding margin)
            : base()
        {
            Dock = DockStyle.Fill;
            Margin = margin;
            Padding = new Padding(18, 16, 18, 18);
            MinimumSize = new Size(0, 112);
            FillColor = SurfaceBackground;
            BorderColor = SurfaceBorder;
            _accentColor = accentColor;

            _titleLabel = CreateTextLabel(title, 9F, FontStyle.Regular, TextMuted, new Padding(0, 2, 0, 8));
            _valueLabel = valueLabel;
            _noteLabel = noteLabel;

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0)
            };

            Controls.Add(_layout);
            Paint += (_, e) =>
            {
                using var accentPen = new Pen(_accentColor, 2F);
                e.Graphics.DrawLine(accentPen, 18, 12, 60, 12);
            };

            Resize += (_, _) => UpdateLayoutMode();
            EnsureLayoutStructure(false);
            UpdateLayoutMode();
        }

        private void UpdateLayoutMode()
        {
            var contentWidth = Math.Max(160, ClientSize.Width - Padding.Horizontal);
            var stacked = contentWidth < 220;

            if (_lastContentWidth == contentWidth && _lastStacked == stacked)
            {
                return;
            }

            _lastContentWidth = contentWidth;
            _valueLabel.AutoSize = true;
            _noteLabel.AutoEllipsis = false;
            PrepareWrappingLabel(_noteLabel, Math.Max(140, contentWidth - 4));
            EnsureLayoutStructure(stacked);
        }

        private void EnsureLayoutStructure(bool stacked)
        {
            if (_lastStacked == stacked && _layout.Controls.Count > 0)
            {
                return;
            }

            _lastStacked = stacked;
            _layout.SuspendLayout();
            _layout.Controls.Clear();
            _layout.ColumnStyles.Clear();
            _layout.RowStyles.Clear();

            if (stacked)
            {
                _layout.ColumnCount = 1;
                _layout.RowCount = 3;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                _valueLabel.Dock = DockStyle.Top;
                _valueLabel.Margin = Padding.Empty;
                _valueLabel.TextAlign = ContentAlignment.MiddleLeft;
                _noteLabel.Dock = DockStyle.Top;
                _noteLabel.Margin = new Padding(0, 8, 0, 0);

                _layout.Controls.Add(_titleLabel, 0, 0);
                _layout.Controls.Add(_valueLabel, 0, 1);
                _layout.Controls.Add(_noteLabel, 0, 2);
            }
            else
            {
                _layout.ColumnCount = 2;
                _layout.RowCount = 2;
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                _valueLabel.Dock = DockStyle.Right;
                _valueLabel.Margin = new Padding(16, 0, 0, 0);
                _valueLabel.TextAlign = ContentAlignment.MiddleRight;
                _noteLabel.Dock = DockStyle.Top;
                _noteLabel.Margin = new Padding(0, 8, 0, 0);

                _layout.Controls.Add(_titleLabel, 0, 0);
                _layout.Controls.Add(_valueLabel, 1, 0);
                _layout.Controls.Add(_noteLabel, 0, 1);
                _layout.SetColumnSpan(_noteLabel, 2);
            }

            _layout.ResumeLayout(true);
        }
    }

    internal static ChromePanel CreateSurfacePanel(
        Padding padding,
        int radius = 16,
        Color? fillColor = null,
        Color? borderColor = null)
    {
        return new ChromePanel(radius)
        {
            Dock = DockStyle.Fill,
            Padding = padding,
            FillColor = fillColor ?? SurfaceBackground,
            BorderColor = borderColor ?? SurfaceBorder
        };
    }

    internal static Control CreatePageHeader(string title, string subtitle, Label infoLabel, params Control[] actions)
    {
        return new PageHeaderShell(title, subtitle, infoLabel, actions);
    }

    internal static void BindControlHeightToRow(TableLayoutPanel layout, int rowIndex, Control control)
    {
        var lastHeight = -1;

        void SyncRowHeight(object? sender, EventArgs e)
        {
            if (rowIndex < 0 || rowIndex >= layout.RowStyles.Count)
            {
                return;
            }

            var targetHeight = Math.Max(HeaderHeight, control.Height + control.Margin.Vertical);
            if (lastHeight == targetHeight)
            {
                return;
            }

            lastHeight = targetHeight;
            var style = layout.RowStyles[rowIndex];
            style.SizeType = SizeType.Absolute;
            style.Height = targetHeight;
            layout.PerformLayout();
        }

        control.SizeChanged += SyncRowHeight;
        control.VisibleChanged += SyncRowHeight;
        SyncRowHeight(null, EventArgs.Empty);
    }

    internal static ChromePanel CreateSectionShell(string title, string subtitle, out Label subtitleLabel, Control body, Padding? margin = null)
    {
        subtitleLabel = CreateTextLabel(subtitle, 8.8F, FontStyle.Regular, TextMuted, new Padding(0, 0, 0, 8));
        return CreateSectionShell(title, subtitleLabel, body, margin);
    }

    internal static ChromePanel CreateSectionShell(string title, Label subtitleLabel, Control body, Padding? margin = null)
    {
        return new SectionShellPanel(title, subtitleLabel, body, margin ?? SectionMargin);
    }

    internal static ChromePanel CreateMetricCard(string title, Color accentColor, Label valueLabel, Label noteLabel, Padding? margin = null)
    {
        return new MetricCardShell(title, accentColor, valueLabel, noteLabel, margin ?? new Padding(0, 0, 12, 0));
    }

    internal static ReadOnlyTextBlock CreateReadOnlyTextBlock()
    {
        return new ReadOnlyTextBlock
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Font = new Font("Microsoft YaHei UI", 9F)
        };
    }

    internal static Button CreateActionButton(string text, Color accent, bool filled)
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = Padding.Empty,
            Padding = new Padding(14, 8, 14, 8),
            Text = text,
            UseVisualStyleBackColor = false
        };

        ApplyButtonStyle(button, accent, filled);
        return button;
    }

    internal static void ApplyButtonStyle(Button button, Color accent, bool filled)
    {
        button.ForeColor = TextPrimary;
        button.BackColor = filled ? accent : SurfaceRaised;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(filled ? 160 : 88, accent);
        button.FlatAppearance.MouseOverBackColor = filled
            ? MixColor(accent, Color.White, 0.12f)
            : Color.FromArgb(36, accent);
        button.FlatAppearance.MouseDownBackColor = filled
            ? MixColor(accent, Color.Black, 0.12f)
            : Color.FromArgb(58, accent);
    }

    internal static Label CreateInfoLabel(string text = "")
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 8.8F),
            ForeColor = TextMuted,
            Margin = Padding.Empty,
            Text = text
        };
    }

    internal static Label CreateValueLabel(float size = 18F, string text = "--")
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", size, FontStyle.Bold),
            ForeColor = TextPrimary,
            Margin = Padding.Empty,
            Text = text
        };
    }

    internal static Label CreateNoteLabel(string text = "", float size = 8.8F, Color? color = null)
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", size),
            ForeColor = color ?? TextMuted,
            Margin = Padding.Empty,
            Text = text
        };
    }

    internal static Label CreateTextLabel(string text, float size, FontStyle style, Color color, Padding margin)
    {
        return new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", size, style),
            ForeColor = color,
            Margin = margin,
            Text = text
        };
    }

    internal static Label CreateEmptyStateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextMuted,
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
    }

    internal static void ApplyGridTheme(DataGridView grid, bool alternateRows = true, Color? selectionColor = null)
    {
        var selectedBackColor = selectionColor ?? Color.FromArgb(48, AccentBlue);
        grid.BackgroundColor = SurfaceBackground;
        grid.GridColor = Color.FromArgb(54, 60, 78);
        grid.DefaultCellStyle.BackColor = SurfaceBackground;
        grid.DefaultCellStyle.ForeColor = TextSecondary;
        grid.DefaultCellStyle.SelectionBackColor = selectedBackColor;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(8, 2, 8, 2);
        grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F);
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersHeight = GridHeaderHeight;
        grid.RowTemplate.Height = GridRowHeight;
        if (alternateRows)
        {
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 26, 36);
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextSecondary;
            grid.AlternatingRowsDefaultCellStyle.Padding = new Padding(8, 2, 8, 2);
        }
    }

    internal static Color MixColor(Color baseColor, Color mixColor, float amount)
    {
        var ratio = Math.Clamp(amount, 0F, 1F);
        var red = (int)(baseColor.R + ((mixColor.R - baseColor.R) * ratio));
        var green = (int)(baseColor.G + ((mixColor.G - baseColor.G) * ratio));
        var blue = (int)(baseColor.B + ((mixColor.B - baseColor.B) * ratio));
        return Color.FromArgb(red, green, blue);
    }

    internal static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
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

    private static void PrepareWrappingLabel(Label label, int availableWidth)
    {
        var wrappedWidth = Math.Max(120, availableWidth);
        if (label.MaximumSize.Width == wrappedWidth &&
            label.MaximumSize.Height == 0 &&
            label.AutoSize &&
            !label.AutoEllipsis)
        {
            return;
        }

        label.AutoEllipsis = false;
        label.AutoSize = true;
        label.MaximumSize = new Size(wrappedWidth, 0);
    }
}
