using System.Drawing;

namespace WinFormsApp.Views;

internal sealed partial class InspectionPageControl
{
    private Control BuildLayout()
    {
        var root = new WorkspacePanel
        {
            Dock = DockStyle.Fill,
            Padding = PageChrome.PagePadding,
            BackColor = PageBackground
        };

        var contentHost = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground
        };
        _contentHost = contentHost;
        contentHost.Controls.Add(BuildDashboardPanel());

        root.Controls.Add(contentHost);
        root.Controls.Add(BuildHeaderPanel());
        return root;
    }

    private Control BuildHeaderPanel()
    {
        var panel = CreateSurfacePanel();
        _headerCard = panel;
        panel.Dock = DockStyle.Top;
        panel.Height = 112;
        panel.Padding = new Padding(20, 16, 20, 16);

        _headerTitlePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0)
        };

        _headerTitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Margin = new Padding(0, 0, 0, 4),
            Text = "\u70b9\u68c0\u8bb0\u5f55\u4e2d\u5fc3",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _headerSubtitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = Color.FromArgb(99, 114, 130),
            Margin = Padding.Empty,
            Text = $"\u5f53\u524d\u8d26\u53f7\uff1a{_account}  |  \u5f55\u5165\u3001\u7b5b\u9009\u3001\u76d1\u63a7\u3001\u5bfc\u51fa",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleLayout.Controls.Add(_headerTitleLabel, 0, 0);
        titleLayout.Controls.Add(_headerSubtitleLabel, 0, 1);
        _headerTitlePanel.Controls.Add(titleLayout);

        _headerRightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0)
        };

        _headerActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        _manageTemplatesButton.Margin = new Padding(8, 0, 0, 0);
        _toggleEntryPanelButton.Margin = new Padding(8, 0, 0, 0);
        _headerActionPanel.Controls.Add(_manageTemplatesButton);
        _headerActionPanel.Controls.Add(_toggleEntryPanelButton);

        _refreshLabel.AutoSize = false;
        _refreshLabel.Height = 26;
        _refreshLabel.Dock = DockStyle.Bottom;
        _refreshLabel.Margin = Padding.Empty;
        _refreshLabel.TextAlign = ContentAlignment.BottomRight;

        _headerActionPanel.Dock = DockStyle.Top;
        
        _headerRightPanel.Controls.Add(_refreshLabel);
        _headerRightPanel.Controls.Add(_headerActionPanel);

        _headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        panel.Controls.Add(_headerLayout);
        UpdateHeaderLayout();
        return panel;
    }

    private Control BuildEntryPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(18);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Text = "\u70b9\u68c0\u8bb0\u5f55\u5f55\u5165"
        };
        _entryTitleLabel = titleLabel;

        var subtitleLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(99, 114, 130),
            Text = "\u65b0\u5f55\u5165\u7684\u6570\u636e\u4f1a\u7acb\u5373\u8fdb\u5165\u67e5\u8be2\u5217\u8868\u548c\u76d1\u63a7\u56fe\u8868\u3002"
        };
        _entrySubtitleLabel = subtitleLabel;

        subtitleLabel.Location = new Point(0, 30);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = Color.Transparent
        };
        titleLabel.Location = new Point(0, 0);
        headerPanel.Controls.Add(subtitleLabel);
        headerPanel.Controls.Add(titleLabel);

        var formTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 8, 0)
        };
        formTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AddInputRow(formTable, "\u53f0\u8d26\u6a21\u677f", _entryTemplateCombo);
        AddInputRow(formTable, "\u4ea7\u7ebf", _entryLineCombo);
        AddInputRow(formTable, "\u8bbe\u5907\u540d\u79f0", _entryDeviceTextBox);
        AddInputRow(formTable, "\u70b9\u68c0\u9879\u76ee", _entryItemTextBox);
        AddInputRow(formTable, "\u70b9\u68c0\u4eba", _entryInspectorTextBox);
        AddInputRow(formTable, "\u70b9\u68c0\u72b6\u6001", _entryStatusCombo);
        AddInputRow(formTable, "\u6d4b\u91cf\u503c", _entryMeasuredValueInput);
        AddInputRow(formTable, "\u70b9\u68c0\u65f6\u95f4", _entryCheckedAtPicker);
        AddInputRow(formTable, "\u5907\u6ce8", _entryRemarkTextBox, 82);

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = false
        };

        var saveButton = CreatePrimaryButton("\u4fdd\u5b58\u8bb0\u5f55");
        _entrySaveButton = saveButton;
        saveButton.Click += OnSaveClicked;
        _saveTemplateButton.Margin = new Padding(0, 0, 8, 0);
        var resetButton = CreateSecondaryButton("\u91cd\u7f6e\u8868\u5355");
        _entryResetButton = resetButton;
        resetButton.Click += OnResetEntryClicked;

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(_saveTemplateButton);
        buttonPanel.Controls.Add(resetButton);

        formTable.Controls.Add(buttonPanel, 0, formTable.RowCount);
        formTable.RowStyles.Add(new RowStyle());
        formTable.RowCount++;
        formTable.Controls.Add(_entryFeedbackLabel, 0, formTable.RowCount);
        formTable.RowStyles.Add(new RowStyle());
        formTable.RowCount++;

        panel.Controls.Add(formTable);
        panel.Controls.Add(headerPanel);
        return panel;
    }

    private Control BuildDashboardPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = PageBackground
        };
        _dashboardLayout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        layout.Controls.Add(BuildFilterPanel(), 0, 0);
        layout.Controls.Add(BuildMetricsPanel(), 0, 1);
        layout.Controls.Add(BuildGridPanel(), 0, 2);
        return layout;
    }

    private Control BuildFilterPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Dock = DockStyle.Fill;
        panel.Padding = new Padding(20, 16, 20, 18);
        panel.AutoSize = true;
        panel.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Text = "\u67e5\u8be2\u7b5b\u9009"
        };

        _filterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 16, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

        _filterKeywordBlock = CreateFilterBlock("\u5173\u952e\u8bcd", _filterKeywordTextBox);
        _filterDeviceBlock = CreateFilterBlock("\u8bbe\u5907\u540d\u79f0", _filterDeviceTextBox);
        _filterLineBlock = CreateFilterBlock("\u4ea7\u7ebf", _filterLineCombo);
        _filterStatusBlock = CreateFilterBlock("\u72b6\u6001", _filterStatusCombo);
        _filterStartBlock = CreateFilterBlock("\u5f00\u59cb\u65f6\u95f4", _filterStartPicker);
        _filterEndBlock = CreateFilterBlock("\u7ed3\u675f\u65f6\u95f4", _filterEndPicker);

        _filterActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Dock = DockStyle.Top,
            BackColor = Color.Transparent
        };

        var searchButton = CreatePrimaryButton("\u5e94\u7528\u7b5b\u9009");
        searchButton.Click += (_, _) => RefreshDashboard();
        var clearButton = CreateSecondaryButton("\u6e05\u7a7a\u6761\u4ef6");
        clearButton.Click += (_, _) =>
        {
            ResetFilters();
            RefreshDashboard();
        };
        var exportButton = CreateSecondaryButton("Excel\u5bfc\u51fa");
        exportButton.Click += OnExportClicked;

        _filterActionPanel.Controls.Add(searchButton);
        _filterActionPanel.Controls.Add(clearButton);
        _filterActionPanel.Controls.Add(exportButton);
        _filterIncludeRevokedCheckBox.Margin = new Padding(12, 8, 0, 0);
        _filterActionPanel.Controls.Add(_filterIncludeRevokedCheckBox);
        _filterActionBlock = CreateFilterBlock("\u64cd\u4f5c", _filterActionPanel);
        _filterActionBlock.Margin = Padding.Empty;

        panel.Controls.Add(_filterLayout);
        panel.Controls.Add(titleLabel);
        UpdateFilterLayout();
        return panel;
    }

    private Control BuildMetricsPanel()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = PageBackground
        };
        _metricsPanel = table;

        for (var index = 0; index < 5; index++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        table.Controls.Add(CreateMetricCard("\u8bb0\u5f55\u603b\u6570", _totalValueLabel, Color.FromArgb(83, 131, 255), 0), 0, 0);
        table.Controls.Add(CreateMetricCard("\u6b63\u5e38", _normalValueLabel, Color.FromArgb(39, 174, 96), 12), 1, 0);
        table.Controls.Add(CreateMetricCard("\u9884\u8b66", _warningValueLabel, Color.FromArgb(241, 196, 15), 12), 2, 0);
        table.Controls.Add(CreateMetricCard("\u5f02\u5e38", _abnormalValueLabel, Color.FromArgb(231, 76, 60), 12), 3, 0);
        table.Controls.Add(CreateMetricCard("\u5408\u683c\u7387", _passRateValueLabel, Color.FromArgb(52, 152, 219), 12), 4, 0);
        return table;
    }

    private Control BuildGridPanel()
    {
        var panel = CreateSurfacePanel();
        panel.Dock = DockStyle.Fill;
        panel.Margin = Padding.Empty;
        panel.Padding = new Padding(20, 16, 20, 20);

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Text = "\u70b9\u68c0\u8bb0\u5f55\u5217\u8868"
        };

        _recordsGrid.Dock = DockStyle.Fill;
        panel.Controls.Add(_recordsGrid);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private Control BuildChartsDrawer()
    {
        var drawerHost = new Panel
        {
            Dock = DockStyle.Right,
            Width = 430,
            Padding = new Padding(16, 0, 0, 0),
            BackColor = PageBackground,
            Visible = false
        };
        _chartsPanel = drawerHost;

        var drawerCard = CreateSurfacePanel();
        drawerCard.Dock = DockStyle.Fill;
        drawerCard.Padding = new Padding(18);

        var closeButton = CreateSecondaryButton("\u5173\u95ed");
        closeButton.Anchor = AnchorStyles.Right;
        closeButton.Margin = new Padding(8, 0, 0, 0);
        closeButton.Click += (_, _) => SetChartsDrawerVisible(false);

        var titleLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Margin = Padding.Empty,
            Text = "\u8d8b\u52bf\u5206\u6790",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        headerLayout.Controls.Add(titleLabel, 0, 0);
        headerLayout.Controls.Add(closeButton, 1, 0);

        var drawerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        drawerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        drawerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        drawerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        drawerLayout.Controls.Add(headerLayout, 0, 0);

        var content = BuildChartsContent();
        content.Dock = DockStyle.Fill;
        content.Margin = new Padding(0, 12, 0, 0);
        drawerLayout.Controls.Add(content, 0, 1);

        drawerCard.Controls.Add(drawerLayout);
        drawerHost.Controls.Add(drawerCard);
        return drawerHost;
    }

    private Control BuildChartsContent()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None,
            SplitterWidth = 8,
            BackColor = PageBackground
        };
        _chartsSplitContainer = split;
        split.Panel1MinSize = 40;
        split.Panel2MinSize = 40;
        split.Panel1.BackColor = PageBackground;
        split.Panel2.BackColor = PageBackground;
        split.Panel1.Padding = new Padding(0, 0, 0, 10);
        split.Panel2.Padding = new Padding(0, 10, 0, 0);

        var trendPanel = CreateSurfacePanel();
        trendPanel.Dock = DockStyle.Fill;
        trendPanel.Padding = new Padding(20, 16, 20, 20);

        var trendLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        trendLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        trendLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        trendLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var trendTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Margin = new Padding(0, 0, 0, 12),
            Text = "\u5b9e\u65f6\u76d1\u63a7\u8d8b\u52bf"
        };
        _trendChart.Dock = DockStyle.Fill;
        _trendChart.Margin = Padding.Empty;
        trendLayout.Controls.Add(trendTitle, 0, 0);
        trendLayout.Controls.Add(_trendChart, 0, 1);
        trendPanel.Controls.Add(trendLayout);

        var statusPanel = CreateSurfacePanel();
        statusPanel.Dock = DockStyle.Fill;
        statusPanel.Padding = new Padding(20, 16, 20, 20);

        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var statusTitle = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Margin = new Padding(0, 0, 0, 12),
            Text = "\u72b6\u6001\u5206\u5e03"
        };
        _statusChart.Dock = DockStyle.Fill;
        _statusChart.Margin = Padding.Empty;
        statusLayout.Controls.Add(statusTitle, 0, 0);
        statusLayout.Controls.Add(_statusChart, 0, 1);
        statusPanel.Controls.Add(statusLayout);

        split.Panel1.Controls.Add(trendPanel);
        split.Panel2.Controls.Add(statusPanel);
        return split;
    }

    private static void AddInputRow(TableLayoutPanel table, string labelText, Control control, int height = 36)
    {
        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(0, 10, 0, 6),
            Text = labelText
        };

        control.Dock = DockStyle.Top;
        control.Height = height;
        control.Margin = new Padding(0, 0, 0, 6);

        table.Controls.Add(label, 0, table.RowCount);
        table.RowStyles.Add(new RowStyle());
        table.RowCount++;
        table.Controls.Add(control, 0, table.RowCount);
        table.RowStyles.Add(new RowStyle());
        table.RowCount++;
    }

    private static Control CreateFilterBlock(string title, Control control)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 10),
            BackColor = Color.Transparent
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle());
        panel.RowStyles.Add(new RowStyle());

        var label = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(75, 85, 99),
            Text = title
        };

        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 6, 0, 0);
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private static Panel CreateMetricCard(string title, Label valueLabel, Color accentColor, int leftMargin)
    {
        var panel = CreateSurfacePanel();
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(Math.Min(leftMargin, 8), 0, 0, 10);
        panel.MinimumSize = new Size(0, 92);
        panel.Padding = new Padding(16, 14, 16, 14);

        var titleLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Color.FromArgb(99, 114, 130),
            Text = title
        };

        valueLabel.Dock = DockStyle.Top;
        valueLabel.ForeColor = accentColor;

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static Label CreateMetricValueLabel()
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            Margin = new Padding(0, 6, 0, 0)
        };
    }

    private static Panel CreateSurfacePanel()
    {
        return new CardPanel
        {
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 0, 14)
        };
    }
}
