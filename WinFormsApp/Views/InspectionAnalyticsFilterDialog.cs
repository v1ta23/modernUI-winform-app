using App.Core.Models;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class InspectionAnalyticsFilterDialog : Form
{
    private readonly ComboBox _lineComboBox;
    private readonly ComboBox _statusComboBox;
    private readonly CheckBox _pendingOnlyCheckBox;
    private readonly CheckBox _dateFilterCheckBox;
    private readonly DateTimePicker _startDatePicker;
    private readonly DateTimePicker _endDatePicker;

    public InspectionFilterViewModel Filter { get; private set; }

    public InspectionAnalyticsFilterDialog(
        InspectionFilterViewModel currentFilter,
        IReadOnlyList<string> lineOptions)
    {
        Filter = currentFilter;

        Text = "风险看板筛选";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 420);
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _lineComboBox = CreateComboBox();
        _lineComboBox.Items.Add("全部产线");
        foreach (var line in lineOptions.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            _lineComboBox.Items.Add(line);
        }

        _lineComboBox.SelectedIndex = FindLineIndex(currentFilter.LineName);

        _statusComboBox = CreateComboBox();
        _statusComboBox.Items.AddRange(["全部状态", "正常", "预警", "异常"]);
        _statusComboBox.SelectedIndex = currentFilter.Status switch
        {
            InspectionStatus.Normal => 1,
            InspectionStatus.Warning => 2,
            InspectionStatus.Abnormal => 3,
            _ => 0
        };

        _pendingOnlyCheckBox = CreateCheckBox("只看待闭环");
        _pendingOnlyCheckBox.Checked = currentFilter.PendingOnly;

        _dateFilterCheckBox = CreateCheckBox("启用日期范围");
        _dateFilterCheckBox.Checked = currentFilter.StartTime.HasValue || currentFilter.EndTime.HasValue;
        _dateFilterCheckBox.CheckedChanged += (_, _) => UpdateDatePickerState();

        _startDatePicker = CreateDatePicker();
        _endDatePicker = CreateDatePicker();
        _startDatePicker.Value = currentFilter.StartTime?.Date ?? DateTime.Today.AddDays(-7);
        _endDatePicker.Value = currentFilter.EndTime?.Date ?? DateTime.Today;
        UpdateDatePickerState();

        Controls.Add(BuildContent());
    }

    private Control BuildContent()
    {
        var shell = PageChrome.CreateSurfacePanel(new Padding(22), 16);
        shell.Margin = Padding.Empty;

        var titleLabel = PageChrome.CreateTextLabel(
            "筛选风险看板",
            15F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 0, 0, 6));
        var noteLabel = PageChrome.CreateTextLabel(
            "筛选会影响图表、AI 分析和巡检日报。",
            9F,
            FontStyle.Regular,
            PageChrome.TextMuted,
            new Padding(0, 0, 0, 14));

        var layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 11
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var index = 0; index < 10; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(noteLabel, 0, 1);
        layout.Controls.Add(CreateFieldLabel("产线"), 0, 2);
        layout.Controls.Add(_lineComboBox, 0, 3);
        layout.Controls.Add(CreateFieldLabel("状态"), 0, 4);
        layout.Controls.Add(_statusComboBox, 0, 5);
        layout.Controls.Add(_pendingOnlyCheckBox, 0, 6);
        layout.Controls.Add(_dateFilterCheckBox, 0, 7);
        layout.Controls.Add(BuildDateRow(), 0, 8);
        layout.Controls.Add(BuildActions(), 0, 9);

        shell.Controls.Add(layout);
        return shell;
    }

    private Control BuildDateRow()
    {
        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            WrapContents = false
        };
        var toLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = PageChrome.TextMuted,
            Margin = new Padding(8, 5, 8, 0),
            Text = "至"
        };
        layout.Controls.Add(_startDatePicker);
        layout.Controls.Add(toLabel);
        layout.Controls.Add(_endDatePicker);
        return layout;
    }

    private Control BuildActions()
    {
        var applyButton = PageChrome.CreateActionButton("应用筛选", PageChrome.AccentBlue, true);
        var resetButton = PageChrome.CreateActionButton("清空筛选", PageChrome.AccentRed, false);
        var cancelButton = PageChrome.CreateActionButton("取消", PageChrome.SurfaceBorder, false);
        resetButton.Margin = new Padding(10, 0, 0, 0);
        cancelButton.Margin = new Padding(10, 0, 0, 0);

        applyButton.Click += (_, _) => Confirm();
        resetButton.Click += (_, _) =>
        {
            Filter = new InspectionFilterViewModel();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = applyButton;
        CancelButton = cancelButton;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 18, 0, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };
        actions.Controls.Add(applyButton);
        actions.Controls.Add(cancelButton);
        actions.Controls.Add(resetButton);
        return actions;
    }

    private void Confirm()
    {
        if (_dateFilterCheckBox.Checked && _startDatePicker.Value.Date > _endDatePicker.Value.Date)
        {
            MessageBox.Show(this, "开始日期不能晚于结束日期。", "风险看板筛选", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var lineName = _lineComboBox.SelectedIndex > 0
            ? _lineComboBox.SelectedItem?.ToString() ?? string.Empty
            : string.Empty;
        var status = _statusComboBox.SelectedIndex switch
        {
            1 => InspectionStatus.Normal,
            2 => InspectionStatus.Warning,
            3 => InspectionStatus.Abnormal,
            _ => (InspectionStatus?)null
        };

        Filter = new InspectionFilterViewModel
        {
            LineName = lineName,
            Status = status,
            StartTime = _dateFilterCheckBox.Checked ? _startDatePicker.Value.Date : null,
            EndTime = _dateFilterCheckBox.Checked ? _endDatePicker.Value.Date.AddDays(1).AddTicks(-1) : null,
            PendingOnly = _pendingOnlyCheckBox.Checked
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private int FindLineIndex(string lineName)
    {
        if (string.IsNullOrWhiteSpace(lineName))
        {
            return 0;
        }

        for (var index = 1; index < _lineComboBox.Items.Count; index++)
        {
            if (string.Equals(_lineComboBox.Items[index]?.ToString(), lineName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    private void UpdateDatePickerState()
    {
        var enabled = _dateFilterCheckBox.Checked;
        _startDatePicker.Enabled = enabled;
        _endDatePicker.Enabled = enabled;
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            BackColor = PageChrome.InputBackground,
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = PageChrome.TextPrimary,
            Margin = new Padding(0, 0, 0, 8)
        };
    }

    private static DateTimePicker CreateDatePicker()
    {
        return new DateTimePicker
        {
            CalendarMonthBackground = PageChrome.InputBackground,
            CalendarTitleBackColor = PageChrome.SurfaceBackground,
            CalendarTitleForeColor = PageChrome.TextPrimary,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Font = new Font("Microsoft YaHei UI", 9F),
            Margin = Padding.Empty,
            Width = 138
        };
    }

    private static CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = PageChrome.TextMuted,
            Margin = new Padding(0, 8, 0, 6),
            Text = text
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return PageChrome.CreateTextLabel(
            text,
            9F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 8, 0, 6));
    }
}
