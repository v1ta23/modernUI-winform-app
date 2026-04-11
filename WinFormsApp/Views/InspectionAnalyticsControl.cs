using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace WinFormsApp.Views;

internal sealed class InspectionAnalyticsControl : UserControl, IInteractiveResizeAware
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color InputBackground = PageChrome.InputBackground;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color SuccessColor = PageChrome.AccentGreen;
    private static readonly Color WarningColor = PageChrome.AccentOrange;
    private static readonly Color DangerColor = PageChrome.AccentRed;
    private static readonly Color PendingColor = PageChrome.AccentPurple;

    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private readonly TableLayoutPanel _rootLayout;
    private readonly Control _headerShell;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly BufferedPanel _trendChartPanel;
    private readonly BufferedPanel _statusChartPanel;
    private readonly DataGridView _lineSummaryGrid;
    private readonly DataGridView _issueGrid;
    private readonly PageChrome.ReadOnlyTextBlock _summaryTextBlock;
    private readonly Label _decisionValueLabel;
    private readonly Label _decisionNoteLabel;
    private Label _decisionSubtitleLabel = null!;
    private Label _trendSubtitleLabel = null!;
    private Label _statusSubtitleLabel = null!;
    private Label _lineSubtitleLabel = null!;
    private Label _issueSubtitleLabel = null!;
    private Label _summarySubtitleLabel = null!;
    private readonly SummaryHintRow _summaryRiskHintRow;
    private readonly SummaryHintRow _summaryLineHintRow;
    private readonly SummaryHintRow _summaryActionHintRow;

    private InspectionDashboardViewModel _currentDashboard = new();
    private IReadOnlyList<LineSummaryRow> _lineRows = Array.Empty<LineSummaryRow>();
    private IReadOnlyList<AttentionRow> _attentionRows = Array.Empty<AttentionRow>();

    public InspectionAnalyticsControl(InspectionController inspectionController)
    {
        _inspectionController = inspectionController;

        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        _decisionValueLabel = PageChrome.CreateValueLabel(14F, "--");
        _decisionValueLabel.Dock = DockStyle.Top;
        _decisionValueLabel.Margin = Padding.Empty;
        _decisionNoteLabel = PageChrome.CreateNoteLabel();
        _decisionNoteLabel.Dock = DockStyle.Top;
        _decisionNoteLabel.Margin = new Padding(0, 8, 0, 0);

        _trendChartPanel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = Padding.Empty
        };
        _trendChartPanel.Paint += DrawTrendChart;

        _statusChartPanel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = Padding.Empty
        };
        _statusChartPanel.Paint += DrawStatusChart;

        _lineSummaryGrid = CreateLineSummaryGrid();
        _issueGrid = CreateIssueGrid();
        _summaryTextBlock = PageChrome.CreateReadOnlyTextBlock();
        _summaryTextBlock.Font = new Font("Microsoft YaHei UI", 8.8F);

        _summaryRiskHintRow = new SummaryHintRow("风险级别", DangerColor);
        _summaryLineHintRow = new SummaryHintRow("重点产线", WarningColor);
        _summaryActionHintRow = new SummaryHintRow("下一步", AccentBlue);

        var refreshButton = PageChrome.CreateActionButton("刷新数据", AccentBlue, true);
        refreshButton.Click += (_, _) => RefreshData();

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 5
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 37F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 27F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));

        _headerShell = BuildHeader(refreshButton);
        PageChrome.BindControlHeightToRow(_rootLayout, 0, _headerShell);

        _rootLayout.Controls.Add(_headerShell, 0, 0);
        _rootLayout.Controls.Add(BuildDecisionBar(), 0, 1);
        _rootLayout.Controls.Add(BuildPrimaryRow(), 0, 2);
        _rootLayout.Controls.Add(BuildSecondaryRow(), 0, 3);
        _rootLayout.Controls.Add(BuildSummaryRow(), 0, 4);

        Controls.Add(_rootLayout);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _rootLayout, PageBackground);
        _rootLayout.BringToFront();
        VisibleChanged += (_, _) => QueueVisibleLayoutPass();
        ApplyTheme();
        RefreshData();
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        _trendChartPanel.BackColor = SurfaceBackground;
        _statusChartPanel.BackColor = SurfaceBackground;
        PageChrome.ApplyGridTheme(_lineSummaryGrid);
        PageChrome.ApplyGridTheme(_issueGrid);
        Invalidate(true);
    }

    public void BeginInteractiveResize()
    {
        _interactiveResizeController.Begin();
    }

    public void EndInteractiveResize()
    {
        if (!_interactiveResizeController.IsActive)
        {
            return;
        }

        _interactiveResizeController.End();
        _headerShell.PerformLayout();
        _rootLayout.PerformLayout();
        PerformLayout();
        _trendChartPanel.Invalidate();
        _statusChartPanel.Invalidate();
        Invalidate(true);
        Update();
    }

    public void RefreshData()
    {
        _currentDashboard = _inspectionController.Load(new InspectionFilterViewModel());
        _lineRows = BuildLineRows(_currentDashboard.Records);
        _attentionRows = BuildAttentionRows(_currentDashboard.Records);

        var pendingRows = _currentDashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal && !record.IsClosed)
            .OrderByDescending(record => record.CheckedAtValue)
            .ToList();
        var affectedDeviceCount = _currentDashboard.Records
            .Where(record => record.Status != InspectionStatus.Normal)
            .Select(record => $"{record.LineName}|{record.DeviceName}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var highRiskLine = _lineRows.FirstOrDefault(row => row.AbnormalCount > 0)
            ?? _lineRows.FirstOrDefault(row => row.WarningCount > 0);

        _generatedAtLabel.Text = $"更新时间：{_currentDashboard.GeneratedAt:yyyy-MM-dd HH:mm}";
        _trendSubtitleLabel.Text = BuildTrendSubtitle(_currentDashboard.TrendPoints);
        _statusSubtitleLabel.Text = $"正常 {_currentDashboard.NormalCount} / 预警 {_currentDashboard.WarningCount} / 异常 {_currentDashboard.AbnormalCount}";
        _lineSubtitleLabel.Text = _lineRows.Count == 0
            ? "当前没有产线统计结果。"
            : $"按风险优先级排序，共 {_lineRows.Count} 条产线。";
        _issueSubtitleLabel.Text = _attentionRows.Count == 0
            ? "当前没有预警和异常记录。"
            : $"最近 {_attentionRows.Count} 条需关注记录。";

        _decisionSubtitleLabel.Text = BuildDecisionSubtitle(pendingRows.Count, highRiskLine);
        _decisionValueLabel.Text = BuildDecisionTitle(pendingRows.Count, highRiskLine);
        _decisionNoteLabel.Text = BuildDecisionNote(pendingRows.Count, affectedDeviceCount, highRiskLine);

        _summarySubtitleLabel.Text = BuildSummarySubtitle(pendingRows.Count, highRiskLine);
        _summaryTextBlock.Text = BuildSummaryText(pendingRows.Count, affectedDeviceCount, highRiskLine);

        _summaryRiskHintRow.ValueText = BuildRiskLevelText(pendingRows.Count, highRiskLine);
        _summaryRiskHintRow.NoteText = pendingRows.Count == 0
            ? "当前没有待闭环问题。"
            : $"待闭环 {pendingRows.Count} 条，异常优先。";

        _summaryLineHintRow.ValueText = highRiskLine?.LineName ?? "暂无重点产线";
        _summaryLineHintRow.NoteText = highRiskLine is null
            ? "先看整体趋势和最近关注项。"
            : $"异常 {highRiskLine.AbnormalCount} 条 / 预警 {highRiskLine.WarningCount} 条。";

        _summaryActionHintRow.ValueText = BuildActionTitle(pendingRows.Count, highRiskLine);
        _summaryActionHintRow.NoteText = pendingRows.Count > 0
            ? "建议先去报警中心或巡检页处理。"
            : "当前更适合复盘趋势和产线稳定性。";

        _lineSummaryGrid.DataSource = _lineRows.ToList();
        _issueGrid.DataSource = _attentionRows.ToList();

        _trendChartPanel.Invalidate();
        _statusChartPanel.Invalidate();
    }

    private void QueueVisibleLayoutPass()
    {
        if (!Visible || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new MethodInvoker(() =>
        {
            if (IsDisposed || !Visible)
            {
                return;
            }

            _headerShell.PerformLayout();
            _rootLayout.PerformLayout();
            PerformLayout();
            Invalidate(true);
            Update();
        }));
    }

    private Control BuildHeader(Button refreshButton)
    {
        return PageChrome.CreatePageHeader(
            "统计分析",
            "把关键判断、趋势和关注项放稳，不再用花哨结构挤内容。",
            _generatedAtLabel,
            refreshButton);
    }

    private Control BuildDecisionBar()
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.Controls.Add(_decisionValueLabel, 0, 0);
        content.Controls.Add(_decisionNoteLabel, 0, 1);

        return PageChrome.CreateSectionShell(
            "核心判断",
            "系统先给一句结论，再决定你先看哪个区块。",
            out _decisionSubtitleLabel,
            content,
            new Padding(0, 0, 0, 12));
    }

    private Control BuildPrimaryRow()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(PageChrome.CreateSectionShell(
            "趋势变化",
            "最近 8 个时间桶的状态变化。",
            out _trendSubtitleLabel,
            _trendChartPanel,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(PageChrome.CreateSectionShell(
            "状态占比",
            "按当前筛选结果汇总。",
            out _statusSubtitleLabel,
            _statusChartPanel,
            Padding.Empty), 1, 0);
        return layout;
    }

    private Control BuildSecondaryRow()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(PageChrome.CreateSectionShell(
            "产线汇总",
            "按风险优先级排，先看最需要处理的产线。",
            out _lineSubtitleLabel,
            _lineSummaryGrid,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(PageChrome.CreateSectionShell(
            "最近关注项",
            "只放最近需要立刻看到的记录。",
            out _issueSubtitleLabel,
            _issueGrid,
            Padding.Empty), 1, 0);
        return layout;
    }

    private Control BuildSummaryRow()
    {
        var summaryBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        summaryBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        summaryBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        summaryBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var textShell = PageChrome.CreateSurfacePanel(
            new Padding(14),
            14,
            fillColor: InputBackground,
            borderColor: Color.FromArgb(70, SurfaceBorder));
        textShell.Margin = new Padding(0, 0, 12, 0);
        _summaryTextBlock.Padding = new Padding(0);
        textShell.Controls.Add(_summaryTextBlock);

        var hintLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        hintLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        hintLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
        hintLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        hintLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        _summaryRiskHintRow.Margin = new Padding(0, 0, 0, 8);
        _summaryLineHintRow.Margin = new Padding(0, 0, 0, 8);
        _summaryActionHintRow.Margin = Padding.Empty;
        hintLayout.Controls.Add(_summaryRiskHintRow, 0, 0);
        hintLayout.Controls.Add(_summaryLineHintRow, 0, 1);
        hintLayout.Controls.Add(_summaryActionHintRow, 0, 2);

        summaryBody.Controls.Add(textShell, 0, 0);
        summaryBody.Controls.Add(hintLayout, 1, 0);

        return PageChrome.CreateSectionShell(
            "AI 摘要",
            "默认先给一句结论，再告诉你下一步看哪里。",
            out _summarySubtitleLabel,
            summaryBody,
            Padding.Empty);
    }

    private sealed class SummaryHintRow : Control
    {
        private readonly Font _titleFont = new("Microsoft YaHei UI", 8.8F, FontStyle.Regular);
        private readonly Font _valueFont = new("Microsoft YaHei UI", 11.6F, FontStyle.Bold);
        private readonly Font _noteFont = new("Microsoft YaHei UI", 8.8F, FontStyle.Regular);
        private readonly Color _fillColor;
        private readonly Color _borderColor;
        private string _valueText = "--";
        private string _noteText = string.Empty;

        public SummaryHintRow(string title, Color accentColor)
        {
            Title = title;
            _fillColor = MixColor(InputBackground, accentColor, 0.14F);
            _borderColor = MixColor(SurfaceBorder, accentColor, 0.48F);
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = new Padding(12, 6, 12, 6);
            BackColor = SurfaceBackground;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public string Title { get; }

        public string ValueText
        {
            get => _valueText;
            set
            {
                var next = value ?? string.Empty;
                if (_valueText == next)
                {
                    return;
                }

                _valueText = next;
                Invalidate();
            }
        }

        public string NoteText
        {
            get => _noteText;
            set
            {
                var next = value ?? string.Empty;
                if (_noteText == next)
                {
                    return;
                }

                _noteText = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var outerRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = PageChrome.CreateRoundedPath(outerRect, 14))
            using (var fillBrush = new SolidBrush(_fillColor))
            using (var borderPen = new Pen(_borderColor))
            {
                e.Graphics.FillPath(fillBrush, path);
                e.Graphics.DrawPath(borderPen, path);
            }

            var contentBounds = new Rectangle(
                Padding.Left,
                Padding.Top,
                Math.Max(0, Width - Padding.Horizontal),
                Math.Max(0, Height - Padding.Vertical));

            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            {
                return;
            }

            const TextFormatFlags singleLineFlags =
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsTranslateTransform;

            const TextFormatFlags multiLineFlags =
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.PreserveGraphicsTranslateTransform;

            var titleHeight = Math.Max(14, _titleFont.Height);
            var valueHeight = Math.Max(20, _valueFont.Height);
            var noteHeight = Math.Max(14, _noteFont.Height * 2);
            var noteGap = string.IsNullOrWhiteSpace(_noteText) ? 0 : 3;
            var compactMode = contentBounds.Height < titleHeight + valueHeight + 4;
            var noteFits = !string.IsNullOrWhiteSpace(_noteText)
                && !compactMode
                && contentBounds.Height >= titleHeight + valueHeight + noteHeight + noteGap;

            if (compactMode)
            {
                var measuredTitleWidth = TextRenderer.MeasureText(
                    e.Graphics,
                    Title,
                    _titleFont,
                    new Size(contentBounds.Width, contentBounds.Height),
                    singleLineFlags).Width;
                var titleWidth = Math.Min(
                    Math.Max(72, measuredTitleWidth + 8),
                    Math.Max(72, contentBounds.Width / 3));
                var valueLeft = Math.Min(contentBounds.Right, contentBounds.X + titleWidth + 8);
                var compactTitleBounds = new Rectangle(contentBounds.X, contentBounds.Y, titleWidth, contentBounds.Height);
                var compactValueBounds = new Rectangle(
                    valueLeft,
                    contentBounds.Y,
                    Math.Max(0, contentBounds.Right - valueLeft),
                    contentBounds.Height);

                TextRenderer.DrawText(e.Graphics, Title, _titleFont, compactTitleBounds, TextMutedColor, singleLineFlags);
                TextRenderer.DrawText(e.Graphics, ValueText, _valueFont, compactValueBounds, TextPrimaryColor, singleLineFlags);
                return;
            }

            var titleBounds = new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width, titleHeight);
            var valueTop = titleBounds.Bottom;
            var valueAvailableHeight = noteFits
                ? contentBounds.Bottom - noteGap - noteHeight - valueTop
                : contentBounds.Bottom - valueTop;
            var valueBounds = new Rectangle(
                contentBounds.X,
                valueTop,
                contentBounds.Width,
                Math.Max(0, valueAvailableHeight));

            TextRenderer.DrawText(e.Graphics, Title, _titleFont, titleBounds, TextMutedColor, singleLineFlags);
            TextRenderer.DrawText(e.Graphics, ValueText, _valueFont, valueBounds, TextPrimaryColor, singleLineFlags);

            if (!noteFits)
            {
                return;
            }

            var noteBounds = new Rectangle(
                contentBounds.X,
                contentBounds.Bottom - noteHeight,
                contentBounds.Width,
                noteHeight);
            TextRenderer.DrawText(e.Graphics, NoteText, _noteFont, noteBounds, TextMutedColor, multiLineFlags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _valueFont.Dispose();
                _noteFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var backgroundBrush = new SolidBrush(SurfaceBackground);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
        }

        private static Color MixColor(Color baseColor, Color overlayColor, float overlayWeight)
        {
            var weight = Math.Clamp(overlayWeight, 0F, 1F);
            var baseWeight = 1F - weight;
            return Color.FromArgb(
                (int)Math.Round(baseColor.R * baseWeight + overlayColor.R * weight),
                (int)Math.Round(baseColor.G * baseWeight + overlayColor.G * weight),
                (int)Math.Round(baseColor.B * baseWeight + overlayColor.B * weight));
        }
    }

    private static DataGridView CreateLineSummaryGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.LineName), "产线", 120F, 96));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.TotalCount), "总数", 68F, 60));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.NormalCount), "正常", 68F, 60));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.WarningCount), "预警", 68F, 60));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.AbnormalCount), "异常", 68F, 60));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.PassRateText), "合格率", 78F, 70));
        return grid;
    }

    private static DataGridView CreateIssueGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.CheckedAt), "时间", 96F, 92));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.TargetName), "设备", 150F, 120));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.StatusText), "状态", 64F, 60));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.Remark), "说明", 210F, 160));
        AttachStatusColoring(grid, nameof(AttentionRow.StatusText));
        return grid;
    }

    private static DataGridView CreateGrid()
    {
        var grid = new BufferedDataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        PageChrome.ApplyGridTheme(grid);
        return grid;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string title, float fillWeight, int minimumWidth)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = title,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            MinimumWidth = minimumWidth
        };
    }

    private static void AttachStatusColoring(DataGridView grid, string statusPropertyName)
    {
        grid.CellFormatting += (_, args) =>
        {
            if (args.Value is not string text)
            {
                return;
            }

            if (grid.Columns[args.ColumnIndex].DataPropertyName != statusPropertyName)
            {
                return;
            }

            var cellStyle = args.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "正常" => SuccessColor,
                "预警" => WarningColor,
                "异常" => DangerColor,
                _ => TextPrimaryColor
            };
        };
    }

    private void DrawTrendChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(SurfaceBackground);

        var points = _currentDashboard.TrendPoints;
        var bounds = new Rectangle(18, 12, Math.Max(0, _trendChartPanel.Width - 36), Math.Max(0, _trendChartPanel.Height - 24));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);
        using var labelBrush = new SolidBrush(TextMutedColor);
        if (points.Count == 0)
        {
            DrawCenteredText(g, bounds, "暂无趋势数据", labelFont, labelBrush);
            return;
        }

        var plotRect = new Rectangle(bounds.X + 34, bounds.Y + 20, bounds.Width - 54, bounds.Height - 62);
        if (plotRect.Width <= 20 || plotRect.Height <= 20)
        {
            return;
        }

        var maxValue = Math.Max(1, points.Max(point => Math.Max(point.NormalCount, Math.Max(point.WarningCount, point.AbnormalCount))));
        using var axisPen = new Pen(SurfaceBorder, 1F);
        using var gridPen = new Pen(Color.FromArgb(55, 62, 80), 1F);

        for (var index = 0; index <= 4; index++)
        {
            var y = plotRect.Bottom - plotRect.Height * index / 4F;
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = Math.Round(maxValue * index / 4F).ToString("0");
            g.DrawString(label, labelFont, labelBrush, bounds.Left, y - 8);
        }

        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);

        DrawTrendSeries(g, plotRect, points, maxValue, point => point.NormalCount, SuccessColor);
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.WarningCount, WarningColor);
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.AbnormalCount, DangerColor);

        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + plotRect.Width * index / Math.Max(1F, points.Count - 1F);
            var label = points[index].Label;
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, labelBrush, x - size.Width / 2F, plotRect.Bottom + 10);
        }

        DrawLegend(g, new Point(plotRect.Right - 158, bounds.Top));
    }

    private void DrawStatusChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(SurfaceBackground);

        var counts = new[]
        {
            ("正常", _currentDashboard.NormalCount, SuccessColor),
            ("预警", _currentDashboard.WarningCount, WarningColor),
            ("异常", _currentDashboard.AbnormalCount, DangerColor)
        };

        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);
        using var labelBrush = new SolidBrush(TextMutedColor);

        var total = counts.Sum(item => item.Item2);
        if (total == 0)
        {
            DrawCenteredText(g, new Rectangle(0, 0, _statusChartPanel.Width, _statusChartPanel.Height), "暂无状态统计", labelFont, labelBrush);
            return;
        }

        var contentRect = new Rectangle(14, 10, Math.Max(0, _statusChartPanel.Width - 28), Math.Max(0, _statusChartPanel.Height - 20));
        if (contentRect.Width <= 0 || contentRect.Height <= 0)
        {
            return;
        }

        var compactLegend = contentRect.Width < 360;
        var legendHeight = compactLegend ? counts.Length * 26 : 52;
        var diameter = Math.Min(contentRect.Height - legendHeight - 16, contentRect.Width - 24);
        diameter = Math.Max(100, diameter);
        var donutRect = new Rectangle(
            contentRect.Left + (contentRect.Width - diameter) / 2,
            contentRect.Top,
            diameter,
            diameter);

        var ringWidth = diameter >= 150 ? 22F : 18F;
        using var backPen = new Pen(Color.FromArgb(55, 62, 80), ringWidth);
        g.DrawArc(backPen, donutRect, 0, 360);

        var startAngle = -90F;
        foreach (var (_, value, color) in counts)
        {
            if (value == 0)
            {
                continue;
            }

            var sweepAngle = 360F * value / total;
            using var pen = new Pen(color, ringWidth);
            g.DrawArc(pen, donutRect, startAngle, sweepAngle);
            startAngle += sweepAngle;
        }

        using var valueFont = new Font("Segoe UI", 18F, FontStyle.Bold);
        using var totalBrush = new SolidBrush(TextPrimaryColor);
        var totalText = total.ToString();
        var totalSize = g.MeasureString(totalText, valueFont);
        g.DrawString(totalText, valueFont, totalBrush,
            donutRect.Left + donutRect.Width / 2F - totalSize.Width / 2F,
            donutRect.Top + donutRect.Height / 2F - 24F);
        g.DrawString("总记录", labelFont, labelBrush,
            donutRect.Left + donutRect.Width / 2F - 20F,
            donutRect.Top + donutRect.Height / 2F + 6F);

        var legendArea = new Rectangle(
            contentRect.Left,
            donutRect.Bottom + 10,
            contentRect.Width,
            Math.Max(0, contentRect.Bottom - donutRect.Bottom - 10));
        using var legendTitleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        using var legendValueFont = new Font("Microsoft YaHei UI", 9F);
        if (compactLegend)
        {
            var legendRowY = legendArea.Top;
            foreach (var (name, value, color) in counts)
            {
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, legendArea.Left, legendRowY + 8, 10, 10);
                g.DrawString(name, legendTitleFont, totalBrush, legendArea.Left + 16, legendRowY);
                g.DrawString($"{value} / {value * 100F / total:0.0}%", legendValueFont, labelBrush, legendArea.Left + 90, legendRowY + 1);
                legendRowY += 26;
            }
        }
        else
        {
            var gap = 10;
            var itemWidth = Math.Max(96, (legendArea.Width - gap * (counts.Length - 1)) / counts.Length);
            var itemHeight = Math.Min(legendArea.Height, 42);
            var itemTop = legendArea.Top + Math.Max(0, (legendArea.Height - itemHeight) / 2);

            for (var index = 0; index < counts.Length; index++)
            {
                var (name, value, color) = counts[index];
                var itemLeft = legendArea.Left + index * (itemWidth + gap);
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, itemLeft, itemTop + 8, 10, 10);
                g.DrawString(name, legendTitleFont, totalBrush, itemLeft + 16, itemTop);
                g.DrawString($"{value} / {value * 100F / total:0.0}%", legendValueFont, labelBrush, itemLeft + 16, itemTop + 20);
            }
        }
    }

    private static void DrawTrendSeries(
        Graphics graphics,
        Rectangle plotRect,
        IReadOnlyList<InspectionTrendPointViewModel> points,
        int maxValue,
        Func<InspectionTrendPointViewModel, int> selector,
        Color color)
    {
        using var pen = new Pen(color, 2.4F);
        using var brush = new SolidBrush(color);

        var positions = new List<PointF>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + plotRect.Width * index / Math.Max(1F, points.Count - 1F);
            var ratio = selector(points[index]) / (float)maxValue;
            var y = plotRect.Bottom - plotRect.Height * ratio;
            positions.Add(new PointF(x, y));
        }

        if (positions.Count > 1)
        {
            graphics.DrawLines(pen, positions.ToArray());
        }

        foreach (var position in positions)
        {
            graphics.FillEllipse(brush, position.X - 3.5F, position.Y - 3.5F, 7, 7);
        }
    }

    private static void DrawLegend(Graphics graphics, Point origin)
    {
        DrawLegendItem(graphics, origin, "正常", SuccessColor);
        DrawLegendItem(graphics, new Point(origin.X + 54, origin.Y), "预警", WarningColor);
        DrawLegendItem(graphics, new Point(origin.X + 108, origin.Y), "异常", DangerColor);
    }

    private static void DrawLegendItem(Graphics graphics, Point origin, string text, Color color)
    {
        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(TextMutedColor);
        using var font = new Font("Microsoft YaHei UI", 8.5F);
        graphics.FillEllipse(brush, origin.X, origin.Y + 4, 8, 8);
        graphics.DrawString(text, font, textBrush, origin.X + 12, origin.Y);
    }

    private static void DrawCenteredText(Graphics graphics, Rectangle bounds, string text, Font font, Brush brush)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(
            text,
            font,
            brush,
            bounds.Left + (bounds.Width - size.Width) / 2F,
            bounds.Top + (bounds.Height - size.Height) / 2F);
    }

    private static string BuildTrendSubtitle(IReadOnlyList<InspectionTrendPointViewModel> points)
    {
        if (points.Count == 0)
        {
            return "当前没有趋势数据。";
        }

        return $"从 {points.First().Label} 到 {points.Last().Label} 的状态变化。";
    }

    private string BuildDecisionSubtitle(int pendingCount, LineSummaryRow? highRiskLine)
    {
        if (_currentDashboard.AbnormalCount > 0)
        {
            return highRiskLine is null
                ? "当前存在异常，建议先看最近关注项。"
                : $"当前异常主要集中在 {highRiskLine.LineName}。";
        }

        if (pendingCount > 0)
        {
            return "当前没有异常压顶，但还有待闭环问题。";
        }

        if (_currentDashboard.WarningCount > 0)
        {
            return "当前以预警为主，建议做一次快速复核。";
        }

        return "当前整体平稳，可以把重点放在趋势复盘。";
    }

    private string BuildDecisionTitle(int pendingCount, LineSummaryRow? highRiskLine)
    {
        if (_currentDashboard.AbnormalCount > 0)
        {
            return $"结论：{highRiskLine?.LineName ?? "当前产线"} 风险偏高，异常需要优先处理。";
        }

        if (pendingCount > 0)
        {
            return "结论：当前有待闭环问题，先把未处理项收干净。";
        }

        if (_currentDashboard.WarningCount > 0)
        {
            return "结论：当前以预警为主，先复核重点设备。";
        }

        return "结论：当前巡检状态平稳，可以继续按节奏复盘。";
    }

    private string BuildDecisionNote(int pendingCount, int affectedDeviceCount, LineSummaryRow? highRiskLine)
    {
        if (_lineRows.Count == 0)
        {
            return "还没有可统计的数据，先补充巡检记录。";
        }

        if (pendingCount > 0)
        {
            return $"当前覆盖 {_lineRows.Count} 条产线，涉及 {affectedDeviceCount} 台问题设备。优先从 {highRiskLine?.LineName ?? "重点产线"} 开始。";
        }

        return $"当前覆盖 {_lineRows.Count} 条产线，涉及 {affectedDeviceCount} 台问题设备，先看趋势是否持续收敛。";
    }

    private string BuildSummarySubtitle(int pendingCount, LineSummaryRow? highRiskLine)
    {
        if (_currentDashboard.AbnormalCount > 0)
        {
            return highRiskLine is null
                ? "系统判断当前风险偏高。"
                : $"系统判断 {highRiskLine.LineName} 是当前最该先看的产线。";
        }

        if (pendingCount > 0)
        {
            return "系统判断当前先闭环，再回头看趋势。";
        }

        return "系统判断当前风险可控，适合做复盘。";
    }

    private string BuildSummaryText(int pendingCount, int affectedDeviceCount, LineSummaryRow? highRiskLine)
    {
        var overview = $"本次共 {_currentDashboard.TotalCount} 条记录，合格率 {_currentDashboard.PassRateText}。正常 {_currentDashboard.NormalCount} / 预警 {_currentDashboard.WarningCount} / 异常 {_currentDashboard.AbnormalCount}。";
        var focus = _lineRows.Count == 0
            ? "当前还没有产线统计结果，先补足巡检数据。"
            : highRiskLine is null
                ? $"当前覆盖 {_lineRows.Count} 条产线，涉及 {affectedDeviceCount} 台问题设备，暂时没有单一产线明显压过其他产线。"
                : $"当前覆盖 {_lineRows.Count} 条产线，涉及 {affectedDeviceCount} 台问题设备，优先关注 {highRiskLine.LineName}。";
        var action = pendingCount > 0
            ? $"当前还有 {pendingCount} 条待闭环问题，建议先处理异常，再回头确认预警。"
            : _attentionRows.Count > 0
                ? "当前没有待闭环问题，但最近仍有需要复核的记录，先看最近关注项。"
                : "当前没有待闭环问题，下一步更适合看趋势是否持续稳定。";

        return string.Join(Environment.NewLine, [BuildDecisionTitle(pendingCount, highRiskLine), $"{overview} {focus}", action]);
    }

    private string BuildRiskLevelText(int pendingCount, LineSummaryRow? highRiskLine)
    {
        if (_currentDashboard.AbnormalCount > 0)
        {
            return highRiskLine is null ? "风险偏高" : $"{highRiskLine.LineName} 偏高";
        }

        if (pendingCount > 0)
        {
            return "需要关注";
        }

        if (_currentDashboard.WarningCount > 0)
        {
            return "轻度波动";
        }

        return "整体平稳";
    }

    private string BuildActionTitle(int pendingCount, LineSummaryRow? highRiskLine)
    {
        if (pendingCount > 0)
        {
            return highRiskLine is null ? "先去闭环" : $"先看 {highRiskLine.LineName}";
        }

        if (_attentionRows.Count > 0)
        {
            return "先复核记录";
        }

        return "看趋势复盘";
    }

    private static IReadOnlyList<LineSummaryRow> BuildLineRows(IReadOnlyList<InspectionRecordViewModel> records)
    {
        return records
            .GroupBy(record => record.LineName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Count();
                var normal = group.Count(record => record.Status == InspectionStatus.Normal);
                var warning = group.Count(record => record.Status == InspectionStatus.Warning);
                var abnormal = group.Count(record => record.Status == InspectionStatus.Abnormal);
                var passRate = total == 0 ? 0 : normal * 100F / total;

                return new LineSummaryRow
                {
                    LineName = group.First().LineName,
                    TotalCount = total,
                    NormalCount = normal,
                    WarningCount = warning,
                    AbnormalCount = abnormal,
                    PassRateText = $"{passRate:0.0}%"
                };
            })
            .OrderByDescending(row => row.AbnormalCount)
            .ThenByDescending(row => row.WarningCount)
            .ThenByDescending(row => row.TotalCount)
            .ThenBy(row => row.LineName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AttentionRow> BuildAttentionRows(IReadOnlyList<InspectionRecordViewModel> records)
    {
        return records
            .Where(record => record.Status != InspectionStatus.Normal)
            .OrderByDescending(record => record.CheckedAtValue)
            .Take(8)
            .Select(record => new AttentionRow
            {
                CheckedAt = record.CheckedAt,
                TargetName = $"{record.LineName} / {record.DeviceName}",
                StatusText = record.StatusText,
                Remark = BuildAttentionRemark(record)
            })
            .ToList();
    }

    private static string BuildAttentionRemark(InspectionRecordViewModel record)
    {
        var prefix = record.IsClosed ? "已闭环" : "待跟进";
        var detail = string.IsNullOrWhiteSpace(record.Remark) ? record.ActionRemark : record.Remark;
        return string.IsNullOrWhiteSpace(detail) ? prefix : $"{prefix} · {detail}";
    }

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
        }
    }

    private sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();
        }
    }

    private sealed class LineSummaryRow
    {
        public string LineName { get; init; } = string.Empty;

        public int TotalCount { get; init; }

        public int NormalCount { get; init; }

        public int WarningCount { get; init; }

        public int AbnormalCount { get; init; }

        public string PassRateText { get; init; } = "0%";
    }

    private sealed class AttentionRow
    {
        public string CheckedAt { get; init; } = string.Empty;

        public string TargetName { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;
    }
}
