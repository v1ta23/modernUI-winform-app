using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.Services;
using WinFormsApp.ViewModels;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace WinFormsApp.Views;

internal sealed class InspectionAnalyticsControl : UserControl, IInteractiveResizeAware
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceRaised = PageChrome.SurfaceRaised;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color InputBackground = PageChrome.InputBackground;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextSecondaryColor = PageChrome.TextSecondary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private const int KpiRowHeight = 152;
    private const int PrimaryRowHeight = 352;
    private const int DetailRowHeight = 432;
    private const int InsightTileRowHeight = 72;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color SuccessColor = PageChrome.AccentGreen;
    private static readonly Color WarningColor = PageChrome.AccentOrange;
    private static readonly Color DangerColor = PageChrome.AccentRed;
    private static readonly Color PendingColor = PageChrome.AccentPurple;

    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private readonly Panel _scrollHost;
    private readonly TableLayoutPanel _rootLayout;
    private readonly TableLayoutPanel _kpiLayout;
    private readonly TableLayoutPanel _primaryLayout;
    private readonly TableLayoutPanel _detailLayout;
    private readonly Control _headerShell;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly BufferedPanel _trendChartPanel;
    private readonly BufferedPanel _statusChartPanel;
    private readonly DataGridView _lineSummaryGrid;
    private readonly DataGridView _issueGrid;
    private readonly PageChrome.ReadOnlyTextBlock _summaryBlock;
    private readonly Button _generateAiButton;
    private readonly Label _totalValueLabel;
    private readonly Label _totalNoteLabel;
    private readonly Label _riskRateValueLabel;
    private readonly Label _riskRateNoteLabel;
    private readonly Label _pendingValueLabel;
    private readonly Label _pendingNoteLabel;
    private readonly Label _passRateValueLabel;
    private readonly Label _passRateNoteLabel;
    private readonly Label _topDeviceValueLabel;
    private readonly Label _topDeviceNoteLabel;
    private Label _trendSubtitleLabel = null!;
    private Label _statusSubtitleLabel = null!;
    private Label _lineSubtitleLabel = null!;
    private Label _issueSubtitleLabel = null!;
    private Label _summarySubtitleLabel = null!;
    private readonly Label _riskLevelValueLabel;
    private readonly Label _riskLevelNoteLabel;
    private readonly Label _primaryLineValueLabel;
    private readonly Label _primaryLineNoteLabel;
    private readonly Label _priorityActionValueLabel;
    private readonly Label _priorityActionNoteLabel;

    private InspectionDashboardViewModel _currentDashboard = new();
    private RiskAnalysisResult _currentAnalysis = RiskAnalysisResult.Empty;
    private InspectionFilterViewModel _currentFilter = new();
    private IReadOnlyList<string> _lineOptions = Array.Empty<string>();
    private IReadOnlyList<LineSummaryRow> _lineRows = Array.Empty<LineSummaryRow>();
    private IReadOnlyList<AttentionRow> _attentionRows = Array.Empty<AttentionRow>();
    private bool _scrollableLayoutUpdateQueued;
    private bool _updatingScrollableLayout;

    public InspectionAnalyticsControl(InspectionController inspectionController)
    {
        _inspectionController = inspectionController;

        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        DoubleBuffered = true;
        UpdateStyles();

        _generatedAtLabel = PageChrome.CreateInfoLabel();

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
        _summaryBlock = PageChrome.CreateReadOnlyTextBlock();

        _totalValueLabel = PageChrome.CreateValueLabel();
        _totalNoteLabel = PageChrome.CreateNoteLabel();
        _riskRateValueLabel = PageChrome.CreateValueLabel();
        _riskRateNoteLabel = PageChrome.CreateNoteLabel();
        _pendingValueLabel = PageChrome.CreateValueLabel();
        _pendingNoteLabel = PageChrome.CreateNoteLabel();
        _passRateValueLabel = PageChrome.CreateValueLabel();
        _passRateNoteLabel = PageChrome.CreateNoteLabel();
        _topDeviceValueLabel = PageChrome.CreateValueLabel(14F);
        _topDeviceNoteLabel = PageChrome.CreateNoteLabel();

        _riskLevelValueLabel = CreateInsightValueLabel(16F);
        _riskLevelNoteLabel = CreateInsightNoteLabel();
        _primaryLineValueLabel = CreateInsightValueLabel(11F);
        _primaryLineNoteLabel = CreateInsightNoteLabel();
        _priorityActionValueLabel = CreateInsightValueLabel(11F);
        _priorityActionNoteLabel = CreateInsightNoteLabel();

        _generateAiButton = PageChrome.CreateActionButton("生成 AI 分析", SuccessColor, false);
        _generateAiButton.Click += async (_, _) => await GenerateAiAnalysisAsync();

        var moreButton = PageChrome.CreateActionButton("更多", AccentBlue, false);
        moreButton.ContextMenuStrip = BuildMoreMenu();
        moreButton.Click += (_, _) =>
        {
            moreButton.ContextMenuStrip?.Show(moreButton, new Point(0, moreButton.Height + 4));
        };

        _scrollHost = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = PageBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.None,
            Location = Point.Empty,
            BackColor = PageBackground,
            ColumnCount = 1,
            RowCount = 4
        };
        _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, PageChrome.HeaderHeight));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, KpiRowHeight));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, PrimaryRowHeight));
        _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DetailRowHeight));

        _headerShell = BuildHeader(_generateAiButton, moreButton);
        _kpiLayout = BuildKpiRowUnified();
        _primaryLayout = BuildPrimaryRowUnified();
        _detailLayout = BuildDetailRowUnified();

        _rootLayout.Controls.Add(_headerShell, 0, 0);
        _rootLayout.Controls.Add(_kpiLayout, 0, 1);
        _rootLayout.Controls.Add(_primaryLayout, 0, 2);
        _rootLayout.Controls.Add(_detailLayout, 0, 3);

        _scrollHost.Controls.Add(_rootLayout);
        Controls.Add(_scrollHost);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _scrollHost, PageBackground);
        _scrollHost.BringToFront();
        SizeChanged += (_, _) => QueueScrollableLayoutUpdate();
        _scrollHost.SizeChanged += (_, _) => QueueScrollableLayoutUpdate();
        _headerShell.SizeChanged += (_, _) => QueueScrollableLayoutUpdate();
        VisibleChanged += (_, _) => QueueVisibleLayoutPass();
        ApplyTheme();
        RefreshData();
        UpdateScrollableLayout();
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        _scrollHost.BackColor = PageBackground;
        _rootLayout.BackColor = PageBackground;
        _kpiLayout.BackColor = PageBackground;
        _primaryLayout.BackColor = PageBackground;
        _detailLayout.BackColor = PageBackground;
        _trendChartPanel.BackColor = SurfaceBackground;
        _statusChartPanel.BackColor = SurfaceBackground;
        _summaryBlock.BackColor = SurfaceRaised;
        _summaryBlock.ForeColor = TextSecondaryColor;
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
    }

    public void RefreshData()
    {
        _currentDashboard = _inspectionController.Load(_currentFilter);
        _lineOptions = _currentDashboard.LineOptions;
        _lineRows = BuildLineRows(_currentDashboard.Records);
        _attentionRows = BuildAttentionRows(_currentDashboard.Records);

        var analysis = _currentDashboard.RiskAnalysis;

        _generatedAtLabel.Text = $"更新时间：{_currentDashboard.GeneratedAt:yyyy-MM-dd HH:mm}";
        _trendSubtitleLabel.Text = BuildTrendSubtitle(_currentDashboard.TrendPoints);
        _statusSubtitleLabel.Text = $"正常 {_currentDashboard.NormalCount} / 预警 {_currentDashboard.WarningCount} / 异常 {_currentDashboard.AbnormalCount}";
        _lineSubtitleLabel.Text = _lineRows.Count == 0
            ? "当前没有产线统计结果。"
            : $"按风险优先级排序，共 {_lineRows.Count} 条产线。";
        _issueSubtitleLabel.Text = _attentionRows.Count == 0
            ? "当前没有预警和异常记录。"
            : $"最近 {_attentionRows.Count} 条需关注记录。";

        UpdateKpiMetrics();
        ApplyRiskAnalysis(analysis);

        _lineSummaryGrid.DataSource = _lineRows.ToList();
        _issueGrid.DataSource = _attentionRows.ToList();

        _trendChartPanel.Invalidate();
        _statusChartPanel.Invalidate();
        UpdateScrollableLayout();
    }

    private async Task GenerateAiAnalysisAsync()
    {
        if (!_generateAiButton.Enabled)
        {
            return;
        }

        var originalText = _generateAiButton.Text;
        var originalCursor = Cursor;
        _generateAiButton.Enabled = false;
        _generateAiButton.Text = "AI 分析中...";
        Cursor = Cursors.WaitCursor;
        UseWaitCursor = true;
        ApplyRiskAnalysis(BuildAiLoadingAnalysis());
        _generatedAtLabel.Text = $"AI 分析中：{DateTime.Now:HH:mm:ss}";
        RefreshAiFeedback();

        RiskAnalysisResult? completedAnalysis = null;
        try
        {
            var analysis = await _inspectionController.GenerateAiAnalysisAsync(_currentFilter);
            if (IsDisposed)
            {
                return;
            }

            ApplyRiskAnalysis(analysis);
            _generatedAtLabel.Text = $"AI 分析已更新：{DateTime.Now:yyyy-MM-dd HH:mm}";
            RefreshAiFeedback();
            _inspectionController.SaveAiAnalysisHistory(
                analysis,
                AiReportFormatter.BuildAnalysisReport(analysis));
            completedAnalysis = analysis;
        }
        catch (TaskCanceledException)
        {
            ApplyRiskAnalysis(BuildAiErrorAnalysis("AI 分析超时，请稍后再试。"));
            RefreshAiFeedback();
            MessageBox.Show(this, "AI 分析超时，请稍后再试。", "AI 分析", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (InvalidOperationException ex)
        {
            ApplyRiskAnalysis(BuildAiErrorAnalysis(ex.Message));
            RefreshAiFeedback();
            MessageBox.Show(this, ex.Message, "AI 分析", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (HttpRequestException ex)
        {
            var message = $"AI 分析请求失败：{ex.Message}";
            ApplyRiskAnalysis(BuildAiErrorAnalysis(message));
            RefreshAiFeedback();
            MessageBox.Show(this, message, "AI 分析", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
            {
                _generateAiButton.Text = originalText;
                _generateAiButton.Enabled = true;
                Cursor = originalCursor;
                UseWaitCursor = false;
                RefreshAiFeedback();
            }
        }

        if (completedAnalysis is not null && !IsDisposed)
        {
            ShowAiAnalysisResult(completedAnalysis);
        }
    }

    private void ShowAiAnalysisResult(RiskAnalysisResult analysis)
    {
        using var dialog = new AiAnalysisResultDialog(analysis);
        dialog.ShowDialog(FindForm());
    }

    private void OpenAiSettings()
    {
        using var dialog = new AiSettingsDialog(
            _inspectionController.GetAiSettings(),
            _inspectionController.TestAiSettingsAsync);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _inspectionController.SaveAiSettings(dialog.Settings);
            _generatedAtLabel.Text = $"AI 设置已保存：{DateTime.Now:yyyy-MM-dd HH:mm}";
            RefreshAiFeedback();
            MessageBox.Show(this, "AI 设置已保存，当前页面已生效。", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is ArgumentException or UriFormatException or InvalidOperationException)
        {
            MessageBox.Show(this, $"AI 设置保存失败：{ex.Message}", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenAiHistory()
    {
        using var dialog = new AiAnalysisHistoryDialog(_inspectionController.GetAiAnalysisHistory());
        dialog.ShowDialog(FindForm());
    }

    private void OpenFilterDialog()
    {
        using var dialog = new InspectionAnalyticsFilterDialog(_currentFilter, _lineOptions);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        _currentFilter = dialog.Filter;
        RefreshData();
    }

    private void ShowDailyReport()
    {
        var reportText = AiReportFormatter.BuildDailyReport(
            _currentDashboard,
            _currentAnalysis,
            _currentFilter);
        using var dialog = new TextReportDialog(
            "巡检日报",
            "根据当前筛选结果和风险结论生成，可直接复制到日报或群通知。",
            reportText);
        dialog.ShowDialog(FindForm());
    }

    private void ApplyRiskAnalysis(RiskAnalysisResult analysis)
    {
        _currentAnalysis = analysis;
        _summarySubtitleLabel.Text = analysis.DecisionTitle;
        _summaryBlock.Text = BuildSummaryText(analysis);

        var riskColor = ResolveRiskColor(analysis.RiskLevel);
        _riskLevelValueLabel.Text = string.IsNullOrWhiteSpace(analysis.RiskLevel) ? "--" : analysis.RiskLevel;
        _riskLevelValueLabel.ForeColor = riskColor;
        _riskLevelNoteLabel.Text = string.IsNullOrWhiteSpace(analysis.RiskLevelNote) ? "等待风险结论。" : analysis.RiskLevelNote;

        _primaryLineValueLabel.Text = string.IsNullOrWhiteSpace(analysis.PrimaryLineName) ? "--" : analysis.PrimaryLineName;
        _primaryLineNoteLabel.Text = string.IsNullOrWhiteSpace(analysis.PrimaryLineNote) ? "暂无重点产线。" : analysis.PrimaryLineNote;

        _priorityActionValueLabel.Text = string.IsNullOrWhiteSpace(analysis.ActionTitle) ? "--" : analysis.ActionTitle;
        _priorityActionNoteLabel.Text = string.IsNullOrWhiteSpace(analysis.ActionNote) ? analysis.PriorityAction : analysis.ActionNote;
    }

    private void RefreshAiFeedback()
    {
        _headerShell.PerformLayout();
        _headerShell.Invalidate();
        _summarySubtitleLabel.Invalidate();
        _summaryBlock.Invalidate();
        Invalidate();
    }

    private static RiskAnalysisResult BuildAiLoadingAnalysis()
    {
        return new RiskAnalysisResult(
            "AI 正在分析当前巡检数据，请稍等。",
            "分析中",
            "正在读取异常、预警和待闭环记录。",
            "计算中",
            "AI 完成后会更新重点产线。",
            "等待结果",
            "请保持页面打开。",
            "正在整理巡检记录和产线风险。",
            "等待 AI 生成处理优先级。",
            "分析完成后会自动刷新这里。");
    }

    private static RiskAnalysisResult BuildAiErrorAnalysis(string message)
    {
        var reason = string.IsNullOrWhiteSpace(message)
            ? "AI 服务暂时没有返回可用结果。"
            : message;
        return new RiskAnalysisResult(
            "AI 分析未完成，请检查网络或密钥配置。",
            "未更新",
            "当前仍可参考本地风险判断。",
            "未更新",
            "重点产线暂未更新。",
            "稍后重试",
            "接口恢复后再次点击生成。",
            reason,
            "先按本地风险结论处理待闭环问题。",
            "确认网络、密钥和模型配置后重试。");
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

            UpdateScrollableLayout();
            _headerShell.PerformLayout();
            _rootLayout.PerformLayout();
            PerformLayout();
            Invalidate(true);
        }));
    }

    private void QueueScrollableLayoutUpdate()
    {
        if (_updatingScrollableLayout || _scrollableLayoutUpdateQueued || !IsHandleCreated || IsDisposed)
        {
            return;
        }

        _scrollableLayoutUpdateQueued = true;
        BeginInvoke(new MethodInvoker(() =>
        {
            _scrollableLayoutUpdateQueued = false;
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            UpdateScrollableLayout();
        }));
    }

    private void UpdateScrollableLayout()
    {
        if (_updatingScrollableLayout || _scrollHost.ClientSize.Width <= 0)
        {
            return;
        }

        _updatingScrollableLayout = true;
        try
        {
            var viewportWidth = Math.Max(320, _scrollHost.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            if (_rootLayout.Width != viewportWidth)
            {
                _rootLayout.Width = viewportWidth;
            }

            var headerWidth = Math.Max(1, viewportWidth - _headerShell.Margin.Horizontal);
            var headerHeight = Math.Max(
                PageChrome.HeaderHeight,
                _headerShell.GetPreferredSize(new Size(headerWidth, 0)).Height + _headerShell.Margin.Vertical);
            var headerRow = _rootLayout.RowStyles[0];
            if (headerRow.SizeType != SizeType.Absolute || Math.Abs(headerRow.Height - headerHeight) > 0.5F)
            {
                headerRow.SizeType = SizeType.Absolute;
                headerRow.Height = headerHeight;
            }

            var headerControlHeight = Math.Max(1, headerHeight - _headerShell.Margin.Vertical);
            if (_headerShell.Height != headerControlHeight)
            {
                _headerShell.Height = headerControlHeight;
            }

            var targetHeight = headerHeight + KpiRowHeight + PrimaryRowHeight + DetailRowHeight;
            if (_rootLayout.Height != targetHeight)
            {
                _rootLayout.Height = targetHeight;
            }

            var scrollSize = new Size(viewportWidth, targetHeight);
            if (_scrollHost.AutoScrollMinSize != scrollSize)
            {
                _scrollHost.AutoScrollMinSize = scrollSize;
            }
        }
        finally
        {
            _updatingScrollableLayout = false;
        }
    }

    private ContextMenuStrip BuildMoreMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = SurfaceBackground,
            ForeColor = TextPrimaryColor,
            Font = new Font("Microsoft YaHei UI", 9F),
            ShowImageMargin = false
        };
        menu.Items.Add(CreateMenuItem("刷新数据", RefreshData));
        menu.Items.Add(CreateMenuItem("筛选", OpenFilterDialog));
        menu.Items.Add(CreateMenuItem("生成日报", ShowDailyReport));
        menu.Items.Add(CreateMenuItem("分析历史", OpenAiHistory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("AI 设置", OpenAiSettings));
        return menu;
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action action)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    private Control BuildHeader(Button generateAiButton, Button moreButton)
    {
        return PageChrome.CreatePageHeader(
            "生产风险看板",
            "汇总巡检趋势、异常分布和待处理风险。",
            _generatedAtLabel,
            generateAiButton,
            moreButton);
    }

    private TableLayoutPanel BuildKpiRow()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        for (var column = 0; column < 5; column++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        layout.Controls.Add(CreateMetricTile("总巡检", AccentBlue, _totalValueLabel, _totalNoteLabel, new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(CreateMetricTile("风险率", WarningColor, _riskRateValueLabel, _riskRateNoteLabel, new Padding(0, 0, 12, 0)), 1, 0);
        layout.Controls.Add(CreateMetricTile("待闭环", PendingColor, _pendingValueLabel, _pendingNoteLabel, new Padding(0, 0, 12, 0)), 2, 0);
        layout.Controls.Add(CreateMetricTile("合格率", SuccessColor, _passRateValueLabel, _passRateNoteLabel, new Padding(0, 0, 12, 0)), 3, 0);
        layout.Controls.Add(CreateMetricTile("高风险设备", DangerColor, _topDeviceValueLabel, _topDeviceNoteLabel, Padding.Empty), 4, 0);
        return layout;
    }

    private TableLayoutPanel BuildPrimaryRow()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(CreateSolidSectionShell(
            "风险趋势",
            "最近 8 个时间段的巡检状态变化。",
            out _trendSubtitleLabel,
            _trendChartPanel,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(CreateSolidSectionShell(
            "异常分布",
            "正常、预警和异常占比。",
            out _statusSubtitleLabel,
            _statusChartPanel,
            Padding.Empty), 1, 0);
        return layout;
    }

    private TableLayoutPanel BuildDetailRow()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var detailBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        detailBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        detailBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        detailBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        detailBody.Controls.Add(CreateGridBlock("产线排行", out _lineSubtitleLabel, _lineSummaryGrid, new Padding(0, 0, 12, 0)), 0, 0);
        detailBody.Controls.Add(CreateGridBlock("待关注记录", out _issueSubtitleLabel, _issueGrid, Padding.Empty), 1, 0);

        layout.Controls.Add(CreateSolidSectionShell(
            "风险明细",
            "产线排行和待关注记录同屏展示。",
            out _,
            detailBody,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(BuildSummaryRow(), 1, 0);
        return layout;
    }

    private Control BuildSummaryRow()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, (InsightTileRowHeight * 3) + 16F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var insightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        insightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.Controls.Add(CreateInsightTile("风险等级", DangerColor, _riskLevelValueLabel, _riskLevelNoteLabel, new Padding(0, 0, 0, 8)), 0, 0);
        insightLayout.Controls.Add(CreateInsightTile("重点产线", WarningColor, _primaryLineValueLabel, _primaryLineNoteLabel, new Padding(0, 0, 0, 8)), 0, 1);
        insightLayout.Controls.Add(CreateInsightTile("优先动作", AccentBlue, _priorityActionValueLabel, _priorityActionNoteLabel, Padding.Empty), 0, 2);

        body.Controls.Add(insightLayout, 0, 0);
        body.Controls.Add(_summaryBlock, 0, 1);

        return CreateSolidSectionShell(
            "Agent 洞察",
            "当前风险判断和处理优先级。",
            out _summarySubtitleLabel,
            body,
            Padding.Empty);
    }

    private TableLayoutPanel BuildKpiRowUnified()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        for (var column = 0; column < 5; column++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        layout.Controls.Add(PageChrome.CreateMetricCard("总巡检", AccentBlue, _totalValueLabel, _totalNoteLabel), 0, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("风险率", WarningColor, _riskRateValueLabel, _riskRateNoteLabel), 1, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("待闭环", PendingColor, _pendingValueLabel, _pendingNoteLabel), 2, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("合格率", SuccessColor, _passRateValueLabel, _passRateNoteLabel), 3, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("高风险设备", DangerColor, _topDeviceValueLabel, _topDeviceNoteLabel, Padding.Empty), 4, 0);
        return layout;
    }

    private TableLayoutPanel BuildPrimaryRowUnified()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(PageChrome.CreateSectionShell(
            "风险趋势",
            "展示最近 8 个时间段的巡检状态变化。",
            out _trendSubtitleLabel,
            _trendChartPanel,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(PageChrome.CreateSectionShell(
            "异常分布",
            "查看正常、预警和异常记录占比。",
            out _statusSubtitleLabel,
            _statusChartPanel,
            Padding.Empty), 1, 0);
        return layout;
    }

    private TableLayoutPanel BuildDetailRowUnified()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var detailBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        detailBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        detailBody.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        detailBody.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        detailBody.Controls.Add(CreateGridBlockUnified("产线排行", out _lineSubtitleLabel, _lineSummaryGrid, new Padding(0, 0, 0, 12)), 0, 0);
        detailBody.Controls.Add(CreateGridBlockUnified("待关注记录", out _issueSubtitleLabel, _issueGrid, Padding.Empty), 0, 1);

        layout.Controls.Add(PageChrome.CreateSectionShell(
            "风险明细",
            "产线排行和待关注记录同屏展示。",
            out _,
            detailBody,
            new Padding(0, 0, 12, 0)), 0, 0);
        layout.Controls.Add(BuildSummaryRowUnified(), 1, 0);
        return layout;
    }

    private Control BuildSummaryRowUnified()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, (InsightTileRowHeight * 3) + 20F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var insightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        insightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, InsightTileRowHeight));
        insightLayout.Controls.Add(CreateInsightTileUnified("风险等级", DangerColor, _riskLevelValueLabel, _riskLevelNoteLabel, new Padding(0, 0, 0, 10)), 0, 0);
        insightLayout.Controls.Add(CreateInsightTileUnified("重点产线", WarningColor, _primaryLineValueLabel, _primaryLineNoteLabel, new Padding(0, 0, 0, 10)), 0, 1);
        insightLayout.Controls.Add(CreateInsightTileUnified("优先动作", AccentBlue, _priorityActionValueLabel, _priorityActionNoteLabel, Padding.Empty), 0, 2);

        body.Controls.Add(insightLayout, 0, 0);
        body.Controls.Add(CreateSummaryBlockShell(), 0, 1);

        return PageChrome.CreateSectionShell(
            "Agent 洞察",
            "当前风险判断和处理优先级。",
            out _summarySubtitleLabel,
            body,
            Padding.Empty);
    }

    private static Control CreateGridBlockUnified(string title, out Label subtitleLabel, DataGridView grid, Padding margin)
    {
        var block = PageChrome.CreateSurfacePanel(
            new Padding(14, 12, 14, 14),
            14,
            fillColor: SurfaceRaised,
            borderColor: SurfaceBorder);
        block.Margin = margin;

        var titleLabel = PageChrome.CreateTextLabel(title, 9.5F, FontStyle.Bold, TextPrimaryColor, new Padding(0, 0, 0, 2));
        subtitleLabel = PageChrome.CreateTextLabel(string.Empty, 8.2F, FontStyle.Regular, TextMutedColor, new Padding(0, 0, 0, 8));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        grid.Dock = DockStyle.Fill;
        grid.Margin = Padding.Empty;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(subtitleLabel, 0, 1);
        layout.Controls.Add(grid, 0, 2);
        block.Controls.Add(layout);
        return block;
    }

    private Control CreateSummaryBlockShell()
    {
        var block = PageChrome.CreateSurfacePanel(
            new Padding(14),
            14,
            fillColor: SurfaceRaised,
            borderColor: SurfaceBorder);
        _summaryBlock.Dock = DockStyle.Fill;
        _summaryBlock.Margin = Padding.Empty;
        block.Controls.Add(_summaryBlock);
        return block;
    }

    private static Control CreateInsightTileUnified(string title, Color accent, Label valueLabel, Label noteLabel, Padding margin)
    {
        var tile = PageChrome.CreateSurfacePanel(
            new Padding(0),
            14,
            fillColor: SurfaceRaised,
            borderColor: PageChrome.MixColor(SurfaceBorder, accent, 0.32F));
        tile.Margin = margin;

        var accentBar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = accent,
            Margin = Padding.Empty
        };

        var titleLabel = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 8.6F),
            ForeColor = TextMutedColor,
            Margin = Padding.Empty,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };

        valueLabel.BackColor = SurfaceRaised;
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Margin = Padding.Empty;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.Height = Math.Max(valueLabel.Height, 30);

        noteLabel.BackColor = SurfaceRaised;
        noteLabel.Visible = false;

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(12, 10, 14, 10)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.Controls.Add(titleLabel, 0, 0);
        content.Controls.Add(valueLabel, 1, 0);

        tile.Controls.Add(content);
        tile.Controls.Add(accentBar);
        return tile;
    }

    private void UpdateKpiMetrics()
    {
        var total = _currentDashboard.TotalCount;
        var riskCount = _currentDashboard.WarningCount + _currentDashboard.AbnormalCount;
        var riskRate = total == 0 ? 0F : riskCount * 100F / total;
        var pendingCount = _currentDashboard.Records.Count(record =>
            record.Status != InspectionStatus.Normal &&
            !record.IsClosed &&
            !record.IsRevoked);
        var closedRiskCount = _currentDashboard.Records.Count(record =>
            record.Status != InspectionStatus.Normal &&
            record.IsClosed &&
            !record.IsRevoked);
        var closureBase = pendingCount + closedRiskCount;
        var closureRate = closureBase == 0 ? 100F : closedRiskCount * 100F / closureBase;
        var topDevice = BuildTopRiskDevice(_currentDashboard.Records);

        _totalValueLabel.Text = total.ToString();
        _totalNoteLabel.Text = $"正常 {_currentDashboard.NormalCount}，风险 {riskCount}";
        _riskRateValueLabel.Text = $"{riskRate:0.0}%";
        _riskRateNoteLabel.Text = $"预警 {_currentDashboard.WarningCount}，异常 {_currentDashboard.AbnormalCount}";
        _pendingValueLabel.Text = pendingCount.ToString();
        _pendingNoteLabel.Text = $"闭环率 {closureRate:0.0}%";
        _passRateValueLabel.Text = _currentDashboard.PassRateText;
        _passRateNoteLabel.Text = total == 0 ? "暂无巡检记录" : "正常记录占比";
        _topDeviceValueLabel.Text = ShortenMetricText(topDevice.Name, 14);
        _topDeviceNoteLabel.Text = topDevice.Count == 0 ? "暂无突出设备" : $"风险 {topDevice.Count} 条";
    }

    private static Control CreateSolidSectionShell(
        string title,
        string subtitle,
        out Label subtitleLabel,
        Control body,
        Padding margin)
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = margin,
            Padding = new Padding(16, 14, 16, 16)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = CreateSolidLabel(title, 11F, FontStyle.Bold, TextPrimaryColor, SurfaceBackground);
        titleLabel.Margin = new Padding(0, 0, 0, 4);
        subtitleLabel = CreateSolidLabel(subtitle, 8.8F, FontStyle.Regular, TextMutedColor, SurfaceBackground);
        subtitleLabel.Margin = new Padding(0, 0, 0, 10);

        body.Dock = DockStyle.Fill;
        body.Margin = Padding.Empty;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(subtitleLabel, 0, 1);
        layout.Controls.Add(body, 0, 2);
        shell.Controls.Add(layout);
        return shell;
    }

    private static Control CreateGridBlock(string title, out Label subtitleLabel, DataGridView grid, Padding margin)
    {
        var block = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = margin,
            Padding = new Padding(12, 10, 12, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = InputBackground,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleLabel = CreateSolidLabel(title, 9.5F, FontStyle.Bold, TextPrimaryColor, InputBackground);
        titleLabel.Margin = new Padding(0, 0, 0, 2);
        subtitleLabel = CreateSolidLabel(string.Empty, 8.2F, FontStyle.Regular, TextMutedColor, InputBackground);
        subtitleLabel.Margin = new Padding(0, 0, 0, 8);

        grid.Dock = DockStyle.Fill;
        grid.Margin = Padding.Empty;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(subtitleLabel, 0, 1);
        layout.Controls.Add(grid, 0, 2);
        block.Controls.Add(layout);
        return block;
    }

    private static Control CreateMetricTile(string title, Color accent, Label valueLabel, Label noteLabel, Padding margin)
    {
        var fillColor = PageChrome.MixColor(SurfaceBackground, accent, 0.045F);
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = fillColor,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = margin,
            Padding = new Padding(14, 10, 14, 10)
        };

        var titleLabel = CreateSolidLabel(title, 8.6F, FontStyle.Regular, TextMutedColor, fillColor);
        titleLabel.Margin = new Padding(0, 0, 0, 4);
        valueLabel.BackColor = fillColor;
        valueLabel.ForeColor = TextPrimaryColor;
        valueLabel.Dock = DockStyle.Top;
        valueLabel.Margin = Padding.Empty;
        noteLabel.BackColor = fillColor;
        noteLabel.ForeColor = TextMutedColor;
        noteLabel.Dock = DockStyle.Top;
        noteLabel.Margin = new Padding(0, 6, 0, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = fillColor,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        layout.Controls.Add(noteLabel, 0, 2);
        tile.Controls.Add(layout);
        return tile;
    }

    private static Control CreateInsightTile(string title, Color accent, Label valueLabel, Label noteLabel, Padding margin)
    {
        var fillColor = PageChrome.MixColor(InputBackground, accent, 0.08F);
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = fillColor,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = margin,
            Padding = new Padding(10, 8, 10, 8)
        };

        var titleLabel = CreateSolidLabel(title, 8F, FontStyle.Regular, TextMutedColor, fillColor);
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Height = 22;
        titleLabel.Margin = Padding.Empty;
        valueLabel.BackColor = fillColor;
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Margin = Padding.Empty;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        noteLabel.BackColor = fillColor;
        noteLabel.Visible = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = fillColor,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(valueLabel, 1, 0);
        tile.Controls.Add(layout);
        return tile;
    }

    private static Label CreateSolidLabel(string text, float size, FontStyle style, Color color, Color backColor)
    {
        return new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = backColor,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", size, style),
            ForeColor = color,
            Height = Math.Max(18, (int)Math.Ceiling(size * 2.2F)),
            Margin = Padding.Empty,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateMetricValueLabel(float size = 16F)
    {
        return new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            Height = 28,
            Margin = Padding.Empty,
            Text = "--",
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private static Label CreateMetricNoteLabel()
    {
        return new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 8.2F),
            Height = 20,
            Margin = Padding.Empty,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateInsightValueLabel(float size)
    {
        return new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", size, FontStyle.Bold),
            ForeColor = TextPrimaryColor,
            Height = size >= 15F ? 28 : 22,
            Margin = Padding.Empty,
            Text = "--",
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateInsightNoteLabel()
    {
        return new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 8.1F),
            ForeColor = TextMutedColor,
            Margin = Padding.Empty,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateSummaryTextBox()
    {
        return new TextBox
        {
            BackColor = InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 8.7F),
            ForeColor = PageChrome.TextSecondary,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true
        };
    }

    private static (string Name, int Count) BuildTopRiskDevice(IReadOnlyList<InspectionRecordViewModel> records)
    {
        var top = records
            .Where(record => record.Status != InspectionStatus.Normal)
            .GroupBy(record => string.IsNullOrWhiteSpace(record.DeviceName) ? record.LineName : record.DeviceName)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return top is null ? ("--", 0) : (top.Name, top.Count);
    }

    private static string ShortenMetricText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "--";
        }

        return text.Length <= maxLength ? text : text[..Math.Max(1, maxLength - 1)] + "…";
    }

    private static Color ResolveRiskColor(string riskLevel)
    {
        if (riskLevel.Contains("高", StringComparison.OrdinalIgnoreCase))
        {
            return DangerColor;
        }

        if (riskLevel.Contains("中", StringComparison.OrdinalIgnoreCase))
        {
            return WarningColor;
        }

        return SuccessColor;
    }

    private static DataGridView CreateLineSummaryGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.LineName), "产线", 120F, 76));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.TotalCount), "总数", 58F, 42));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.NormalCount), "正常", 58F, 42));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.WarningCount), "预警", 58F, 42));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.AbnormalCount), "异常", 58F, 42));
        grid.Columns.Add(CreateTextColumn(nameof(LineSummaryRow.PassRateText), "合格率", 72F, 58));
        return grid;
    }

    private static DataGridView CreateIssueGrid()
    {
        var grid = CreateGrid();
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.CheckedAt), "时间", 92F, 74));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.TargetName), "设备", 130F, 96));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.StatusText), "状态", 52F, 42));
        grid.Columns.Add(CreateTextColumn(nameof(AttentionRow.Remark), "说明", 150F, 98));
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
        grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.AlternatingRowsDefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8.6F, FontStyle.Bold);
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
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
        var axisMaxValue = Math.Max(4, maxValue);
        using var axisPen = new Pen(SurfaceBorder, 1F);
        using var gridPen = new Pen(Color.FromArgb(55, 62, 80), 1F);

        for (var index = 0; index <= 4; index++)
        {
            var y = plotRect.Bottom - plotRect.Height * index / 4F;
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var label = Math.Round(axisMaxValue * index / 4F).ToString("0");
            g.DrawString(label, labelFont, labelBrush, bounds.Left, y - 8);
        }

        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);

        DrawTrendSeries(g, plotRect, points, axisMaxValue, point => point.NormalCount, SuccessColor);
        DrawTrendSeries(g, plotRect, points, axisMaxValue, point => point.WarningCount, WarningColor);
        DrawTrendSeries(g, plotRect, points, axisMaxValue, point => point.AbnormalCount, DangerColor);

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
        var legendHeight = compactLegend ? counts.Length * 26 : 44;
        var availableDiameterHeight = contentRect.Height - legendHeight - 12;
        var maxDiameter = Math.Max(1, Math.Min(contentRect.Width, contentRect.Height));
        var minDiameter = Math.Min(compactLegend ? 56 : 78, maxDiameter);
        var diameter = Math.Min(availableDiameterHeight, contentRect.Width - 24);
        diameter = Math.Clamp(diameter, minDiameter, maxDiameter);
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

        var valueFontSize = diameter < 90 ? 15F : 18F;
        using var valueFont = new Font("Segoe UI", valueFontSize, FontStyle.Bold);
        using var totalBrush = new SolidBrush(TextPrimaryColor);
        var totalText = total.ToString();
        const string totalLabel = "总记录";
        var totalSize = g.MeasureString(totalText, valueFont);
        var totalLabelSize = g.MeasureString(totalLabel, labelFont);
        var totalGap = diameter < 90 ? 0F : 2F;
        var totalBlockHeight = totalSize.Height + totalGap + totalLabelSize.Height;
        var totalTop = donutRect.Top + (donutRect.Height - totalBlockHeight) / 2F;
        g.DrawString(totalText, valueFont, totalBrush,
            donutRect.Left + donutRect.Width / 2F - totalSize.Width / 2F,
            totalTop);
        g.DrawString(totalLabel, labelFont, labelBrush,
            donutRect.Left + donutRect.Width / 2F - totalLabelSize.Width / 2F,
            totalTop + totalSize.Height + totalGap);

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

    private static string BuildSummaryText(RiskAnalysisResult analysis)
    {
        return string.Join(Environment.NewLine, [
            $"风险等级：{analysis.RiskLevel}",
            $"主要原因：{analysis.RiskReason}",
            $"优先处理：{analysis.PriorityAction}",
            $"责任建议：{analysis.SuggestedOwner}",
            $"处理时限：{analysis.SuggestedDeadline}",
            $"停机复检：{analysis.StopInspectionAdvice}",
            $"管理建议：{analysis.ManagementAdvice}"
        ]);
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
