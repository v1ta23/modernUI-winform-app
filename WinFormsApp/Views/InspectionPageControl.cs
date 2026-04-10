using System.Drawing;
using System.Drawing.Drawing2D;
using App.Core.Models;
using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed partial class InspectionPageControl : UserControl
{
    private static Color PageBackground = PageChrome.PageBackground;
    private static Color SurfaceBackground = PageChrome.SurfaceBackground;
    private static Color SurfaceBorder = PageChrome.SurfaceBorder;
    private static Color InputBackground = PageChrome.InputBackground;
    private static Color TextPrimaryColor = PageChrome.TextPrimary;
    private static Color TextSecondaryColor = PageChrome.TextSecondary;
    private static Color TextMutedColor = PageChrome.TextMuted;
    private static Color AccentBlue = PageChrome.AccentBlue;

    private readonly InspectionController _controller;
    private readonly string _account;

    private readonly ComboBox _entryLineCombo;
    private readonly ComboBox _entryTemplateCombo;
    private readonly TextBox _entryDeviceTextBox;
    private readonly TextBox _entryItemTextBox;
    private readonly TextBox _entryInspectorTextBox;
    private readonly ComboBox _entryStatusCombo;
    private readonly NumericUpDown _entryMeasuredValueInput;
    private readonly DateTimePicker _entryCheckedAtPicker;
    private readonly TextBox _entryRemarkTextBox;
    private readonly Label _entryFeedbackLabel;

    private readonly TextBox _filterKeywordTextBox;
    private readonly TextBox _filterDeviceTextBox;
    private readonly ComboBox _filterLineCombo;
    private readonly ComboBox _filterStatusCombo;
    private readonly CheckBox _filterIncludeRevokedCheckBox;
    private readonly DateTimePicker _filterStartPicker;
    private readonly DateTimePicker _filterEndPicker;

    private readonly Label _refreshLabel;
    private readonly Button _toggleEntryPanelButton;
    private readonly Button _manageTemplatesButton;
    private readonly Button _toggleChartsButton;
    private readonly Button _saveTemplateButton;
    private readonly Label _totalValueLabel;
    private readonly Label _normalValueLabel;
    private readonly Label _warningValueLabel;
    private readonly Label _abnormalValueLabel;
    private readonly Label _passRateValueLabel;

    private readonly DataGridView _recordsGrid;
    private readonly Panel _trendChart;
    private readonly Panel _statusChart;
    private readonly Control _layoutRoot;
    private readonly PictureBox _resizeSnapshotBox;
    private Panel? _headerCard;
    private TableLayoutPanel? _headerLayout;
    private Panel? _headerTitlePanel;
    private Panel? _headerRightPanel;
    private FlowLayoutPanel? _headerActionPanel;
    private Label? _headerTitleLabel;
    private Label? _headerSubtitleLabel;
    private TableLayoutPanel? _filterLayout;
    private Control? _filterKeywordBlock;
    private Control? _filterDeviceBlock;
    private Control? _filterLineBlock;
    private Control? _filterStatusBlock;
    private Control? _filterStartBlock;
    private Control? _filterEndBlock;
    private Control? _filterActionBlock;
    private FlowLayoutPanel? _filterActionPanel;
    private InspectionDashboardViewModel _currentDashboard = new();
    private IReadOnlyList<InspectionTemplateViewModel> _currentTemplates = Array.Empty<InspectionTemplateViewModel>();
    private Control? _contentHost;
    private Control? _dashboardLayout;
    private Control? _metricsPanel;
    private Control? _chartsPanel;
    private SplitContainer? _chartsSplitContainer;
    private Label? _entryTitleLabel;
    private Label? _entrySubtitleLabel;
    private Button? _entrySaveButton;
    private Button? _entryResetButton;
    private Form? _entryWindow;
    private bool _isDisposingFloatingWindows;
    private bool _isInteractiveResize;
    private bool _suppressTemplateSelectionChanged;
    private bool _suppressPresetReset;
    private bool _pendingOnlyOverride;
    private bool _screenInitialized;
    private bool _hasDeferredPreset;
    private Guid? _editingRecordId;
    private Bitmap? _resizeSnapshot;
    private string _activePresetLabel = string.Empty;
    private string _deferredPresetLabel = string.Empty;
    private InspectionFilterViewModel _deferredPresetFilter = new();

    public event EventHandler? DataChanged;

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (BackColor.A == 255)
            {
                using var brush = new SolidBrush(BackColor);
                e.Graphics.FillRectangle(brush, ClientRectangle);
                return;
            }

            base.OnPaintBackground(e);
        }
    }

    private sealed class WorkspacePanel : Panel
    {
        public WorkspacePanel()
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
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var workspacePath = CardPanel.CreateRoundedPath(rect, 22);
            using var workspaceBrush = new SolidBrush(PageBackground);
            e.Graphics.FillPath(workspaceBrush, workspacePath);
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
            using var workspacePath = CardPanel.CreateRoundedPath(rect, 22);
            using var borderPen = new Pen(Color.FromArgb(108, SurfaceBorder), 1.2F);
            e.Graphics.DrawPath(borderPen, workspacePath);
        }
    }

    private sealed class CardPanel : Panel
    {
        public CardPanel()
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
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var cardPath = CreateRoundedPath(rect, 16);
            using var cardBrush = new SolidBrush(SurfaceBackground);
            e.Graphics.FillPath(cardBrush, cardPath);
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
            using var cardPath = CreateRoundedPath(rect, 16);
            using var pen = new Pen(SurfaceBorder, 1.2F);
            e.Graphics.DrawPath(pen, cardPath);
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

    private sealed class StatusOption
    {
        public StatusOption(string text, InspectionStatus? value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public InspectionStatus? Value { get; }

        public override string ToString() => Text;
    }

    private sealed class EntryTemplateOption
    {
        public EntryTemplateOption(string text, InspectionTemplateViewModel? template)
        {
            Text = text;
            Template = template;
        }

        public string Text { get; }

        public InspectionTemplateViewModel? Template { get; }

        public override string ToString() => Text;
    }

    public InspectionPageControl(string account, InspectionController controller)
    {
        _account = account;
        _controller = controller;

        Text = "点检记录中心";
        Dock = DockStyle.Fill;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = PageBackground;
        DoubleBuffered = true;

        _entryTemplateCombo = CreateDropDownListComboBox();
        _entryTemplateCombo.SelectedIndexChanged += OnEntryTemplateChanged;
        _entryLineCombo = CreateEditableComboBox();
        _entryDeviceTextBox = CreateTextBox();
        _entryItemTextBox = CreateTextBox();
        _entryInspectorTextBox = CreateTextBox();
        _entryStatusCombo = CreateDropDownListComboBox();
        _entryMeasuredValueInput = CreateMeasuredValueInput();
        _entryCheckedAtPicker = CreateDateTimePicker(false);
        _entryRemarkTextBox = CreateTextBox(multiline: true);
        _entryFeedbackLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = AccentBlue,
            Margin = new Padding(0, 4, 0, 0),
            Text = "录入后列表和图表会即时刷新。"
        };

        _filterKeywordTextBox = CreateTextBox();
        _filterDeviceTextBox = CreateTextBox();
        _filterDeviceTextBox.PlaceholderText = "\u8f93\u5165\u8bbe\u5907\u540d";
        _filterLineCombo = CreateDropDownListComboBox();
        _filterStatusCombo = CreateDropDownListComboBox();
        _filterIncludeRevokedCheckBox = CreateCheckBox("显示已撤回");
        _filterStartPicker = CreateDateTimePicker(true);
        _filterEndPicker = CreateDateTimePicker(true);
        AttachFilterPresetResetHandlers();

        _refreshLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextMutedColor,
            TextAlign = ContentAlignment.MiddleRight
        };
        _toggleEntryPanelButton = CreateSecondaryButton("新增点检");
        _toggleEntryPanelButton.Click += (_, _) => OpenEntryWindow();
        _manageTemplatesButton = CreateSecondaryButton("台账模板");
        _manageTemplatesButton.Click += (_, _) => OpenTemplateManager();
        _toggleChartsButton = CreateSecondaryButton("趋势分析");
        _toggleChartsButton.Click += (_, _) => ToggleChartsDrawer();
        _saveTemplateButton = CreateSecondaryButton("保存为模板");
        _saveTemplateButton.Click += OnSaveTemplateClicked;

        _totalValueLabel = CreateMetricValueLabel();
        _normalValueLabel = CreateMetricValueLabel();
        _warningValueLabel = CreateMetricValueLabel();
        _abnormalValueLabel = CreateMetricValueLabel();
        _passRateValueLabel = CreateMetricValueLabel();

        _recordsGrid = CreateRecordsGrid();
        InitializeRecordInteractions();
        _trendChart = CreateTrendCanvas();
        _statusChart = CreateStatusCanvas();

        _layoutRoot = BuildLayout();
        _resizeSnapshotBox = CreateResizeSnapshotBox();
        Controls.Add(_layoutRoot);
        Controls.Add(_resizeSnapshotBox);
        ApplyDarkVisualTree(this);
        Load += (_, _) => InitializeScreen();
        Disposed += (_, _) =>
        {
            DisposeResizeSnapshot();
            DisposeFloatingWindows();
        };
    }

    private void InitializeRecordInteractions()
    {
        var menu = new ContextMenuStrip();
        var editItem = new ToolStripMenuItem("编辑记录");
        var closeItem = new ToolStripMenuItem("异常闭环");
        var revokeItem = new ToolStripMenuItem("撤回记录");
        var deleteItem = new ToolStripMenuItem("删除记录");

        editItem.Click += (_, _) =>
        {
            var record = GetSelectedRecord();
            if (record is not null)
            {
                OpenEntryWindowForEdit(record);
            }
        };
        closeItem.Click += (_, _) => CloseSelectedRecord();
        revokeItem.Click += (_, _) => RevokeSelectedRecord();
        deleteItem.Click += (_, _) => DeleteSelectedRecord();

        menu.Items.AddRange([editItem, closeItem, revokeItem, deleteItem]);
        menu.Opening += (_, args) =>
        {
            var record = GetSelectedRecord();
            if (record is null)
            {
                args.Cancel = true;
                return;
            }

            editItem.Enabled = !record.IsRevoked;
            closeItem.Enabled = !record.IsRevoked && !record.IsClosed && record.Status != InspectionStatus.Normal;
            revokeItem.Enabled = !record.IsRevoked;
            deleteItem.Enabled = true;
        };

        _recordsGrid.ContextMenuStrip = menu;
        _recordsGrid.CellMouseDown += (_, args) =>
        {
            if (args.RowIndex < 0)
            {
                return;
            }

            _recordsGrid.ClearSelection();
            _recordsGrid.Rows[args.RowIndex].Selected = true;
            if (args.ColumnIndex >= 0)
            {
                _recordsGrid.CurrentCell = _recordsGrid.Rows[args.RowIndex].Cells[args.ColumnIndex];
            }
        };
        _recordsGrid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex < 0)
            {
                return;
            }

            var record = GetSelectedRecord();
            if (record is not null && !record.IsRevoked)
            {
                OpenEntryWindowForEdit(record);
            }
        };
        _recordsGrid.DataBindingComplete += (_, _) => ApplyRecordRowStyles();
    }

    private InspectionRecordViewModel? GetSelectedRecord()
    {
        return _recordsGrid.CurrentRow?.DataBoundItem as InspectionRecordViewModel;
    }

    private void ApplyRecordRowStyles()
    {
        foreach (DataGridViewRow row in _recordsGrid.Rows)
        {
            if (row.DataBoundItem is not InspectionRecordViewModel record)
            {
                continue;
            }

            if (record.IsRevoked)
            {
                row.DefaultCellStyle.ForeColor = TextMutedColor;
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(54, 60, 74);
                row.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
            }
            else
            {
                row.DefaultCellStyle.ForeColor = TextSecondaryColor;
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 56, 78);
                row.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
            }
        }
    }

    private void UpdateEntryModeUi()
    {
        var isEditing = _editingRecordId.HasValue;
        if (_entryWindow is not null)
        {
            _entryWindow.Text = isEditing ? "编辑点检记录" : "点检录入";
        }

        if (_entryTitleLabel is not null)
        {
            _entryTitleLabel.Text = isEditing ? "编辑点检记录" : "点检记录录入";
        }

        if (_entrySubtitleLabel is not null)
        {
            _entrySubtitleLabel.Text = isEditing
                ? "保存后会覆盖当前记录，异常闭环和撤回状态会保留。"
                : "新录入的数据会立即进入查询列表和监控图表。";
        }

        if (_entrySaveButton is not null)
        {
            _entrySaveButton.Text = isEditing ? "保存修改" : "保存记录";
        }

        if (_entryResetButton is not null)
        {
            _entryResetButton.Text = isEditing ? "退出编辑" : "重置表单";
        }
    }

    private void StartCreateEntry(bool keepLine = false, bool keepInspector = false)
    {
        _editingRecordId = null;
        UpdateEntryModeUi();
        ResetEntryForm(keepLine, keepInspector);
    }

    private void ShowEntryWindow()
    {
        _entryWindow ??= CreateEntryWindow();
        UpdateEntryModeUi();
        var owner = FindForm();
        CenterEntryWindow(owner);
        if (!_entryWindow.Visible)
        {
            if (owner is not null)
            {
                _entryWindow.Show(owner);
            }
            else
            {
                _entryWindow.Show();
            }
        }

        CenterEntryWindow(owner);
        _entryWindow.BringToFront();
        _entryWindow.Activate();
        if (_entryLineCombo.CanFocus)
        {
            _entryLineCombo.Focus();
        }
    }

    private void OpenEntryWindowForEdit(InspectionRecordViewModel record)
    {
        _editingRecordId = record.Id;
        _entryWindow ??= CreateEntryWindow();
        UpdateEntryModeUi();
        ResetEntryForm();

        _entryLineCombo.Text = record.LineName;
        _entryDeviceTextBox.Text = record.DeviceName;
        _entryItemTextBox.Text = record.InspectionItem;
        _entryInspectorTextBox.Text = record.Inspector;
        SelectStatus(record.Status);
        _entryMeasuredValueInput.Value = Math.Clamp(record.MeasuredValue, _entryMeasuredValueInput.Minimum, _entryMeasuredValueInput.Maximum);
        _entryCheckedAtPicker.Value = record.CheckedAtValue;
        _entryRemarkTextBox.Text = record.Remark;
        TrySelectTemplate(record.LineName, record.DeviceName, record.InspectionItem);
        _entryFeedbackLabel.ForeColor = AccentBlue;
        _entryFeedbackLabel.Text = "当前正在编辑所选记录。";
        ShowEntryWindow();
    }

    private void SelectStatus(InspectionStatus status)
    {
        for (var index = 0; index < _entryStatusCombo.Items.Count; index++)
        {
            if (_entryStatusCombo.Items[index] is StatusOption option && option.Value == status)
            {
                _entryStatusCombo.SelectedIndex = index;
                return;
            }
        }
    }

    private void UpdateTemplateOptions(IReadOnlyList<InspectionTemplateViewModel> templates)
    {
        _currentTemplates = templates;
        var selectedTemplateId = (_entryTemplateCombo.SelectedItem as EntryTemplateOption)?.Template?.Id;

        _suppressTemplateSelectionChanged = true;
        _entryTemplateCombo.BeginUpdate();
        _entryTemplateCombo.Items.Clear();
        _entryTemplateCombo.Items.Add(new EntryTemplateOption("不使用模板", null));
        foreach (var template in templates)
        {
            _entryTemplateCombo.Items.Add(new EntryTemplateOption(template.DisplayText, template));
        }
        _entryTemplateCombo.EndUpdate();

        var selected = _entryTemplateCombo.Items
            .OfType<EntryTemplateOption>()
            .FirstOrDefault(option => option.Template?.Id == selectedTemplateId);
        _entryTemplateCombo.SelectedItem = selected ?? _entryTemplateCombo.Items[0];
        _suppressTemplateSelectionChanged = false;
    }

    private void TrySelectTemplate(string lineName, string deviceName, string inspectionItem)
    {
        var matched = _entryTemplateCombo.Items
            .OfType<EntryTemplateOption>()
            .FirstOrDefault(option =>
                option.Template is not null &&
                string.Equals(option.Template.LineName, lineName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.Template.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(option.Template.InspectionItem, inspectionItem, StringComparison.OrdinalIgnoreCase));

        _suppressTemplateSelectionChanged = true;
        _entryTemplateCombo.SelectedItem = matched ?? _entryTemplateCombo.Items.Cast<object>().FirstOrDefault();
        _suppressTemplateSelectionChanged = false;
    }

    private void OnEntryTemplateChanged(object? sender, EventArgs e)
    {
        if (_suppressTemplateSelectionChanged)
        {
            return;
        }

        if ((_entryTemplateCombo.SelectedItem as EntryTemplateOption)?.Template is not { } template)
        {
            return;
        }

        _entryLineCombo.Text = template.LineName;
        _entryDeviceTextBox.Text = template.DeviceName;
        _entryItemTextBox.Text = template.InspectionItem;
        _entryInspectorTextBox.Text = template.DefaultInspector;
        if (string.IsNullOrWhiteSpace(_entryRemarkTextBox.Text))
        {
            _entryRemarkTextBox.Text = template.DefaultRemark;
        }
    }

    private void OpenTemplateManager()
    {
        using var window = new Form
        {
            Text = "台账模板",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(860, 520),
            MinimumSize = new Size(760, 420),
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            BackColor = PageBackground,
            Font = Font,
            ShowIcon = false,
            ShowInTaskbar = false
        };

        var shell = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(18)
        };

        var card = CreateSurfacePanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(18);

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            Text = "点检台账模板"
        };

        var tipLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = TextMutedColor,
            Padding = new Padding(0, 6, 0, 12),
            Text = "先在录入窗体填好产线、设备、点检项目和点检人，再点“保存为模板”。这里主要用于查看和删除模板。"
        };

        var grid = new BufferedDataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = InputBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
        grid.DefaultCellStyle.BackColor = SurfaceBackground;
        grid.DefaultCellStyle.ForeColor = TextSecondaryColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 56, 78);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
        grid.GridColor = SurfaceBorder;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionTemplateViewModel.LineName), HeaderText = "产线", Width = 100 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionTemplateViewModel.DeviceName), HeaderText = "设备名称", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionTemplateViewModel.InspectionItem), HeaderText = "点检项目", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionTemplateViewModel.DefaultInspector), HeaderText = "默认点检人", Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionTemplateViewModel.DefaultRemark), HeaderText = "默认备注", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var templateSource = new BindingSource();
        void ReloadTemplates()
        {
            templateSource.DataSource = _controller.GetTemplates().ToList();
            grid.DataSource = templateSource;
        }

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };

        var deleteButton = CreateSecondaryButton("删除模板");
        deleteButton.Click += (_, _) =>
        {
            if (grid.CurrentRow?.DataBoundItem is not InspectionTemplateViewModel template)
            {
                MessageBox.Show(window, "请先选中要删除的模板。", "删除模板", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    window,
                    $"确定删除模板：{template.DisplayText}？",
                    "删除模板",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _controller.DeleteTemplate(template.Id);
                RefreshDashboard();
                ReloadTemplates();
            }
            catch (Exception ex)
            {
                MessageBox.Show(window, ex.Message, "删除模板失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var closeButton = CreatePrimaryButton("关闭");
        closeButton.Click += (_, _) => window.Close();

        buttonPanel.Controls.Add(deleteButton);
        buttonPanel.Controls.Add(closeButton);

        card.Controls.Add(grid);
        card.Controls.Add(buttonPanel);
        card.Controls.Add(tipLabel);
        card.Controls.Add(titleLabel);
        shell.Controls.Add(card);
        window.Controls.Add(shell);
        ApplyDarkVisualTree(window);
        ReloadTemplates();
        window.ShowDialog(this);
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

        var shell = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(18)
        };

        var card = CreateSurfacePanel();
        card.Dock = DockStyle.Fill;
        card.Padding = new Padding(18);

        var descriptionLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = TextSecondaryColor,
            Text = description
        };

        var inputBox = CreateTextBox(multiline: true);
        inputBox.Dock = DockStyle.Fill;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };

        var confirmButton = CreatePrimaryButton(confirmText);
        var cancelButton = CreateSecondaryButton("取消");
        confirmButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(inputBox.Text))
            {
                MessageBox.Show(window, "请先填写说明。", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            window.DialogResult = DialogResult.OK;
            window.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            window.DialogResult = DialogResult.Cancel;
            window.Close();
        };

        buttonPanel.Controls.Add(confirmButton);
        buttonPanel.Controls.Add(cancelButton);

        card.Controls.Add(inputBox);
        card.Controls.Add(buttonPanel);
        card.Controls.Add(descriptionLabel);
        shell.Controls.Add(card);
        window.Controls.Add(shell);
        ApplyDarkVisualTree(window);

        return window.ShowDialog(this) == DialogResult.OK
            ? inputBox.Text.Trim()
            : null;
    }

    private void CloseSelectedRecord()
    {
        var record = GetSelectedRecord();
        if (record is null)
        {
            return;
        }

        var closureRemark = ShowActionInputDialog("异常闭环", $"请填写 {record.DeviceName} / {record.InspectionItem} 的闭环说明。", "提交闭环");
        if (closureRemark is null)
        {
            return;
        }

        try
        {
            _controller.Close(record.Id, _account, closureRemark);
            RefreshDashboard();
            NotifyDataChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "异常闭环失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RevokeSelectedRecord()
    {
        var record = GetSelectedRecord();
        if (record is null)
        {
            return;
        }

        var revokeReason = ShowActionInputDialog("撤回记录", $"请填写撤回 {record.DeviceName} / {record.InspectionItem} 的原因。", "确认撤回");
        if (revokeReason is null)
        {
            return;
        }

        try
        {
            _controller.Revoke(record.Id, _account, revokeReason);
            _filterIncludeRevokedCheckBox.Checked = true;
            RefreshDashboard();
            NotifyDataChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "撤回失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void DeleteSelectedRecord()
    {
        var record = GetSelectedRecord();
        if (record is null)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"确定永久删除 {record.DeviceName} / {record.InspectionItem}？这个操作不能撤销。",
                "删除记录",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _controller.Delete(record.Id);
            RefreshDashboard();
            NotifyDataChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "删除失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSaveTemplateClicked(object? sender, EventArgs e)
    {
        try
        {
            var template = new InspectionTemplateViewModel
            {
                LineName = _entryLineCombo.Text.Trim(),
                DeviceName = _entryDeviceTextBox.Text.Trim(),
                InspectionItem = _entryItemTextBox.Text.Trim(),
                DefaultInspector = _entryInspectorTextBox.Text.Trim(),
                DefaultRemark = _entryRemarkTextBox.Text.Trim()
            };
            _controller.SaveTemplate(template);
            _entryFeedbackLabel.ForeColor = Color.FromArgb(39, 174, 96);
            _entryFeedbackLabel.Text = "已保存为台账模板。";
            RefreshDashboard();
            TrySelectTemplate(template.LineName, template.DeviceName, template.InspectionItem);
        }
        catch (Exception ex)
        {
            _entryFeedbackLabel.ForeColor = Color.FromArgb(231, 76, 60);
            _entryFeedbackLabel.Text = ex.Message;
        }
    }

    private void OnResetEntryClicked(object? sender, EventArgs e)
    {
        if (_editingRecordId.HasValue)
        {
            StartCreateEntry();
            return;
        }

        ResetEntryForm();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_isInteractiveResize)
        {
            ApplyResponsiveLayout();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            return;
        }

        HideEntryWindow();
        SetChartsDrawerVisible(false);
    }

    private void InitializeScreen()
    {
        _suppressPresetReset = true;
        try
        {
            BindStatusOptions();
        }
        finally
        {
            _suppressPresetReset = false;
        }

        if (_hasDeferredPreset)
        {
            ApplyPresetValuesToControls(_deferredPresetFilter, _deferredPresetLabel);
            _hasDeferredPreset = false;
        }
        else
        {
            ResetFilters();
        }

        StartCreateEntry();
        _screenInitialized = true;
        RefreshDashboard();
        ApplyResponsiveLayout();
    }

    public void RefreshData()
    {
        RefreshDashboard();
    }

    public void ShowTodayRecords()
    {
        ApplyPresetFilter(
            new InspectionFilterViewModel
            {
                StartTime = DateTime.Today,
                EndTime = DateTime.Now
            },
            "今日巡检");
    }

    public void ShowPendingRecords()
    {
        ApplyPresetFilter(
            new InspectionFilterViewModel
            {
                PendingOnly = true
            },
            "待闭环");
    }

    public void ShowAbnormalRecords()
    {
        ApplyPresetFilter(
            new InspectionFilterViewModel
            {
                Status = InspectionStatus.Abnormal
            },
            "异常记录");
    }

    public void ShowImportedBatch(string batchKeyword, bool pendingOnly)
    {
        if (string.IsNullOrWhiteSpace(batchKeyword))
        {
            return;
        }

        ApplyPresetFilter(
            new InspectionFilterViewModel
            {
                Keyword = batchKeyword,
                PendingOnly = pendingOnly
            },
            pendingOnly ? "导入批次 / 待闭环" : "导入批次");
    }

    public void StartNewEntryFromHome()
    {
        OpenEntryWindow();
    }

    private void OpenEntryWindow()
    {
        StartCreateEntry();
        ShowEntryWindow();
    }

    private Form CreateEntryWindow()
    {
        var window = new Form
        {
            Text = "点检录入",
            StartPosition = FormStartPosition.Manual,
            Size = new Size(640, 900),
            MinimumSize = new Size(600, 820),
            FormBorderStyle = FormBorderStyle.SizableToolWindow,
            BackColor = PageBackground,
            Font = Font,
            ShowIcon = false,
            ShowInTaskbar = false
        };

        var shell = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(18)
        };
        shell.Controls.Add(BuildEntryPanel());
        window.Controls.Add(shell);
        ApplyDarkVisualTree(window);
        window.FormClosing += (_, args) =>
        {
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                window.Hide();
            }
        };
        return window;
    }

    private void CenterEntryWindow(Form? owner)
    {
        if (_entryWindow is null)
        {
            return;
        }

        var preferredBounds = owner?.Bounds ?? Screen.FromControl(this).WorkingArea;
        var workingArea = owner is not null
            ? Screen.FromControl(owner).WorkingArea
            : Screen.FromControl(this).WorkingArea;
        var x = preferredBounds.Left + Math.Max(0, (preferredBounds.Width - _entryWindow.Width) / 2);
        var y = preferredBounds.Top + Math.Max(0, (preferredBounds.Height - _entryWindow.Height) / 2);
        x = Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - _entryWindow.Width));
        y = Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - _entryWindow.Height));
        _entryWindow.Location = new Point(x, y);
    }

    private void HideEntryWindow()
    {
        if (_entryWindow?.Visible == true)
        {
            _entryWindow.Hide();
        }
    }

    private void DisposeFloatingWindows()
    {
        _isDisposingFloatingWindows = true;
        try
        {
            if (_entryWindow is not null && !_entryWindow.IsDisposed)
            {
                _entryWindow.Dispose();
            }
        }
        finally
        {
            _entryWindow = null;
            _isDisposingFloatingWindows = false;
        }
    }

    private void ToggleChartsDrawer()
    {
        SetChartsDrawerVisible(_chartsPanel?.Visible != true);
    }

    private void SetChartsDrawerVisible(bool visible)
    {
        if (_chartsPanel is null)
        {
            return;
        }

        _chartsPanel.Visible = visible;
        _toggleChartsButton.Text = visible ? "关闭趋势" : "趋势分析";
        if (visible)
        {
            UpdateChartsSplitDistance();
            _trendChart.Invalidate();
            _statusChart.Invalidate();
        }
    }

    public void BeginInteractiveResize()
    {
        if (_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = true;
        SuspendGridAutosize();
        CaptureResizeSnapshot();
        _layoutRoot.SuspendLayout();
        _layoutRoot.Visible = false;
        _resizeSnapshotBox.Visible = true;
        _resizeSnapshotBox.BringToFront();
    }

    public void EndInteractiveResize()
    {
        if (!_isInteractiveResize)
        {
            return;
        }

        _isInteractiveResize = false;
        _resizeSnapshotBox.Visible = false;
        _layoutRoot.Visible = true;
        _layoutRoot.ResumeLayout(true);
        DisposeResizeSnapshot();
        ResumeGridAutosize();
        ApplyResponsiveLayout();
        if (_chartsPanel?.Visible == true)
        {
            _trendChart.Invalidate();
            _statusChart.Invalidate();
        }

        Invalidate();
    }

    private void ApplyResponsiveLayout()
    {
        UpdateHeaderLayout();
        UpdateFilterLayout();
        UpdateChartsSplitDistance();
    }

    private void UpdateHeaderLayout()
    {
        if (_headerCard is null ||
            _headerLayout is null ||
            _headerTitlePanel is null ||
            _headerRightPanel is null ||
            _headerActionPanel is null ||
            _headerTitleLabel is null ||
            _headerSubtitleLabel is null)
        {
            return;
        }

        var stacked = ClientSize.Width < 1080;
        _headerCard.Height = stacked ? 150 : 112;

        _headerLayout.SuspendLayout();
        _headerLayout.Controls.Clear();
        _headerLayout.ColumnStyles.Clear();
        _headerLayout.RowStyles.Clear();

        if (stacked)
        {
            _headerLayout.ColumnCount = 1;
            _headerLayout.RowCount = 2;
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _headerLayout.Controls.Add(_headerTitlePanel, 0, 0);
            _headerLayout.Controls.Add(_headerRightPanel, 0, 1);
            _headerRightPanel.Margin = new Padding(0, 10, 0, 0);
            _headerActionPanel.FlowDirection = FlowDirection.LeftToRight;
            _refreshLabel.TextAlign = ContentAlignment.MiddleLeft;
        }
        else
        {
            _headerLayout.ColumnCount = 2;
            _headerLayout.RowCount = 1;
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
            _headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _headerLayout.Controls.Add(_headerTitlePanel, 0, 0);
            _headerLayout.Controls.Add(_headerRightPanel, 1, 0);
            _headerRightPanel.Margin = Padding.Empty;
            _headerActionPanel.FlowDirection = FlowDirection.RightToLeft;
            _refreshLabel.TextAlign = ContentAlignment.MiddleRight;
        }

        var availableTitleWidth = Math.Max(200, _headerTitlePanel.ClientSize.Width);
        _headerTitleLabel.MaximumSize = new Size(availableTitleWidth, 0);
        _headerSubtitleLabel.MaximumSize = new Size(availableTitleWidth, 0);
        _headerLayout.ResumeLayout(true);
    }

    private void UpdateFilterLayout()
    {
        if (_filterLayout is null ||
            _filterKeywordBlock is null ||
            _filterDeviceBlock is null ||
            _filterLineBlock is null ||
            _filterStatusBlock is null ||
            _filterStartBlock is null ||
            _filterEndBlock is null ||
            _filterActionBlock is null)
        {
            return;
        }

        var availableWidth = _filterLayout.Parent?.ClientSize.Width ?? ClientSize.Width;
        var columns = availableWidth >= 1180
            ? 4
            : availableWidth >= 820
                ? 3
                : availableWidth >= 560
                    ? 2
                    : 1;

        var filterBlocks = new[]
        {
            _filterKeywordBlock,
            _filterDeviceBlock,
            _filterLineBlock,
            _filterStatusBlock,
            _filterStartBlock,
            _filterEndBlock
        };

        _filterLayout.SuspendLayout();
        _filterLayout.Controls.Clear();
        _filterLayout.ColumnStyles.Clear();
        _filterLayout.RowStyles.Clear();
        _filterLayout.ColumnCount = columns;

        for (var column = 0; column < columns; column++)
        {
            _filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        var row = 0;
        var columnIndex = 0;
        foreach (var block in filterBlocks)
        {
            _filterLayout.Controls.Add(block, columnIndex, row);
            columnIndex++;
            if (columnIndex < columns)
            {
                continue;
            }

            columnIndex = 0;
            row++;
        }

        if (columnIndex != 0)
        {
            row++;
        }

        _filterLayout.Controls.Add(_filterActionBlock, 0, row);
        _filterLayout.SetColumnSpan(_filterActionBlock, columns);
        _filterLayout.RowCount = row + 1;

        for (var rowIndex = 0; rowIndex < _filterLayout.RowCount; rowIndex++)
        {
            _filterLayout.RowStyles.Add(new RowStyle());
        }

        if (_filterActionPanel is not null)
        {
            _filterActionPanel.FlowDirection = FlowDirection.LeftToRight;
        }

        _filterLayout.ResumeLayout(true);

        if (_filterActionPanel is not null &&
            _filterActionBlock.Width > 0 &&
            _filterActionPanel.GetPreferredSize(Size.Empty).Width > _filterActionBlock.Width)
        {
            _filterLayout.SuspendLayout();
            _filterActionPanel.FlowDirection = FlowDirection.TopDown;
            _filterLayout.ResumeLayout(true);
        }
    }

    private void UpdateChartsSplitDistance()
    {
        if (_chartsSplitContainer is null || _chartsPanel?.Visible != true)
        {
            return;
        }

        var height = _chartsSplitContainer.Height;
        if (height <= 0)
        {
            return;
        }

        var minAllowed = _chartsSplitContainer.Panel1MinSize;
        var maxAllowed = height - _chartsSplitContainer.Panel2MinSize - 1;
        if (maxAllowed <= minAllowed)
        {
            return;
        }

        var preferredMin = Math.Min(320, Math.Max(240, height / 3));
        var preferredMax = Math.Max(preferredMin, height - 240);
        var minDistance = Math.Max(minAllowed, Math.Min(preferredMin, maxAllowed));
        var maxDistance = Math.Max(minDistance, Math.Min(preferredMax, maxAllowed));
        var desired = Math.Clamp((int)(height * 0.56F), minDistance, maxDistance);
        if (Math.Abs(_chartsSplitContainer.SplitterDistance - desired) > 8)
        {
            _chartsSplitContainer.SplitterDistance = desired;
        }
    }

    private void BindStatusOptions()
    {
        _entryStatusCombo.Items.Clear();
        _entryStatusCombo.Items.Add(new StatusOption("正常", InspectionStatus.Normal));
        _entryStatusCombo.Items.Add(new StatusOption("预警", InspectionStatus.Warning));
        _entryStatusCombo.Items.Add(new StatusOption("异常", InspectionStatus.Abnormal));
        _entryStatusCombo.SelectedIndex = 0;

        _filterStatusCombo.Items.Clear();
        _filterStatusCombo.Items.Add(new StatusOption("全部", null));
        _filterStatusCombo.Items.Add(new StatusOption("正常", InspectionStatus.Normal));
        _filterStatusCombo.Items.Add(new StatusOption("预警", InspectionStatus.Warning));
        _filterStatusCombo.Items.Add(new StatusOption("异常", InspectionStatus.Abnormal));
        _filterStatusCombo.SelectedIndex = 0;
    }

    private void RefreshDashboard()
    {
        try
        {
            var dashboard = _controller.Load(BuildFilter());
            _suppressPresetReset = true;
            try
            {
                UpdateLineOptions(dashboard.LineOptions);
                UpdateTemplateOptions(dashboard.Templates);
                UpdateSummary(dashboard);
                UpdateGrid(dashboard.Records);
                UpdateCharts(dashboard);
            }
            finally
            {
                _suppressPresetReset = false;
            }

            UpdateRefreshLabel(dashboard.GeneratedAt);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateLineOptions(IReadOnlyList<string> lineOptions)
    {
        var filterCurrent = _filterLineCombo.SelectedItem?.ToString();
        var entryCurrent = _entryLineCombo.Text;

        _filterLineCombo.BeginUpdate();
        _filterLineCombo.Items.Clear();
        _filterLineCombo.Items.Add("全部");
        foreach (var line in lineOptions)
        {
            _filterLineCombo.Items.Add(line);
        }
        _filterLineCombo.EndUpdate();
        _filterLineCombo.SelectedItem = !string.IsNullOrWhiteSpace(filterCurrent) && _filterLineCombo.Items.Contains(filterCurrent)
            ? filterCurrent
            : _filterLineCombo.Items[0];

        _entryLineCombo.BeginUpdate();
        _entryLineCombo.Items.Clear();
        foreach (var line in lineOptions)
        {
            _entryLineCombo.Items.Add(line);
        }
        _entryLineCombo.EndUpdate();

        if (!string.IsNullOrWhiteSpace(entryCurrent))
        {
            _entryLineCombo.Text = entryCurrent;
        }
        else if (_entryLineCombo.Items.Count > 0)
        {
            _entryLineCombo.SelectedIndex = 0;
        }
    }

    private void UpdateSummary(InspectionDashboardViewModel dashboard)
    {
        _totalValueLabel.Text = dashboard.TotalCount.ToString();
        _normalValueLabel.Text = dashboard.NormalCount.ToString();
        _warningValueLabel.Text = dashboard.WarningCount.ToString();
        _abnormalValueLabel.Text = dashboard.AbnormalCount.ToString();
        _passRateValueLabel.Text = dashboard.PassRateText;
    }

    private void UpdateGrid(IReadOnlyList<InspectionRecordViewModel> records)
    {
        _recordsGrid.DataSource = records.ToList();
    }

    private void UpdateCharts(InspectionDashboardViewModel dashboard)
    {
        _currentDashboard = dashboard;
        _trendChart.Invalidate();
        _statusChart.Invalidate();
    }

    private void SuspendGridAutosize()
    {
    }

    private void ResumeGridAutosize()
    {
    }

    private PictureBox CreateResizeSnapshotBox()
    {
        var box = new PictureBox
        {
            BackColor = PageBackground,
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Visible = false
        };

        box.Paint += (_, e) =>
        {
            if (box.Image is not null)
            {
                return;
            }

            using var titleFont = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            using var subtitleFont = new Font("Microsoft YaHei UI", 9.5F);
            using var titleBrush = new SolidBrush(TextPrimaryColor);
            using var subtitleBrush = new SolidBrush(TextMutedColor);

            var titleText = "Resizing window...";
            var subtitleText = "Layout and charts will refresh after resize.";
            var titleSize = e.Graphics.MeasureString(titleText, titleFont);
            var subtitleSize = e.Graphics.MeasureString(subtitleText, subtitleFont);
            var centerX = box.ClientSize.Width / 2F;
            var centerY = box.ClientSize.Height / 2F;

            e.Graphics.DrawString(titleText, titleFont, titleBrush, centerX - titleSize.Width / 2F, centerY - 24F);
            e.Graphics.DrawString(subtitleText, subtitleFont, subtitleBrush, centerX - subtitleSize.Width / 2F, centerY + 8F);
        };

        return box;
    }

    private void CaptureResizeSnapshot()
    {
        DisposeResizeSnapshot();
        if (_layoutRoot.Width <= 0 || _layoutRoot.Height <= 0)
        {
            return;
        }

        try
        {
            _resizeSnapshot = new Bitmap(_layoutRoot.Width, _layoutRoot.Height);
            _layoutRoot.DrawToBitmap(_resizeSnapshot, new Rectangle(Point.Empty, _layoutRoot.Size));
            _resizeSnapshotBox.Image = _resizeSnapshot;
        }
        catch
        {
            DisposeResizeSnapshot();
        }
    }

    private void DisposeResizeSnapshot()
    {
        _resizeSnapshotBox.Image = null;
        _resizeSnapshot?.Dispose();
        _resizeSnapshot = null;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var entry = BuildEntry();
            if (_editingRecordId.HasValue)
            {
                _controller.Update(_editingRecordId.Value, entry);
                StartCreateEntry();
                _entryFeedbackLabel.ForeColor = Color.FromArgb(39, 174, 96);
                _entryFeedbackLabel.Text = $"已更新：{entry.DeviceName} / {entry.CheckedAt:yyyy-MM-dd HH:mm}";
            }
            else
            {
                _controller.Add(entry);
                StartCreateEntry(keepLine: true, keepInspector: true);
                _entryFeedbackLabel.ForeColor = Color.FromArgb(39, 174, 96);
                _entryFeedbackLabel.Text = $"已保存：{entry.DeviceName} / {entry.CheckedAt:yyyy-MM-dd HH:mm}";
            }

            RefreshDashboard();
            NotifyDataChanged();
        }
        catch (Exception ex)
        {
            _entryFeedbackLabel.ForeColor = Color.FromArgb(231, 76, 60);
            _entryFeedbackLabel.Text = ex.Message;
        }
    }

    private void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                FileName = $"点检记录_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                RestoreDirectory = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            _controller.Export(dialog.FileName, BuildFilter());
            MessageBox.Show(this, $"导出完成：{dialog.FileName}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private InspectionFilterViewModel BuildFilter()
    {
        var startTime = _filterStartPicker.Checked ? (DateTime?)_filterStartPicker.Value : null;
        var endTime = _filterEndPicker.Checked ? (DateTime?)_filterEndPicker.Value : null;

        if (startTime.HasValue && endTime.HasValue && startTime > endTime)
        {
            throw new InvalidOperationException("开始时间不能大于结束时间。");
        }

        var selectedLine = _filterLineCombo.SelectedItem?.ToString();
        var status = (_filterStatusCombo.SelectedItem as StatusOption)?.Value;

        return new InspectionFilterViewModel
        {
            Keyword = _filterKeywordTextBox.Text.Trim(),
            DeviceName = _filterDeviceTextBox.Text.Trim(),
            LineName = selectedLine == "全部" ? string.Empty : selectedLine ?? string.Empty,
            Status = status,
            StartTime = startTime,
            EndTime = endTime,
            IncludeRevoked = _filterIncludeRevokedCheckBox.Checked,
            PendingOnly = _pendingOnlyOverride
        };
    }

    private InspectionEntryViewModel BuildEntry()
    {
        return new InspectionEntryViewModel
        {
            LineName = _entryLineCombo.Text.Trim(),
            DeviceName = _entryDeviceTextBox.Text.Trim(),
            InspectionItem = _entryItemTextBox.Text.Trim(),
            Inspector = _entryInspectorTextBox.Text.Trim(),
            Status = (_entryStatusCombo.SelectedItem as StatusOption)?.Value ?? InspectionStatus.Normal,
            MeasuredValue = _entryMeasuredValueInput.Value,
            CheckedAt = _entryCheckedAtPicker.Value,
            Remark = _entryRemarkTextBox.Text.Trim()
        };
    }

    private void ResetFilters()
    {
        _filterKeywordTextBox.Clear();
        _filterDeviceTextBox.Clear();
        _filterStartPicker.Checked = false;
        _filterEndPicker.Checked = false;
        _filterIncludeRevokedCheckBox.Checked = false;
        _pendingOnlyOverride = false;
        _activePresetLabel = string.Empty;
        if (_filterLineCombo.Items.Count > 0)
        {
            _filterLineCombo.SelectedIndex = 0;
        }
        if (_filterStatusCombo.Items.Count > 0)
        {
            _filterStatusCombo.SelectedIndex = 0;
        }
    }

    private void AttachFilterPresetResetHandlers()
    {
        _filterKeywordTextBox.TextChanged += (_, _) => ClearHiddenPreset();
        _filterDeviceTextBox.TextChanged += (_, _) => ClearHiddenPreset();
        _filterLineCombo.SelectedIndexChanged += (_, _) => ClearHiddenPreset();
        _filterStatusCombo.SelectedIndexChanged += (_, _) => ClearHiddenPreset();
        _filterIncludeRevokedCheckBox.CheckedChanged += (_, _) => ClearHiddenPreset();
        _filterStartPicker.ValueChanged += (_, _) => ClearHiddenPreset();
        _filterEndPicker.ValueChanged += (_, _) => ClearHiddenPreset();
    }

    private void ApplyPresetFilter(InspectionFilterViewModel filter, string presetLabel)
    {
        _deferredPresetFilter = filter;
        _deferredPresetLabel = presetLabel;
        ApplyPresetValuesToControls(filter, presetLabel);

        if (!_screenInitialized)
        {
            _deferredPresetFilter = filter;
            _deferredPresetLabel = presetLabel;
            _hasDeferredPreset = true;
            return;
        }

        _hasDeferredPreset = false;
        RefreshDashboard();
    }

    private void ApplyPresetValuesToControls(InspectionFilterViewModel filter, string presetLabel)
    {
        _suppressPresetReset = true;
        try
        {
            ResetFilters();
            _pendingOnlyOverride = filter.PendingOnly;
            _activePresetLabel = presetLabel;

            _filterKeywordTextBox.Text = filter.Keyword;
            _filterDeviceTextBox.Text = filter.DeviceName;
            _filterIncludeRevokedCheckBox.Checked = filter.IncludeRevoked;

            if (filter.StartTime.HasValue)
            {
                _filterStartPicker.Value = filter.StartTime.Value;
                _filterStartPicker.Checked = true;
            }

            if (filter.EndTime.HasValue)
            {
                _filterEndPicker.Value = filter.EndTime.Value;
                _filterEndPicker.Checked = true;
            }

            SelectFilterStatus(filter.Status);
        }
        finally
        {
            _suppressPresetReset = false;
        }
    }

    private void SelectFilterStatus(InspectionStatus? status)
    {
        for (var index = 0; index < _filterStatusCombo.Items.Count; index++)
        {
            if (_filterStatusCombo.Items[index] is StatusOption option && option.Value == status)
            {
                _filterStatusCombo.SelectedIndex = index;
                return;
            }
        }

        if (_filterStatusCombo.Items.Count > 0)
        {
            _filterStatusCombo.SelectedIndex = 0;
        }
    }

    private void ClearHiddenPreset()
    {
        if (_suppressPresetReset)
        {
            return;
        }

        _pendingOnlyOverride = false;
        _activePresetLabel = string.Empty;
        _hasDeferredPreset = false;
        _deferredPresetLabel = string.Empty;
        _deferredPresetFilter = new InspectionFilterViewModel();
    }

    private void UpdateRefreshLabel(DateTime generatedAt)
    {
        var presetText = string.IsNullOrWhiteSpace(_activePresetLabel)
            ? string.Empty
            : $" | 快捷筛选：{_activePresetLabel}";
        _refreshLabel.Text = $"最近刷新：{generatedAt:yyyy-MM-dd HH:mm:ss}{presetText}";
    }

    private void NotifyDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetEntryForm(bool keepLine = false, bool keepInspector = false)
    {
        var currentLine = _entryLineCombo.Text;
        var currentInspector = _entryInspectorTextBox.Text;

        _suppressTemplateSelectionChanged = true;
        if (_entryTemplateCombo.Items.Count > 0)
        {
            _entryTemplateCombo.SelectedIndex = 0;
        }
        else
        {
            _entryTemplateCombo.Text = string.Empty;
        }
        _suppressTemplateSelectionChanged = false;

        if (keepLine)
        {
            _entryLineCombo.Text = currentLine;
        }
        else if (_entryLineCombo.Items.Count > 0)
        {
            _entryLineCombo.SelectedIndex = 0;
        }
        else
        {
            _entryLineCombo.Text = string.Empty;
        }

        _entryDeviceTextBox.Clear();
        _entryItemTextBox.Clear();
        _entryInspectorTextBox.Text = keepInspector ? currentInspector : string.Empty;
        _entryStatusCombo.SelectedIndex = 0;
        _entryMeasuredValueInput.Value = 0;
        _entryCheckedAtPicker.Value = DateTime.Now;
        _entryRemarkTextBox.Clear();
        _entryFeedbackLabel.ForeColor = AccentBlue;
        _entryFeedbackLabel.Text = "录入后列表和图表会即时刷新。";
    }

    public void ApplyTheme()
    {
        PageBackground = PageChrome.PageBackground;
        SurfaceBackground = PageChrome.SurfaceBackground;
        SurfaceBorder = PageChrome.SurfaceBorder;
        InputBackground = PageChrome.InputBackground;
        TextPrimaryColor = PageChrome.TextPrimary;
        TextSecondaryColor = PageChrome.TextSecondary;
        TextMutedColor = PageChrome.TextMuted;

        BackColor = PageBackground;
        if (_layoutRoot != null)
        {
            _layoutRoot.BackColor = PageBackground;
        }
        if (_contentHost != null)
        {
            _contentHost.BackColor = PageBackground;
        }
        if (_dashboardLayout != null)
        {
            _dashboardLayout.BackColor = PageBackground;
        }
        if (_metricsPanel != null)
        {
            _metricsPanel.BackColor = PageBackground;
        }
        if (_chartsPanel != null)
        {
            _chartsPanel.BackColor = PageBackground;
        }
        if (_chartsSplitContainer != null)
        {
            _chartsSplitContainer.BackColor = PageBackground;
            _chartsSplitContainer.Panel1.BackColor = PageBackground;
            _chartsSplitContainer.Panel2.BackColor = PageBackground;
        }
        if (_trendChart != null) _trendChart.BackColor = SurfaceBackground;
        if (_statusChart != null) _statusChart.BackColor = SurfaceBackground;
        
        if (_layoutRoot != null) ApplyDarkVisualTree(_layoutRoot);
        if (_chartsPanel != null) ApplyDarkVisualTree(_chartsPanel);
        if (_entryWindow != null)
        {
            _entryWindow.BackColor = PageBackground;
            ApplyDarkVisualTree(_entryWindow);
        }
        Invalidate(true);
    }

    private void ApplyDarkVisualTree(Control root)
    {
        bool isDarkTheme = PageBackground.R < 100;

        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case CardPanel cardPanel:
                    cardPanel.BackColor = SurfaceBackground;
                    break;
                case Panel panel when panel is not CardPanel && panel.Parent is CardPanel:
                    panel.BackColor = SurfaceBackground;
                    break;
                case TableLayoutPanel tlp when tlp.Parent is Panel && tlp.Parent.Parent is CardPanel:
                    tlp.BackColor = SurfaceBackground;
                    break;
                case Label label when label == _totalValueLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(83, 131, 255) : Color.FromArgb(41, 98, 255);
                    break;
                case Label label when label == _normalValueLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(39, 174, 96) : Color.FromArgb(22, 138, 62);
                    break;
                case Label label when label == _warningValueLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(241, 196, 15) : Color.FromArgb(217, 119, 6);
                    break;
                case Label label when label == _abnormalValueLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(231, 76, 60) : Color.FromArgb(220, 38, 38);
                    break;
                case Label label when label == _passRateValueLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = isDarkTheme ? Color.FromArgb(52, 152, 219) : Color.FromArgb(37, 99, 235);
                    break;
                case Label label when label == _entryFeedbackLabel:
                    label.BackColor = Color.Transparent; // Usually inside entry CardPanel
                    label.ForeColor = isDarkTheme ? Color.FromArgb(83, 131, 255) : Color.FromArgb(41, 98, 255);
                    break;
                case Label label when label == _refreshLabel:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = TextMutedColor;
                    break;
                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = label.Font.Bold || label.Font.Size >= 12F
                        ? TextPrimaryColor
                        : TextSecondaryColor;
                    break;
                case TextBox textBox:
                    textBox.BackColor = InputBackground;
                    textBox.ForeColor = TextPrimaryColor;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = InputBackground;
                    comboBox.ForeColor = TextPrimaryColor;
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.BackColor = InputBackground;
                    numericUpDown.ForeColor = TextPrimaryColor;
                    break;
                case DateTimePicker dateTimePicker:
                    dateTimePicker.CalendarForeColor = TextPrimaryColor;
                    dateTimePicker.CalendarMonthBackground = InputBackground;
                    dateTimePicker.CalendarTitleBackColor = SurfaceBackground;
                    dateTimePicker.CalendarTitleForeColor = TextPrimaryColor;
                    break;
                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = TextSecondaryColor;
                    break;
                case Button button:
                    if (button.FlatAppearance.BorderSize == 0) // Primary button
                    {
                        button.BackColor = AccentBlue;
                        button.ForeColor = Color.White;
                    }
                    else // Secondary button
                    {
                        button.BackColor = InputBackground;
                        button.ForeColor = TextSecondaryColor;
                        button.FlatAppearance.BorderColor = SurfaceBorder;
                    }
                    break;
                case DataGridView grid:
                    grid.BackgroundColor = SurfaceBackground;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = InputBackground;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
                    grid.DefaultCellStyle.BackColor = SurfaceBackground;
                    grid.DefaultCellStyle.ForeColor = isDarkTheme ? TextSecondaryColor : TextPrimaryColor;
                    grid.DefaultCellStyle.SelectionBackColor = isDarkTheme ? Color.FromArgb(45, 56, 78) : Color.FromArgb(226, 232, 240);
                    grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
                    grid.GridColor = SurfaceBorder;
                    break;
            }

            ApplyDarkVisualTree(control);
        }

        _entryFeedbackLabel.ForeColor = AccentBlue;
        _refreshLabel.ForeColor = TextMutedColor;
    }

    private static TextBox CreateTextBox(bool multiline = false)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = InputBackground,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };
    }

    private static ComboBox CreateDropDownListComboBox()
    {
        return new ComboBox
        {
            BackColor = InputBackground,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            IntegralHeight = false
        };
    }

    private static ComboBox CreateEditableComboBox()
    {
        return new ComboBox
        {
            BackColor = InputBackground,
            DropDownStyle = ComboBoxStyle.DropDown,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            IntegralHeight = false
        };
    }

    private static NumericUpDown CreateMeasuredValueInput()
    {
        return new NumericUpDown
        {
            BackColor = InputBackground,
            DecimalPlaces = 2,
            Maximum = 100000,
            Minimum = 0,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            ForeColor = TextPrimaryColor,
            ThousandsSeparator = true
        };
    }

    private static DateTimePicker CreateDateTimePicker(bool allowEmpty)
    {
        return new DateTimePicker
        {
            CalendarFont = new Font("Microsoft YaHei UI", 9F),
            CalendarForeColor = TextPrimaryColor,
            CalendarMonthBackground = InputBackground,
            CalendarTitleBackColor = SurfaceBackground,
            CalendarTitleForeColor = TextPrimaryColor,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Format = DateTimePickerFormat.Custom,
            ShowCheckBox = allowEmpty,
            Width = 200
        };
    }

    private static Button CreatePrimaryButton(string text)
    {
        var button = PageChrome.CreateActionButton(text, AccentBlue, true);
        button.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        button.Margin = new Padding(0, 0, 8, 0);
        return button;
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = PageChrome.CreateActionButton(text, AccentBlue, false);
        button.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
        button.Margin = new Padding(0, 0, 8, 0);
        return button;
    }

    private static CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = TextSecondaryColor,
            Margin = new Padding(0, 6, 0, 0),
            Text = text,
            UseVisualStyleBackColor = false
        };
    }

    private static DataGridView CreateRecordsGrid()
    {
        var grid = new BufferedDataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            BackgroundColor = SurfaceBackground,
            BorderStyle = BorderStyle.None,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ScrollBars = ScrollBars.Both,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.ColumnHeadersDefaultCellStyle.BackColor = InputBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimaryColor;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.BackColor = SurfaceBackground;
        grid.DefaultCellStyle.ForeColor = TextSecondaryColor;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 56, 78);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimaryColor;
        grid.GridColor = SurfaceBorder;

        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.CheckedAt), HeaderText = "点检时间", FillWeight = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.LineName), HeaderText = "产线", FillWeight = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.DeviceName), HeaderText = "设备名称", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.InspectionItem), HeaderText = "点检项目", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.Inspector), HeaderText = "点检人", FillWeight = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.StatusText), HeaderText = "状态", FillWeight = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.MeasuredValueText), HeaderText = "测量值", FillWeight = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.ClosureStateText), HeaderText = "闭环状态", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.ActionRemark), HeaderText = "处理说明", FillWeight = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InspectionRecordViewModel.Remark), HeaderText = "原始备注", FillWeight = 150 });

        var columnWidths = new[] { 150, 90, 160, 150, 100, 90, 90, 130, 240, 220 };
        for (var index = 0; index < grid.Columns.Count && index < columnWidths.Length; index++)
        {
            grid.Columns[index].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns[index].Width = columnWidths[index];
        }

        grid.CellFormatting += (_, args) =>
        {
            if (args.Value is not string text)
            {
                return;
            }

            var cellStyle = args.CellStyle;
            if (cellStyle is null)
            {
                return;
            }

            var propertyName = grid.Columns[args.ColumnIndex].DataPropertyName;
            if (propertyName == nameof(InspectionRecordViewModel.StatusText))
            {
                cellStyle.ForeColor = text switch
                {
                    "正常" => grid.BackgroundColor.R < 100 ? Color.FromArgb(39, 174, 96) : Color.FromArgb(22, 138, 62),
                    "预警" => grid.BackgroundColor.R < 100 ? Color.FromArgb(241, 196, 15) : Color.FromArgb(217, 119, 6),
                    "异常" => grid.BackgroundColor.R < 100 ? Color.FromArgb(231, 76, 60) : Color.FromArgb(220, 38, 38),
                    _ => TextPrimaryColor
                };
                return;
            }

            if (propertyName == nameof(InspectionRecordViewModel.ClosureStateText))
            {
                cellStyle.ForeColor = text switch
                {
                    var value when value.StartsWith("已闭环", StringComparison.Ordinal) => Color.FromArgb(39, 174, 96),
                    "待闭环" => Color.FromArgb(241, 196, 15),
                    "已撤回" => TextMutedColor,
                    _ => TextSecondaryColor
                };
            }
        };

        return grid;
    }

    private static Panel CreateTrendCanvas()
    {
        var panel = new BufferedPanel
        {
            BackColor = SurfaceBackground
        };
        panel.Paint += DrawTrendCanvas;
        return panel;
    }

    private static Panel CreateStatusCanvas()
    {
        var panel = new BufferedPanel
        {
            BackColor = SurfaceBackground
        };
        panel.Paint += DrawStatusCanvas;
        return panel;
    }

    private static InspectionPageControl? FindOwnerControl(Control? control)
    {
        while (control is not null && control is not InspectionPageControl)
        {
            control = control.Parent;
        }

        return control as InspectionPageControl;
    }

    private static void DrawCenteredHint(Graphics graphics, Rectangle bounds, string text, Font font, Brush brush)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(
            text,
            font,
            brush,
            bounds.Left + (bounds.Width - size.Width) / 2F,
            bounds.Top + (bounds.Height - size.Height) / 2F);
    }

    private static void DrawTrendCanvas(object? sender, PaintEventArgs e)
    {
        if (sender is not BufferedPanel panel)
        {
            return;
        }

        var form = FindOwnerControl(panel);
        if (form is null)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SurfaceBackground);

        var points = form._currentDashboard.TrendPoints;
        var rect = new Rectangle(20, 18, Math.Max(0, panel.Width - 40), Math.Max(0, panel.Height - 36));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        bool isDark = PageBackground.R < 100;
        using var axisPen = new Pen(SurfaceBorder, 1);
        using var gridPen = new Pen(isDark ? Color.FromArgb(55, 62, 80) : Color.FromArgb(200, 209, 222), 1);
        using var labelBrush = new SolidBrush(TextMutedColor);
        using var labelFont = new Font("Microsoft YaHei UI", 8.5F);

        if (points.Count == 0)
        {
            var emptyText = "暂无趋势数据";
            var size = g.MeasureString(emptyText, labelFont);
            g.DrawString(emptyText, labelFont, labelBrush, (panel.Width - size.Width) / 2, (panel.Height - size.Height) / 2);
            return;
        }

        var plotRect = new Rectangle(rect.X + 32, rect.Y + 18, rect.Width - 52, rect.Height - 56);
        if (plotRect.Width <= 20 || plotRect.Height <= 20)
        {
            return;
        }

        var maxValue = Math.Max(1, points.Max(point => Math.Max(point.NormalCount, Math.Max(point.WarningCount, point.AbnormalCount))));
        for (var i = 0; i <= 4; i++)
        {
            var y = plotRect.Bottom - (plotRect.Height * i / 4f);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            var axisText = Math.Round(maxValue * i / 4f).ToString("0");
            g.DrawString(axisText, labelFont, labelBrush, rect.X, y - 8);
        }

        g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom);
        g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);

        DrawTrendSeries(g, plotRect, points, maxValue, point => point.NormalCount, Color.FromArgb(39, 174, 96));
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.WarningCount, Color.FromArgb(241, 196, 15));
        DrawTrendSeries(g, plotRect, points, maxValue, point => point.AbnormalCount, Color.FromArgb(231, 76, 60));

        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + (plotRect.Width * index / Math.Max(1f, points.Count - 1f));
            var label = points[index].Label;
            var size = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, labelBrush, x - size.Width / 2, plotRect.Bottom + 10);
        }

        DrawTrendLegend(g, new Rectangle(plotRect.Right - 165, rect.Y - 2, 160, 18));
    }

    private static void DrawTrendSeries(
        Graphics graphics,
        Rectangle plotRect,
        IReadOnlyList<InspectionTrendPointViewModel> points,
        int maxValue,
        Func<InspectionTrendPointViewModel, int> selector,
        Color color)
    {
        using var seriesPen = new Pen(color, 2.6F);
        using var pointBrush = new SolidBrush(color);

        var positions = new List<PointF>();
        for (var index = 0; index < points.Count; index++)
        {
            var x = plotRect.Left + (plotRect.Width * index / Math.Max(1f, points.Count - 1f));
            var ratio = selector(points[index]) / (float)maxValue;
            var y = plotRect.Bottom - plotRect.Height * ratio;
            positions.Add(new PointF(x, y));
        }

        if (positions.Count > 1)
        {
            graphics.DrawLines(seriesPen, positions.ToArray());
        }

        foreach (var position in positions)
        {
            graphics.FillEllipse(pointBrush, position.X - 3.5F, position.Y - 3.5F, 7, 7);
        }
    }

    private static void DrawTrendLegend(Graphics graphics, Rectangle rect)
    {
        DrawLegendItem(graphics, new Point(rect.Left, rect.Top), "正常", Color.FromArgb(39, 174, 96));
        DrawLegendItem(graphics, new Point(rect.Left + 52, rect.Top), "预警", Color.FromArgb(241, 196, 15));
        DrawLegendItem(graphics, new Point(rect.Left + 104, rect.Top), "异常", Color.FromArgb(231, 76, 60));
    }

    private static void DrawLegendItem(Graphics graphics, Point origin, string text, Color color)
    {
        using var brush = new SolidBrush(color);
        using var textBrush = new SolidBrush(TextMutedColor);
        using var font = new Font("Microsoft YaHei UI", 8.5F);
        graphics.FillEllipse(brush, origin.X, origin.Y + 3, 8, 8);
        graphics.DrawString(text, font, textBrush, origin.X + 12, origin.Y);
    }

    private static void DrawStatusCanvas(object? sender, PaintEventArgs e)
    {
        if (sender is not BufferedPanel panel)
        {
            return;
        }

        var form = FindOwnerControl(panel);
        if (form is null)
        {
            return;
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(SurfaceBackground);
        using var hintBrush = new SolidBrush(TextMutedColor);
        using var hintFont = new Font("Microsoft YaHei UI", 8.5F);

        var counts = new[]
        {
            ("正常", form._currentDashboard.NormalCount, Color.FromArgb(39, 174, 96)),
            ("预警", form._currentDashboard.WarningCount, Color.FromArgb(241, 196, 15)),
            ("异常", form._currentDashboard.AbnormalCount, Color.FromArgb(231, 76, 60))
        };

        var total = counts.Sum(item => item.Item2);
        var diameter = Math.Min(panel.Width - 80, panel.Height - 70);
        diameter = Math.Max(80, diameter);
        var donutRect = new Rectangle(24, Math.Max(20, (panel.Height - diameter) / 2), diameter, diameter);

        bool isDark = PageBackground.R < 100;
        using var backPen = new Pen(isDark ? Color.FromArgb(55, 62, 80) : Color.FromArgb(200, 209, 222), 24);
        g.DrawArc(backPen, donutRect, 0, 360);

        if (total > 0)
        {
            var startAngle = -90F;
            foreach (var (name, value, color) in counts)
            {
                if (value == 0)
                {
                    continue;
                }

                var sweepAngle = 360F * value / total;
                using var pen = new Pen(color, 24);
                g.DrawArc(pen, donutRect, startAngle, sweepAngle);
                startAngle += sweepAngle;
            }
        }

        using var centerBrush = new SolidBrush(TextPrimaryColor);
        using var centerValueFont = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
        using var centerTextFont = new Font("Microsoft YaHei UI", 8.5F);
        var totalText = total.ToString();
        var totalSize = g.MeasureString(totalText, centerValueFont);
        g.DrawString(totalText, centerValueFont, centerBrush,
            donutRect.Left + donutRect.Width / 2F - totalSize.Width / 2,
            donutRect.Top + donutRect.Height / 2F - 22);
        g.DrawString("总记录", centerTextFont, new SolidBrush(TextMutedColor),
            donutRect.Left + donutRect.Width / 2F - 22,
            donutRect.Top + donutRect.Height / 2F + 6);

        var legendX = donutRect.Right + 24;
        var legendY = donutRect.Top + 8;
        foreach (var (name, value, color) in counts)
        {
            using var brush = new SolidBrush(color);
            using var titleBrush = new SolidBrush(TextPrimaryColor);
            using var valueBrush = new SolidBrush(TextMutedColor);
            using var titleFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            using var valueFont = new Font("Microsoft YaHei UI", 9F);
            g.FillRectangle(brush, legendX, legendY + 5, 12, 12);
            g.DrawString(name, titleFont, titleBrush, legendX + 20, legendY);
            g.DrawString($"数量：{value}", valueFont, valueBrush, legendX + 20, legendY + 22);
            legendY += 54;
        }
    }
}
