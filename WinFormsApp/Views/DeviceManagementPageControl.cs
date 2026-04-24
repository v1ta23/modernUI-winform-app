using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class DeviceManagementPageControl : UserControl, IInteractiveResizeAware
{
    private readonly DeviceManagementController _controller;
    private readonly Label _generatedAtLabel;
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly TextBox _keywordTextBox;
    private readonly ComboBox _lineFilterComboBox;
    private readonly ComboBox _statusFilterComboBox;
    private readonly DataGridView _deviceGrid;
    private readonly TextBox _deviceCodeTextBox;
    private readonly TextBox _lineNameTextBox;
    private readonly TextBox _deviceNameTextBox;
    private readonly TextBox _locationTextBox;
    private readonly TextBox _ownerTextBox;
    private readonly TextBox _communicationAddressTextBox;
    private readonly ComboBox _editorStatusComboBox;
    private readonly Button _remarkButton;
    private readonly Label _totalValueLabel;
    private readonly Label _activeValueLabel;
    private readonly Label _maintenanceValueLabel;
    private readonly Label _linkedValueLabel;
    private readonly Label _editorHintLabel;

    private IReadOnlyList<DeviceRowViewModel> _devices = Array.Empty<DeviceRowViewModel>();
    private Guid? _editingId;
    private string _remarkDraft = string.Empty;
    private bool _isRefreshing;

    public event EventHandler? DataChanged;

    public event Action<DeviceCommunicationPresetViewModel>? CommunicationTestRequested;

    public DeviceManagementPageControl(DeviceManagementController controller)
    {
        _controller = controller;
        Dock = DockStyle.Fill;
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);
        Padding = PageChrome.PagePadding;

        _generatedAtLabel = PageChrome.CreateInfoLabel();
        _keywordTextBox = CreateTextBox("设备名 / 编号 / 负责人");
        _lineFilterComboBox = CreateComboBox();
        _statusFilterComboBox = CreateComboBox();
        _deviceGrid = CreateGrid();
        _deviceCodeTextBox = CreateTextBox("可留空，系统自动生成");
        _lineNameTextBox = CreateTextBox("例如：一线");
        _deviceNameTextBox = CreateTextBox("例如：冲压机 A01");
        _locationTextBox = CreateTextBox("例如：一线前段");
        _ownerTextBox = CreateTextBox("例如：张磊");
        _communicationAddressTextBox = CreateTextBox("例如：tcp://192.168.10.21:9001");
        _editorStatusComboBox = CreateComboBox();
        _remarkButton = PageChrome.CreateActionButton("备注", PageChrome.AccentCyan, false);
        _totalValueLabel = PageChrome.CreateValueLabel();
        _activeValueLabel = PageChrome.CreateValueLabel();
        _maintenanceValueLabel = PageChrome.CreateValueLabel();
        _linkedValueLabel = PageChrome.CreateValueLabel();
        _editorHintLabel = PageChrome.CreateNoteLabel("选中左侧设备后可编辑；点“新增设备”会清空表单。");

        ConfigureStatusOptions();
        ConfigureGrid();

        var refreshButton = PageChrome.CreateActionButton("刷新", PageChrome.AccentBlue, false);
        var newButton = PageChrome.CreateActionButton("新增设备", PageChrome.AccentGreen, true);
        var saveButton = PageChrome.CreateActionButton("保存设备", PageChrome.AccentBlue, true);
        var communicationTestButton = PageChrome.CreateActionButton("通信测试", PageChrome.AccentCyan, false);
        var deleteButton = PageChrome.CreateActionButton("删除设备", PageChrome.AccentRed, false);

        refreshButton.Click += (_, _) => RefreshData();
        newButton.Click += (_, _) => StartNewDevice();
        saveButton.Click += (_, _) => SaveCurrentDevice();
        communicationTestButton.Click += (_, _) => RequestCommunicationTest();
        deleteButton.Click += (_, _) => DeleteCurrentDevice();
        _remarkButton.Click += (_, _) => ShowRemarkDialog();
        _keywordTextBox.TextChanged += (_, _) => RefreshData();
        _lineFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_isRefreshing)
            {
                RefreshData();
            }
        };
        _statusFilterComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_isRefreshing)
            {
                RefreshData();
            }
        };
        _deviceGrid.SelectionChanged += (_, _) => FillEditorFromSelection();
        UpdateRemarkPreview();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = PageChrome.CreatePageHeader(
            "设备台账",
            "先把设备管起来：产线、编号、负责人、通信地址都在这里维护。",
            _generatedAtLabel,
            refreshButton,
            newButton,
            saveButton,
            communicationTestButton,
            deleteButton);
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildSummaryArea(), 0, 1);
        root.Controls.Add(BuildWorkspaceArea(), 0, 2);

        _layoutRoot = root;
        Controls.Add(root);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageChrome.PageBackground);
        _layoutRoot.BringToFront();
        ApplyTheme();
        Load += (_, _) => RefreshData();
    }

    public void RefreshData()
    {
        var selectedId = _editingId;
        var dashboard = _controller.Load(new DeviceFilterViewModel
        {
            Keyword = _keywordTextBox.Text,
            LineName = _lineFilterComboBox.SelectedItem as string ?? string.Empty,
            Status = (_statusFilterComboBox.SelectedItem as StatusOption)?.Status
        });

        _devices = dashboard.Devices;
        _totalValueLabel.Text = dashboard.TotalCount.ToString();
        _activeValueLabel.Text = dashboard.ActiveCount.ToString();
        _maintenanceValueLabel.Text = dashboard.MaintenanceCount.ToString();
        _linkedValueLabel.Text = dashboard.CommunicationLinkedCount.ToString();
        _generatedAtLabel.Text = $"最近刷新 {dashboard.GeneratedAt:yyyy-MM-dd HH:mm:ss}";

        ResetLineOptions(dashboard.LineOptions);
        _deviceGrid.DataSource = dashboard.Devices.ToList();
        RestoreSelection(selectedId);
        if (_deviceGrid.CurrentRow is null)
        {
            StartNewDevice();
        }
    }

    public void ApplyTheme()
    {
        BackColor = PageChrome.PageBackground;
        PageChrome.ApplyGridTheme(_deviceGrid);
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
        _layoutRoot.PerformLayout();
        PerformLayout();
        Invalidate(true);
        Update();
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

        layout.Controls.Add(PageChrome.CreateMetricCard("设备总数", PageChrome.AccentBlue, _totalValueLabel, PageChrome.CreateNoteLabel("已纳入台账的设备")), 0, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("运行中", PageChrome.AccentGreen, _activeValueLabel, PageChrome.CreateNoteLabel("可安排巡检和监控")), 1, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("维护中", PageChrome.AccentOrange, _maintenanceValueLabel, PageChrome.CreateNoteLabel("需要关注的设备")), 2, 0);
        layout.Controls.Add(PageChrome.CreateMetricCard("已填通信", PageChrome.AccentCyan, _linkedValueLabel, PageChrome.CreateNoteLabel("后续可接实时通信"), new Padding(0)), 3, 0);
        return layout;
    }

    private Control BuildWorkspaceArea()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        workspace.Controls.Add(PageChrome.CreateSectionShell("设备列表", "按产线、状态和关键词找设备。", out _, BuildListArea(), new Padding(0, 0, 12, 0)), 0, 0);
        workspace.Controls.Add(PageChrome.CreateSectionShell("设备信息", _editorHintLabel, BuildEditorArea(), new Padding(0), BuildRemarkHeaderAction()), 1, 0);
        return workspace;
    }

    private Control BuildListArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var filterBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
        filterBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
        filterBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _keywordTextBox.Margin = new Padding(0, 0, 10, 10);
        _lineFilterComboBox.Margin = new Padding(0, 0, 10, 10);
        _statusFilterComboBox.Margin = new Padding(0, 0, 0, 10);
        filterBar.Controls.Add(_keywordTextBox, 0, 0);
        filterBar.Controls.Add(_lineFilterComboBox, 1, 0);
        filterBar.Controls.Add(_statusFilterComboBox, 2, 0);

        layout.Controls.Add(filterBar, 0, 0);
        layout.Controls.Add(_deviceGrid, 0, 1);
        return layout;
    }

    private Control BuildEditorArea()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 0,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AddEditorRow(layout, "设备编号", _deviceCodeTextBox);
        AddEditorRow(layout, "产线", _lineNameTextBox);
        AddEditorRow(layout, "设备名称", _deviceNameTextBox);
        AddEditorRow(layout, "位置", _locationTextBox);
        AddEditorRow(layout, "负责人", _ownerTextBox);
        AddEditorRow(layout, "通信地址", _communicationAddressTextBox);
        AddEditorRow(layout, "状态", _editorStatusComboBox);
        return layout;
    }

    private Control BuildRemarkHeaderAction()
    {
        _remarkButton.Text = "备注";
        return _remarkButton;
    }

    private static void AddEditorRow(TableLayoutPanel layout, string labelText, Control input, bool fill = false)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var label = PageChrome.CreateNoteLabel(labelText, 8.6F, PageChrome.TextMuted);
        label.Dock = DockStyle.Fill;
        input.Dock = DockStyle.Fill;
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(input, 0, 1);

        var rowIndex = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100F) : new RowStyle(SizeType.Absolute, 64F));
        layout.Controls.Add(row, 0, rowIndex);
    }

    private void ConfigureGrid()
    {
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.DeviceCode), "编号", 92));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.LineName), "产线", 74));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.DeviceName), "设备", 124));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.StatusText), "状态", 74));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.Owner), "负责人", 82));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.CommunicationAddress), "通信地址", 140));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceRowViewModel.UpdatedAtText), "更新", 82));
    }

    private static DataGridView CreateGrid()
    {
        var grid = new BufferedDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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

    private static DataGridViewTextBoxColumn CreateColumn(string propertyName, string headerText, float fillWeight)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = fillWeight,
            MinimumWidth = 64
        };
    }

    private static TextBox CreateTextBox(string placeholder)
    {
        return new TextBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            Margin = Padding.Empty,
            PlaceholderText = placeholder
        };
    }

    private static ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            BackColor = PageChrome.InputBackground,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            Margin = Padding.Empty
        };
    }

    private void ShowRemarkDialog()
    {
        using var dialog = new Form
        {
            BackColor = PageChrome.SurfaceBackground,
            ClientSize = new Size(420, 300),
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            Text = "设备备注"
        };

        var hintLabel = PageChrome.CreateNoteLabel("备注会放回表单，点“保存设备”后入库。", 8.8F, PageChrome.TextSecondary);
        hintLabel.Margin = new Padding(0, 0, 0, 10);

        var input = CreateTextBox("例如：每周检查一次接线端子");
        input.AcceptsReturn = true;
        input.Dock = DockStyle.Fill;
        input.Multiline = true;
        input.ScrollBars = ScrollBars.Vertical;
        input.Text = _remarkDraft;
        input.WordWrap = true;

        var actionBar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 12, 0, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };

        var saveButton = PageChrome.CreateActionButton("确认备注", PageChrome.AccentBlue, true);
        var cancelButton = PageChrome.CreateActionButton("取消", PageChrome.SurfaceBorder, false);
        var clearButton = PageChrome.CreateActionButton("清空", PageChrome.AccentRed, false);

        saveButton.Click += (_, _) =>
        {
            _remarkDraft = input.Text.Trim();
            UpdateRemarkPreview();
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };
        clearButton.Click += (_, _) => input.Clear();

        saveButton.Margin = new Padding(10, 0, 0, 0);
        cancelButton.Margin = new Padding(10, 0, 0, 0);
        clearButton.Margin = Padding.Empty;
        actionBar.Controls.Add(saveButton);
        actionBar.Controls.Add(cancelButton);
        actionBar.Controls.Add(clearButton);

        var layout = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(hintLabel, 0, 0);
        layout.Controls.Add(input, 0, 1);
        layout.Controls.Add(actionBar, 0, 2);

        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;
        dialog.Controls.Add(layout);
        input.SelectionStart = input.TextLength;
        _ = dialog.ShowDialog(this);
    }

    private void UpdateRemarkPreview()
    {
        _remarkButton.Text = "备注";
    }

    private void ConfigureStatusOptions()
    {
        _statusFilterComboBox.Items.Clear();
        _statusFilterComboBox.Items.Add(new StatusOption("全部状态", null));
        _statusFilterComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Active.ToDisplayText(), ManagedDeviceStatus.Active));
        _statusFilterComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Maintenance.ToDisplayText(), ManagedDeviceStatus.Maintenance));
        _statusFilterComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Stopped.ToDisplayText(), ManagedDeviceStatus.Stopped));
        _statusFilterComboBox.SelectedIndex = 0;

        _editorStatusComboBox.Items.Clear();
        _editorStatusComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Active.ToDisplayText(), ManagedDeviceStatus.Active));
        _editorStatusComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Maintenance.ToDisplayText(), ManagedDeviceStatus.Maintenance));
        _editorStatusComboBox.Items.Add(new StatusOption(ManagedDeviceStatus.Stopped.ToDisplayText(), ManagedDeviceStatus.Stopped));
        _editorStatusComboBox.SelectedIndex = 0;
    }

    private void ResetLineOptions(IReadOnlyList<string> lineOptions)
    {
        var selectedLine = _lineFilterComboBox.SelectedItem as string ?? string.Empty;
        _isRefreshing = true;
        _lineFilterComboBox.Items.Clear();
        _lineFilterComboBox.Items.Add(string.Empty);
        foreach (var line in lineOptions)
        {
            _lineFilterComboBox.Items.Add(line);
        }

        _lineFilterComboBox.SelectedItem = _lineFilterComboBox.Items.Contains(selectedLine)
            ? selectedLine
            : string.Empty;
        _isRefreshing = false;
    }

    private void RestoreSelection(Guid? selectedId)
    {
        if (!selectedId.HasValue && _deviceGrid.Rows.Count > 0)
        {
            _deviceGrid.Rows[0].Selected = true;
            _deviceGrid.CurrentCell = _deviceGrid.Rows[0].Cells[0];
            FillEditorFromSelection();
            return;
        }

        if (!selectedId.HasValue)
        {
            return;
        }

        var id = selectedId.Value;
        foreach (DataGridViewRow row in _deviceGrid.Rows)
        {
            if (row.DataBoundItem is DeviceRowViewModel device && device.Id == id)
            {
                row.Selected = true;
                _deviceGrid.CurrentCell = row.Cells[0];
                FillEditor(device);
                return;
            }
        }

        FillEditorFromSelection();
    }

    private void FillEditorFromSelection()
    {
        if (_isRefreshing || _deviceGrid.CurrentRow?.DataBoundItem is not DeviceRowViewModel device)
        {
            return;
        }

        FillEditor(device);
    }

    private void FillEditor(DeviceRowViewModel device)
    {
        _editingId = device.Id;
        _deviceCodeTextBox.Text = device.DeviceCode;
        _lineNameTextBox.Text = device.LineName;
        _deviceNameTextBox.Text = device.DeviceName;
        _locationTextBox.Text = device.Location;
        _ownerTextBox.Text = device.Owner;
        _communicationAddressTextBox.Text = device.CommunicationAddress;
        _remarkDraft = device.Remark;
        UpdateRemarkPreview();
        SelectEditorStatus(device.Status);
        _editorHintLabel.Text = $"正在编辑：{device.LineName} / {device.DeviceName}";
    }

    private void StartNewDevice()
    {
        _editingId = null;
        _deviceCodeTextBox.Clear();
        _lineNameTextBox.Clear();
        _deviceNameTextBox.Clear();
        _locationTextBox.Clear();
        _ownerTextBox.Clear();
        _communicationAddressTextBox.Clear();
        _remarkDraft = string.Empty;
        UpdateRemarkPreview();
        SelectEditorStatus(ManagedDeviceStatus.Active);
        _editorHintLabel.Text = "新增设备：产线和设备名称必填，编号可留空。";
        _deviceNameTextBox.Focus();
    }

    private void SaveCurrentDevice()
    {
        try
        {
            var saved = _controller.Save(new DeviceEditorViewModel
            {
                Id = _editingId,
                DeviceCode = _deviceCodeTextBox.Text,
                LineName = _lineNameTextBox.Text,
                DeviceName = _deviceNameTextBox.Text,
                Location = _locationTextBox.Text,
                Owner = _ownerTextBox.Text,
                CommunicationAddress = _communicationAddressTextBox.Text,
                Status = GetEditorStatus(),
                Remark = _remarkDraft
            });
            _editingId = saved.Id;
            RefreshData();
            _editorHintLabel.Text = $"已保存：{saved.LineName} / {saved.DeviceName}";
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteCurrentDevice()
    {
        if (!_editingId.HasValue)
        {
            MessageBox.Show(this, "先从左侧选一个设备。", "删除设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            "确定删除这个设备吗？这只会删除设备台账，不会删除历史巡检记录。",
            "删除设备",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _controller.Delete(_editingId.Value);
            _editingId = null;
            RefreshData();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RequestCommunicationTest()
    {
        var preset = BuildCommunicationPreset();
        if (preset is null)
        {
            MessageBox.Show(this, "请先在左侧选择设备。", "通信测试", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(preset.CommunicationAddress))
        {
            MessageBox.Show(this, "当前设备未填写通信地址，请先在右侧补充后再测试。", "通信测试", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CommunicationTestRequested?.Invoke(preset);
    }

    private DeviceCommunicationPresetViewModel? BuildCommunicationPreset()
    {
        if (!_editingId.HasValue)
        {
            return null;
        }

        return new DeviceCommunicationPresetViewModel
        {
            LineName = _lineNameTextBox.Text.Trim(),
            DeviceName = _deviceNameTextBox.Text.Trim(),
            CommunicationAddress = _communicationAddressTextBox.Text.Trim()
        };
    }

    private ManagedDeviceStatus GetEditorStatus()
    {
        return (_editorStatusComboBox.SelectedItem as StatusOption)?.Status ?? ManagedDeviceStatus.Active;
    }

    private void SelectEditorStatus(ManagedDeviceStatus status)
    {
        foreach (var item in _editorStatusComboBox.Items)
        {
            if (item is StatusOption option && option.Status == status)
            {
                _editorStatusComboBox.SelectedItem = option;
                return;
            }
        }

        _editorStatusComboBox.SelectedIndex = 0;
    }

    private sealed class StatusOption
    {
        public StatusOption(string label, ManagedDeviceStatus? status)
        {
            Label = label;
            Status = status;
        }

        public string Label { get; }

        public ManagedDeviceStatus? Status { get; }

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
        }
    }
}
