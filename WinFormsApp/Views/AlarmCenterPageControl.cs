using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class AlarmCenterPageControl : UserControl
{
    private static readonly Color PageBackground = PageChrome.PageBackground;
    private static readonly Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static readonly Color TextPrimaryColor = PageChrome.TextPrimary;
    private static readonly Color TextSecondaryColor = PageChrome.TextSecondary;
    private static readonly Color AccentBlue = PageChrome.AccentBlue;
    private static readonly Color WarningColor = PageChrome.AccentOrange;
    private static readonly Color DangerColor = PageChrome.AccentRed;
    private static readonly Color SuccessColor = PageChrome.AccentGreen;

    private readonly string _account;
    private readonly InspectionController _inspectionController;
    private readonly Label _generatedAtLabel;
    private Label _pendingValueLabel = null!;
    private Label _abnormalValueLabel = null!;
    private Label _warningValueLabel = null!;
    private Label _closedValueLabel = null!;
    private DataGridView _pendingGrid = null!;
    private DataGridView _historyGrid = null!;
    private Label _historyEmptyLabel = null!;

    private IReadOnlyList<AlarmRow> _pendingRows = Array.Empty<AlarmRow>();
    private IReadOnlyList<AlarmRow> _historyRows = Array.Empty<AlarmRow>();

    public event EventHandler? DataChanged;

    public AlarmCenterPageControl(string account, InspectionController inspectionController)
    {
        _account = account;
        _inspectionController = inspectionController;
        Dock = DockStyle.Fill;
        BackColor = PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        var refreshButton = CreateRefreshButton();
        refreshButton.Click += (_, _) => RefreshData();
        var closeButton = CreateCloseButton();
        closeButton.Click += (_, _) => CloseSelectedAlarm();

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

        var header = BuildHeader(refreshButton, closeButton);
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
            .Where(record => !record.IsRevoked && record.Status != InspectionStatus.Normal)
            .OrderByDescending(record => record.CheckedAtValue)
            .ToList();

        _pendingRows = records
            .Where(record => !record.IsClosed)
            .Select(record => ToRow(record, "待处理"))
            .ToList();

        _historyRows = records
            .Where(record => record.IsClosed)
            .Take(10)
            .Select(record => ToRow(record, "已闭环"))
            .ToList();

        _pendingValueLabel.Text = _pendingRows.Count.ToString();
        _abnormalValueLabel.Text = _pendingRows.Count(row => row.StatusText == "异常").ToString();
        _warningValueLabel.Text = _pendingRows.Count(row => row.StatusText == "预警").ToString();
        _closedValueLabel.Text = _historyRows.Count.ToString();
        _generatedAtLabel.Text = $"最近刷新 {dashboard.GeneratedAt:yyyy-MM-dd HH:mm:ss}";

        _pendingGrid.DataSource = _pendingRows.ToList();
        _historyGrid.DataSource = _historyRows.ToList();
        _historyGrid.Visible = _historyRows.Count > 0;
        _historyEmptyLabel.Visible = _historyRows.Count == 0;
    }

    public void ApplyTheme()
    {
        BackColor = PageBackground;
        PageChrome.ApplyGridTheme(_pendingGrid);
        PageChrome.ApplyGridTheme(_historyGrid);
        Invalidate(true);
    }

    private Control BuildHeader(Button refreshButton, Button closeButton)
    {
        return PageChrome.CreatePageHeader(
            "报警中心",
            "这里看完整告警列表和处理状态，首页后面只挑重点摘要。",
            _generatedAtLabel,
            refreshButton,
            closeButton);
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

        _pendingValueLabel = PageChrome.CreateValueLabel();
        _abnormalValueLabel = PageChrome.CreateValueLabel();
        _warningValueLabel = PageChrome.CreateValueLabel();
        _closedValueLabel = PageChrome.CreateValueLabel();

        layout.Controls.Add(PageChrome.CreateMetricCard("待处理总数", DangerColor, _pendingValueLabel, PageChrome.CreateNoteLabel("未闭环的预警和异常")), 0, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("异常", DangerColor, _abnormalValueLabel, PageChrome.CreateNoteLabel("优先级最高，建议先处理")), 1, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("预警", WarningColor, _warningValueLabel, PageChrome.CreateNoteLabel("可安排巡检复核")), 2, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("最近闭环", SuccessColor, _closedValueLabel, PageChrome.CreateNoteLabel("方便回看最近处理结果"), Padding.Empty), 3, 0);
        return layout;
    }

    private Control BuildBodyArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));

        _pendingGrid = CreateGrid();
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.CheckedAt), "时间", 140));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.LineName), "产线", 90));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.DeviceName), "设备", 120));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.InspectionItem), "问题项", 140));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.StatusText), "状态", 70));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.ProcessingState), "处理状态", 100));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.Inspector), "巡检人", 90));
        _pendingGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.Remark), "备注", 220));
        _pendingGrid.CellFormatting += GridOnCellFormatting;
        _pendingGrid.CellDoubleClick += (_, _) => CloseSelectedAlarm();

        var pendingPanel = PageChrome.CreateSectionShell(
            "待处理告警",
            "这里看完整清单，确认、闭环动作仍通过巡检流程完成。",
            out _,
            _pendingGrid,
            new Padding(0, 0, 0, 12));

        _historyGrid = CreateGrid();
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.CheckedAt), "时间", 140));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.LineName), "产线", 90));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.DeviceName), "设备", 120));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.InspectionItem), "问题项", 140));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.StatusText), "状态", 70));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.ProcessingState), "处理状态", 100));
        _historyGrid.Columns.Add(CreateTextColumn(nameof(AlarmRow.Remark), "处理说明", 320));
        _historyGrid.CellFormatting += GridOnCellFormatting;

        _historyEmptyLabel = PageChrome.CreateEmptyStateLabel("暂无闭环记录");
        var historyBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        historyBody.Controls.Add(_historyGrid);
        historyBody.Controls.Add(_historyEmptyLabel);

        var historyPanel = PageChrome.CreateSectionShell(
            "最近闭环",
            "只保留最近处理完的记录，方便回看。",
            out _,
            historyBody,
            Padding.Empty);

        layout.Controls.Add(pendingPanel, 0, 0);
        layout.Controls.Add(historyPanel, 0, 1);
        return layout;
    }

    private void GridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.Value is not string text)
        {
            return;
        }

        var propertyName = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (propertyName == nameof(AlarmRow.StatusText))
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

        if (propertyName == nameof(AlarmRow.ProcessingState))
        {
            var cellStyle = e.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            cellStyle.ForeColor = text switch
            {
                "待处理" => DangerColor,
                "已闭环" => SuccessColor,
                _ => TextSecondaryColor
            };
        }
    }

    private AlarmRow? GetSelectedPendingRow()
    {
        return _pendingGrid.CurrentRow?.DataBoundItem as AlarmRow;
    }

    private void CloseSelectedAlarm()
    {
        var row = GetSelectedPendingRow();
        if (row is null)
        {
            MessageBox.Show(this, "请先选中要闭环的告警。", "告警闭环", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var closureRemark = ShowActionInputDialog(
            "告警闭环",
            $"请填写 {row.DeviceName} / {row.InspectionItem} 的处理说明。",
            "提交闭环");
        if (closureRemark is null)
        {
            return;
        }

        try
        {
            CloseAlarm(row.RecordId, closureRemark);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "告警闭环失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CloseAlarm(Guid recordId, string closureRemark)
    {
        _inspectionController.Close(recordId, _account, closureRemark);
        RefreshData();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? ShowActionInputDialog(string title, string description, string confirmText)
    {
        using var window = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(520, 320),
            MinimumSize = new Size(480, 300),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = PageBackground,
            Font = Font,
            ShowIcon = false,
            ShowInTaskbar = false
        };

        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = PageChrome.PagePadding
        };

        var card = PageChrome.CreateSurfacePanel(new Padding(18));
        card.Dock = DockStyle.Fill;

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
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var descriptionLabel = PageChrome.CreateTextLabel(description, 9F, FontStyle.Regular, TextSecondaryColor, new Padding(0, 0, 0, 12));
        var inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            BackColor = PageChrome.InputBackground,
            ForeColor = TextPrimaryColor,
            BorderStyle = BorderStyle.FixedSingle
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Color.Transparent
        };

        var confirmButton = PageChrome.CreateActionButton(confirmText, AccentBlue, true);
        confirmButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(inputBox.Text))
            {
                MessageBox.Show(window, "请先填写处理说明。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            window.DialogResult = DialogResult.OK;
            window.Close();
        };

        var cancelButton = PageChrome.CreateActionButton("取消", DangerColor, false);
        cancelButton.Margin = new Padding(10, 0, 0, 0);
        cancelButton.Click += (_, _) =>
        {
            window.DialogResult = DialogResult.Cancel;
            window.Close();
        };

        buttonPanel.Controls.Add(confirmButton);
        buttonPanel.Controls.Add(cancelButton);

        layout.Controls.Add(descriptionLabel, 0, 0);
        layout.Controls.Add(inputBox, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        card.Controls.Add(layout);
        shell.Controls.Add(card);
        window.Controls.Add(shell);

        return window.ShowDialog(this) == DialogResult.OK
            ? inputBox.Text.Trim()
            : null;
    }

    private static AlarmRow ToRow(InspectionRecordViewModel record, string processingState)
    {
        return new AlarmRow
        {
            RecordId = record.Id,
            CheckedAt = record.CheckedAtValue.ToString("MM-dd HH:mm"),
            LineName = record.LineName,
            DeviceName = record.DeviceName,
            InspectionItem = record.InspectionItem,
            StatusText = record.StatusText,
            ProcessingState = processingState,
            Inspector = record.Inspector,
            Remark = string.IsNullOrWhiteSpace(record.ActionRemark) ? record.Remark : record.ActionRemark
        };
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

    private static Button CreateRefreshButton()
    {
        return PageChrome.CreateActionButton("刷新告警", AccentBlue, true);
    }

    private static Button CreateCloseButton()
    {
        return PageChrome.CreateActionButton("闭环选中告警", DangerColor, false);
    }

    private sealed class AlarmRow
    {
        public Guid RecordId { get; init; }

        public string CheckedAt { get; init; } = string.Empty;

        public string LineName { get; init; } = string.Empty;

        public string DeviceName { get; init; } = string.Empty;

        public string InspectionItem { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public string ProcessingState { get; init; } = string.Empty;

        public string Inspector { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;
    }
}
