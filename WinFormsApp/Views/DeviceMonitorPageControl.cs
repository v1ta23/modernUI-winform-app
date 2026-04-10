using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class DeviceMonitorPageControl : UserControl
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextSecondaryColor = PageChrome.TextSecondary;
    private static readonly Color TextMutedColor = PageChrome.TextMuted;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color SuccessColor = PageChrome.AccentGreen;
    private static readonly Color WarningColor = PageChrome.AccentOrange;
    private static readonly Color DangerColor = PageChrome.AccentRed;

    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private Label _deviceCountValueLabel = null!;
    private Label _issueDeviceValueLabel = null!;
    private Label _issueDeviceNoteLabel = null!;
    private Label _pendingCountValueLabel = null!;
    private Label _pendingCountNoteLabel = null!;
    private Label _healthyCountValueLabel = null!;
    private Label _healthyCountNoteLabel = null!;
    private Label _focusDeviceLabel = null!;
    private Label _focusDetailLabel = null!;
    private DataGridView _deviceGrid = null!;
    private DataGridView _attentionGrid = null!;
    private Label _attentionEmptyLabel = null!;

    private IReadOnlyList<DeviceRow> _deviceRows = Array.Empty<DeviceRow>();
    private IReadOnlyList<AttentionRow> _attentionRows = Array.Empty<AttentionRow>();

    public DeviceMonitorPageControl(InspectionController inspectionController)
    {
        _inspectionController = inspectionController;
        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        var refreshButton = CreateRefreshButton();
        refreshButton.Click += (_, _) => RefreshData();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = BuildHeader(refreshButton);
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildSummaryArea(), 0, 1);
        root.Controls.Add(BuildBodyArea(), 0, 2);

        Controls.Add(root);
        ApplyTheme();
        Load += (_, _) => RefreshData();
    }

    public void RefreshData()
    {
        var dashboard = _inspectionController.Load(new InspectionFilterViewModel());
        var records = dashboard.Records
            .Where(record => !record.IsRevoked)
            .OrderByDescending(record => record.CheckedAtValue)
            .ToList();

        _deviceRows = records
            .GroupBy(record => new { record.LineName, record.DeviceName })
            .Select(group =>
            {
                var latest = group.First();
                var pendingCount = group.Count(record => record.Status != InspectionStatus.Normal && !record.IsClosed);
                var abnormalCount = group.Count(record => record.Status == InspectionStatus.Abnormal && !record.IsClosed);
                var warningCount = group.Count(record => record.Status == InspectionStatus.Warning && !record.IsClosed);

                return new DeviceRow
                {
                    LineName = latest.LineName,
                    DeviceName = latest.DeviceName,
                    LatestStatus = latest.StatusText,
                    LatestCheckedAt = latest.CheckedAtValue.ToString("MM-dd HH:mm"),
                    Inspector = latest.Inspector,
                    PendingCount = pendingCount,
                    AttentionLevel = abnormalCount > 0
                        ? "异常待处理"
                        : warningCount > 0
                            ? "预警待确认"
                            : "状态稳定"
                };
            })
            .OrderByDescending(row => row.PendingCount)
            .ThenBy(row => row.LineName)
            .ThenBy(row => row.DeviceName)
            .ToList();

        _attentionRows = records
            .Where(record => record.Status != InspectionStatus.Normal && !record.IsClosed)
            .Take(8)
            .Select(record => new AttentionRow
            {
                DeviceName = record.DeviceName,
                InspectionItem = record.InspectionItem,
                StatusText = record.StatusText,
                CheckedAt = record.CheckedAtValue.ToString("MM-dd HH:mm"),
                Detail = $"{record.LineName} / {record.Inspector}"
            })
            .ToList();

        var focusRow = _deviceRows.FirstOrDefault(row => row.PendingCount > 0) ?? _deviceRows.FirstOrDefault();
        var pendingDeviceCount = _deviceRows.Count(row => row.PendingCount > 0);
        var healthyDeviceCount = Math.Max(0, _deviceRows.Count - pendingDeviceCount);

        _deviceCountValueLabel.Text = _deviceRows.Count.ToString();
        _pendingCountValueLabel.Text = pendingDeviceCount.ToString();
        _pendingCountNoteLabel.Text = pendingDeviceCount == 0 ? "当前没有待处理设备" : "优先处理有未闭环问题的设备";
        _healthyCountValueLabel.Text = healthyDeviceCount.ToString();
        _healthyCountNoteLabel.Text = healthyDeviceCount == 0 ? "暂时没有稳定设备" : "最近一次巡检结果正常";

        if (focusRow is null)
        {
            _issueDeviceValueLabel.Text = "--";
            _issueDeviceNoteLabel.Text = "暂时没有设备数据";
            _focusDeviceLabel.Text = "暂无重点设备";
            _focusDetailLabel.Text = "等有巡检记录后，这里再显示需要优先处理的设备。";
        }
        else
        {
            _issueDeviceValueLabel.Text = focusRow.DeviceName;
            _issueDeviceNoteLabel.Text = $"{focusRow.LineName} / {focusRow.AttentionLevel}";
            _focusDeviceLabel.Text = $"{focusRow.LineName} / {focusRow.DeviceName}";
            _focusDetailLabel.Text = focusRow.PendingCount > 0
                ? $"当前有 {focusRow.PendingCount} 条待处理问题，建议先进入报警中心或巡检页。"
                : $"最近巡检时间 {focusRow.LatestCheckedAt}，当前状态稳定。";
        }

        _generatedAtLabel.Text = $"最近刷新 {dashboard.GeneratedAt:yyyy-MM-dd HH:mm:ss}";
        _deviceGrid.DataSource = _deviceRows.ToList();
        _attentionGrid.DataSource = _attentionRows.ToList();
        _attentionGrid.Visible = _attentionRows.Count > 0;
        _attentionEmptyLabel.Visible = _attentionRows.Count == 0;
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        PageChrome.ApplyGridTheme(_deviceGrid);
        PageChrome.ApplyGridTheme(_attentionGrid);
        Invalidate(true);
    }

    private Control BuildHeader(Button refreshButton)
    {
        return PageChrome.CreatePageHeader(
            "设备监控",
            "先把设备维度的工作面做出来，首页后面只抽摘要。",
            _generatedAtLabel,
            refreshButton);
    }

    private Control BuildSummaryArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < 4; index++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _deviceCountValueLabel = PageChrome.CreateValueLabel();
        var deviceNoteLabel = PageChrome.CreateNoteLabel("当前有巡检数据的设备数");

        _issueDeviceValueLabel = PageChrome.CreateValueLabel(16F);
        _issueDeviceNoteLabel = PageChrome.CreateNoteLabel();

        _pendingCountValueLabel = PageChrome.CreateValueLabel();
        _pendingCountNoteLabel = PageChrome.CreateNoteLabel();

        _healthyCountValueLabel = PageChrome.CreateValueLabel();
        _healthyCountNoteLabel = PageChrome.CreateNoteLabel();

        layout.Controls.Add(PageChrome.CreateMetricCard("设备总数", AccentBlue, _deviceCountValueLabel, deviceNoteLabel), 0, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("当前关注设备", WarningColor, _issueDeviceValueLabel, _issueDeviceNoteLabel), 1, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("待处理设备", DangerColor, _pendingCountValueLabel, _pendingCountNoteLabel), 2, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("状态稳定", SuccessColor, _healthyCountValueLabel, _healthyCountNoteLabel, Padding.Empty), 3, 0);
        return layout;
    }

    private Control BuildBodyArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _deviceGrid = CreateGrid();
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LineName), "产线", 90));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.DeviceName), "设备", 160));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LatestStatus), "最近状态", 90));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.AttentionLevel), "关注级别", 120));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.PendingCount), "待处理", 70));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.LatestCheckedAt), "最近巡检", 150));
        _deviceGrid.Columns.Add(CreateTextColumn(nameof(DeviceRow.Inspector), "巡检人", 90));
        _deviceGrid.CellFormatting += DeviceGridOnCellFormatting;

        var devicePanel = PageChrome.CreateSectionShell(
            "设备列表",
            "详细控制和更多参数后面继续往这个页面里加。",
            out _,
            _deviceGrid,
            new Padding(0, 0, 12, 0));

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 188F));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _focusDeviceLabel = PageChrome.CreateValueLabel(16F);
        _focusDeviceLabel.Dock = DockStyle.Top;
        _focusDeviceLabel.Margin = Padding.Empty;
        _focusDetailLabel = PageChrome.CreateNoteLabel();
        _focusDetailLabel.AutoSize = false;
        _focusDetailLabel.Dock = DockStyle.Fill;
        _focusDetailLabel.TextAlign = ContentAlignment.TopLeft;

        var focusContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        focusContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        focusContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        focusContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        focusContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        focusContent.Controls.Add(PageChrome.CreateNoteLabel("这里固定放最该先看的那台设备。", 8.8F, TextMutedColor), 0, 0);
        focusContent.Controls.Add(_focusDeviceLabel, 0, 1);
        focusContent.Controls.Add(_focusDetailLabel, 0, 2);

        var focusPanel = PageChrome.CreateSectionShell(
            "重点设备",
            "先处理最该看的设备，首页以后只显示这条摘要。",
            out _,
            focusContent,
            new Padding(0, 0, 0, 12));

        _attentionGrid = CreateGrid();
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.DeviceName), "设备", 120));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.InspectionItem), "问题项", 150));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.StatusText), "状态", 70));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.CheckedAt), "时间", 100));
        _attentionGrid.Columns.Add(CreateTextColumn(nameof(AttentionRow.Detail), "补充", 150));
        _attentionGrid.CellFormatting += AttentionGridOnCellFormatting;

        _attentionEmptyLabel = PageChrome.CreateEmptyStateLabel("当前没有待关注记录");
        var attentionBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        attentionBody.Controls.Add(_attentionGrid);
        attentionBody.Controls.Add(_attentionEmptyLabel);

        var attentionPanel = PageChrome.CreateSectionShell(
            "最近关注",
            "只放最需要马上看见的几条。",
            out _,
            attentionBody,
            Padding.Empty);

        rightLayout.Controls.Add(focusPanel, 0, 0);
        rightLayout.Controls.Add(attentionPanel, 0, 1);

        layout.Controls.Add(devicePanel, 0, 0);
        layout.Controls.Add(rightLayout, 1, 0);
        return layout;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string dataPropertyName,
        string headerText,
        float fillWeight,
        int minimumWidth = 68)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = dataPropertyName,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            MinimumWidth = minimumWidth
        };
    }

    private void DeviceGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_deviceGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(DeviceRow.AttentionLevel) &&
            e.Value is string text)
        {
            var cellStyle = e.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "异常待处理" => DangerColor,
                "预警待确认" => WarningColor,
                _ => SuccessColor
            };
        }
    }

    private void AttentionGridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_attentionGrid.Columns[e.ColumnIndex].DataPropertyName == nameof(AttentionRow.StatusText) &&
            e.Value is string text)
        {
            var cellStyle = e.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "异常" => DangerColor,
                "预警" => WarningColor,
                _ => TextSecondaryColor
            };
        }
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
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
            ScrollBars = ScrollBars.Vertical,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        PageChrome.ApplyGridTheme(grid);
        return grid;
    }

    private static Button CreateRefreshButton()
    {
        return PageChrome.CreateActionButton("刷新监控", AccentBlue, true);
    }

    private sealed class DeviceRow
    {
        public string LineName { get; init; } = string.Empty;

        public string DeviceName { get; init; } = string.Empty;

        public string LatestStatus { get; init; } = string.Empty;

        public string AttentionLevel { get; init; } = string.Empty;

        public int PendingCount { get; init; }

        public string LatestCheckedAt { get; init; } = string.Empty;

        public string Inspector { get; init; } = string.Empty;
    }

    private sealed class AttentionRow
    {
        public string DeviceName { get; init; } = string.Empty;

        public string InspectionItem { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string CheckedAt { get; init; } = string.Empty;

        public string Detail { get; init; } = string.Empty;
    }
}
