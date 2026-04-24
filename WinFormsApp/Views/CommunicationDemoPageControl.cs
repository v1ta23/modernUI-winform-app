using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using WinFormsApp.ViewModels;

namespace WinFormsApp.Views;

internal sealed class CommunicationDemoPageControl : UserControl, IInteractiveResizeAware
{
    private const string CommandTemplateFileName = "communication-command-templates.json";
    private const string ConnectionHistoryFileName = "communication-devices.txt";
    private const int MaxRecentDevices = 8;

    private static readonly CommunicationCommandTemplate[] DefaultCommandTemplates =
    [
        new(
            "PING 测试连接",
            "PING",
            "确认设备能不能正常回应。",
            "ECHO、PONG 或 OK。",
            ["ECHO:", "PONG", "OK"],
            "通信正常：设备已经回应连接测试。",
            "PING 没有得到确认，先检查设备是否在线，或设备是否支持这条指令。"),
        new(
            "READ_STATUS 读取状态",
            "READ_STATUS",
            "读取设备当前运行状态。",
            "STATUS 或 OK。",
            ["STATUS", "OK"],
            "状态读取成功：设备返回了当前运行状态。",
            "状态读取失败，可能是指令不支持，或设备当前不允许查询。"),
        new(
            "READ_ALARM 读取报警",
            "READ_ALARM",
            "查看设备当前是否有报警。",
            "ALARM、NO_ALARM 或 OK。",
            ["ALARM", "NO_ALARM", "OK"],
            "报警查询成功：设备返回了报警状态。",
            "报警查询失败，建议检查设备协议里的报警读取指令。"),
        new(
            "RESET_FAULT 清除故障",
            "RESET_FAULT",
            "请求设备清除可恢复故障。",
            "OK 或 RESET_OK。",
            ["RESET_OK", "OK"],
            "清除请求已被设备接受。",
            "设备拒绝清除故障，可能是故障不可恢复，或需要现场复位。")
    ];

    private static readonly CommunicationCommandTemplate[] CommonCommands = LoadCommandTemplates();

    private readonly BindingList<DeviceStatusRow> _deviceRows = new();
    private readonly BindingList<PacketLogRow> _packetRows = new();
    private readonly List<string> _recentDevices;
    private readonly Control _layoutRoot;
    private readonly InteractiveResizeFreezeController _interactiveResizeController;
    private readonly Label _infoLabel;
    private readonly Label _connectionValueLabel;
    private readonly Label _onlineValueLabel;
    private readonly Label _latencyValueLabel;
    private readonly Label _alarmValueLabel;
    private readonly Label _connectionNoteLabel;
    private readonly Label _onlineNoteLabel;
    private readonly Label _latencyNoteLabel;
    private readonly Label _alarmNoteLabel;
    private readonly Button _connectButton;
    private readonly ComboBox _commandComboBox;
    private readonly TextBox _sendTextBox;
    private readonly TextBox _responseTextBox;
    private readonly TopologyCanvas _topologyCanvas;
    private readonly DataGridView _deviceGrid;
    private readonly DataGridView _packetGrid;
    private TcpClient? _tcpClient;
    private NetworkStream? _tcpStream;
    private LocalDemoDevice? _demoDevice;
    private CommunicationCommandTemplate? _lastSentTemplate;

    private bool _connected;
    private int _pulse;
    private int _alarmCount;
    private int _messageCount;
    private int _replyCount;
    private int _lastDeviceIndex = -1;
    private string _deviceHost = "127.0.0.1";
    private int _devicePort = 9001;
    private string _lastFlowText = "还没开始";
    private string _selectedLineName = string.Empty;
    private string _selectedDeviceName = "待测设备";
    private string _selectedCommunicationAddress = string.Empty;
    private bool _hasSelectedDeviceEndpoint;

    public CommunicationDemoPageControl()
    {
        Dock = DockStyle.Fill;
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _recentDevices = LoadRecentDevices();
        UseFirstRecentDevice();

        _infoLabel = PageChrome.CreateInfoLabel("点击 1 打开连接窗口，连接后发送测试内容并读取真实回复。");
        _connectionValueLabel = PageChrome.CreateValueLabel(18F, "未连接");
        _onlineValueLabel = PageChrome.CreateValueLabel(18F, "0/1");
        _latencyValueLabel = PageChrome.CreateValueLabel(18F, "暂无");
        _alarmValueLabel = PageChrome.CreateValueLabel(18F, "0");
        _connectionNoteLabel = PageChrome.CreateNoteLabel("连接后才能发送测试消息");
        _onlineNoteLabel = PageChrome.CreateNoteLabel("当前没有设备在线");
        _latencyNoteLabel = PageChrome.CreateNoteLabel("最近做了什么会显示在这里");
        _alarmNoteLabel = PageChrome.CreateNoteLabel("设备故障会写入通信记录");

        _sendTextBox = CreateTextInput("PING");
        _responseTextBox = CreateTextInput("真实设备回复会显示在这里");
        _responseTextBox.Multiline = true;
        _responseTextBox.ReadOnly = true;
        _responseTextBox.ScrollBars = ScrollBars.Vertical;

        _commandComboBox = CreateCommandComboBox();
        _commandComboBox.Items.AddRange(CommonCommands);
        _commandComboBox.SelectedIndexChanged += (_, _) => ApplySelectedCommand();
        _commandComboBox.SelectedIndex = 0;

        _connectButton = PageChrome.CreateActionButton("1 连接设备", PageChrome.AccentGreen, true);
        var sendButton = PageChrome.CreateActionButton("2 发送测试", PageChrome.AccentBlue, false);
        var replyButton = PageChrome.CreateActionButton("3 接收回复", PageChrome.AccentCyan, false);
        var errorButton = PageChrome.CreateActionButton("4 模拟故障", PageChrome.AccentOrange, false);
        var clearButton = PageChrome.CreateActionButton("清空记录", PageChrome.AccentPurple, false);

        _connectButton.Click += (_, _) => ToggleRealConnection();
        sendButton.Click += (_, _) => SendTestMessage();
        replyButton.Click += (_, _) => ReadRealReply();
        errorButton.Click += (_, _) => SimulateError();
        clearButton.Click += (_, _) => ClearRecords();

        _topologyCanvas = new TopologyCanvas(_deviceRows)
        {
            Dock = DockStyle.Fill
        };

        _deviceGrid = CreateGrid();
        _packetGrid = CreateGrid();
        ConfigureDeviceGrid();
        ConfigurePacketGrid();
        ConfigureGridFormatting();

        SeedDemoData();
        _layoutRoot = BuildLayout(_connectButton, sendButton, replyButton, errorButton, clearButton);
        Controls.Add(_layoutRoot);
        _interactiveResizeController = new InteractiveResizeFreezeController(this, _layoutRoot, PageChrome.PageBackground);
        _layoutRoot.BringToFront();
        RefreshStatus();
    }

    public void ApplyTheme()
    {
        BackColor = PageChrome.PageBackground;
        PageChrome.ApplyGridTheme(_deviceGrid);
        PageChrome.ApplyGridTheme(_packetGrid);
        Invalidate(true);
    }

    public void ApplyDevicePreset(DeviceCommunicationPresetViewModel device)
    {
        _selectedLineName = device.LineName.Trim();
        _selectedDeviceName = string.IsNullOrWhiteSpace(device.DeviceName) ? "待测设备" : device.DeviceName.Trim();
        _selectedCommunicationAddress = device.CommunicationAddress.Trim();

        if (_connected)
        {
            DisconnectRealDevice("已切换待测设备，原连接已断开。");
        }

        _messageCount = 0;
        _replyCount = 0;
        _lastDeviceIndex = 0;
        _lastFlowText = "已带入";

        if (TryParseCommunicationAddress(_selectedCommunicationAddress, out var host, out var port))
        {
            _deviceHost = host;
            _devicePort = port;
            _hasSelectedDeviceEndpoint = true;
            UpdateDeviceRow("离线", "0 次", $"待连接 {_deviceHost}:{_devicePort}");
            AddPacket("系统", GetCurrentDeviceDisplayName(), $"已从设备台账带入通信地址：{_selectedCommunicationAddress}", "待连接");
            SetDeviceReply(
                $"已带入：{GetCurrentDeviceDisplayName()}",
                $"地址：{_selectedCommunicationAddress}。点击 1 连接设备时会自动填入该地址。");
        }
        else
        {
            _hasSelectedDeviceEndpoint = false;
            UpdateDeviceRow("离线", "0 次", "通信地址格式不正确");
            AddPacket("系统", GetCurrentDeviceDisplayName(), $"通信地址格式不正确：{_selectedCommunicationAddress}", "地址错误");
            SetDeviceReply(
                $"已带入：{GetCurrentDeviceDisplayName()}",
                "通信地址格式应类似 tcp://192.168.10.21:9001 或 192.168.10.21:9001。");
        }

        RefreshStatus();
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseTcpConnection();
            StopDemoDevice();
        }

        base.Dispose(disposing);
    }

    private Control BuildLayout(
        Button connectButton,
        Button sendButton,
        Button replyButton,
        Button errorButton,
        Button clearButton)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, PageChrome.HeaderHeight + 12));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = PageChrome.CreatePageHeader(
            "通信测试台",
            "连接真实 TCP 设备，发送测试内容，并查看设备返回的原始回复。",
            _infoLabel,
            clearButton,
            errorButton,
            replyButton,
            sendButton,
            connectButton);
        PageChrome.BindControlHeightToRow(root, 0, header);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildMetrics(), 0, 1);
        root.Controls.Add(BuildWorkspace(), 0, 2);
        return root;
    }

    private Control BuildMetrics()
    {
        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty
        };
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < 4; index++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        metrics.Controls.Add(PageChrome.CreateMetricCard("连接状态", PageChrome.AccentGreen, _connectionValueLabel, _connectionNoteLabel), 0, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("已连设备", PageChrome.AccentCyan, _onlineValueLabel, _onlineNoteLabel), 1, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("最新动作", PageChrome.AccentBlue, _latencyValueLabel, _latencyNoteLabel), 2, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("故障次数", PageChrome.AccentOrange, _alarmValueLabel, _alarmNoteLabel, new Padding(0)), 3, 0);
        return metrics;
    }

    private Control BuildSendPanel()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var sendLine = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        sendLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sendLine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
        sendLine.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sendLine.Controls.Add(_sendTextBox, 0, 0);
        sendLine.Controls.Add(_commandComboBox, 1, 0);

        body.Controls.Add(CreateFieldLabel("发送内容"), 0, 0);
        body.Controls.Add(sendLine, 1, 0);
        body.Controls.Add(CreateFieldLabel("设备回复"), 0, 1);
        body.Controls.Add(_responseTextBox, 1, 1);

        return PageChrome.CreateSectionShell(
            "发送与回复",
            "连接后发送一条测试内容，设备原始回复用于判断通信是否正常。",
            out _,
            body,
            new Padding(0, 0, 12, 12));
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var leftSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        leftSide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        leftSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 205F));
        leftSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var topologyShell = PageChrome.CreateSectionShell(
            "控制端与设备",
            "中间是控制端，外侧是当前待测设备。高亮连线表示最近一次通信。",
            out _,
            _topologyCanvas,
            new Padding(0, 0, 12, 0));

        leftSide.Controls.Add(BuildSendPanel(), 0, 0);
        leftSide.Controls.Add(topologyShell, 0, 1);

        var rightSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rightSide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        rightSide.Controls.Add(PageChrome.CreateSectionShell("设备列表", "设备状态分为在线、离线、故障。", out _, _deviceGrid), 0, 0);
        rightSide.Controls.Add(PageChrome.CreateSectionShell("通信记录", "记录每一步通信动作和处理结果。", out _, _packetGrid, new Padding(0)), 0, 1);

        workspace.Controls.Add(leftSide, 0, 0);
        workspace.Controls.Add(rightSide, 1, 0);
        return workspace;
    }

    private void SeedDemoData()
    {
        _deviceRows.Add(new DeviceStatusRow("待测设备", "离线", "0 次", "--", "等待连接"));

        _deviceGrid.DataSource = _deviceRows;
        _packetGrid.DataSource = _packetRows;
        AddPacket("系统", "控制端", "页面已就绪，请先连接设备。", "提示");
    }

    private void ConfigureDeviceGrid()
    {
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.DeviceName), "设备", 100));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LinkState), "状态", 76));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Latency), "次数", 70));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LastPacket), "时间", 92));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Detail), "说明", 140));
    }

    private void ConfigurePacketGrid()
    {
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Time), "时间", 78));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Direction), "动作", 70));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.DeviceName), "对象", 92));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Payload), "说明", 180));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Result), "状态", 74));
    }

    private void ConfigureGridFormatting()
    {
        _deviceGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                e.CellStyle is not { } style ||
                _deviceGrid.Rows[e.RowIndex].DataBoundItem is not DeviceStatusRow row)
            {
                return;
            }

            var stateColor = GetStateColor(row.LinkState);
            var columnName = _deviceGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (columnName == nameof(DeviceStatusRow.LinkState))
            {
                style.ForeColor = stateColor;
                style.SelectionForeColor = stateColor;
            }
            else if (row.LinkState is "故障" or "错误" or "异常" or "超时")
            {
                style.ForeColor = PageChrome.MixColor(PageChrome.TextSecondary, stateColor, 0.32F);
            }
        };

        _packetGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                e.CellStyle is not { } style ||
                _packetGrid.Rows[e.RowIndex].DataBoundItem is not PacketLogRow row)
            {
                return;
            }

            var columnName = _packetGrid.Columns[e.ColumnIndex].DataPropertyName;
            if (columnName == nameof(PacketLogRow.Direction))
            {
                var directionColor = row.Direction switch
                {
                    "发送" => PageChrome.AccentBlue,
                    "接收" => PageChrome.AccentCyan,
                    "故障" => PageChrome.AccentOrange,
                    _ => PageChrome.AccentPurple
                };
                style.ForeColor = directionColor;
                style.SelectionForeColor = directionColor;
            }
            else if (columnName == nameof(PacketLogRow.Result))
            {
                var resultColor = GetResultColor(row.Result);
                style.ForeColor = resultColor;
                style.SelectionForeColor = resultColor;
            }
        };
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
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
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
            MinimumWidth = 58
        };
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = PageChrome.TextSecondary,
            Margin = Padding.Empty,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateTextInput(string text)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            Margin = new Padding(0, 0, 10, 8),
            Text = text
        };
    }

    private static ComboBox CreateCommandComboBox()
    {
        return new ComboBox
        {
            Dock = DockStyle.Fill,
            BackColor = PageChrome.InputBackground,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 9F),
            ForeColor = PageChrome.TextPrimary,
            Margin = new Padding(0, 0, 10, 8)
        };
    }

    private void ApplySelectedCommand()
    {
        if (_commandComboBox.SelectedItem is not CommunicationCommandTemplate command)
        {
            return;
        }

        _sendTextBox.Text = command.CommandText;
        SetDeviceReply($"用途：{command.Purpose}", $"预期回复：{command.ExpectedReply}");
    }

    private void SetDeviceReply(string message, string? explanation = null)
    {
        _responseTextBox.Text = string.IsNullOrWhiteSpace(explanation)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}说明：{explanation}";
    }

    private void UseFirstRecentDevice()
    {
        if (_recentDevices.Count == 0)
        {
            return;
        }

        if (TryParseEndpoint(_recentDevices[0], out var host, out var port))
        {
            _deviceHost = host;
            _devicePort = port;
        }
    }

    private static List<string> LoadRecentDevices()
    {
        var path = GetConnectionHistoryPath();
        if (!File.Exists(path))
        {
            return new List<string>();
        }

        try
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentDevices)
                .ToList();
        }
        catch (IOException)
        {
            return new List<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    private static CommunicationCommandTemplate[] LoadCommandTemplates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, CommandTemplateFileName);
        if (!File.Exists(path))
        {
            return DefaultCommandTemplates;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var configs = JsonSerializer.Deserialize<List<CommunicationCommandTemplateConfig>>(File.ReadAllText(path), options);
            var templates = configs?
                .Select(config => config.ToTemplate())
                .Where(template => template is not null)
                .Cast<CommunicationCommandTemplate>()
                .ToArray();
            return templates is { Length: > 0 } ? templates : DefaultCommandTemplates;
        }
        catch (JsonException)
        {
            return DefaultCommandTemplates;
        }
        catch (IOException)
        {
            return DefaultCommandTemplates;
        }
        catch (UnauthorizedAccessException)
        {
            return DefaultCommandTemplates;
        }
    }

    private void RememberCurrentDevice()
    {
        var endpoint = $"{_deviceHost}:{_devicePort}";
        _recentDevices.RemoveAll(item => string.Equals(item, endpoint, StringComparison.OrdinalIgnoreCase));
        _recentDevices.Insert(0, endpoint);

        if (_recentDevices.Count > MaxRecentDevices)
        {
            _recentDevices.RemoveRange(MaxRecentDevices, _recentDevices.Count - MaxRecentDevices);
        }

        SaveRecentDevices(_recentDevices);
    }

    private static void SaveRecentDevices(IEnumerable<string> devices)
    {
        try
        {
            var path = GetConnectionHistoryPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, devices);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetConnectionHistoryPath()
    {
        return Path.Combine(Application.UserAppDataPath, ConnectionHistoryFileName);
    }

    private static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var divider = endpoint.LastIndexOf(':');
        if (divider <= 0 || divider == endpoint.Length - 1)
        {
            return false;
        }

        var parsedHost = endpoint[..divider].Trim();
        if (parsedHost.Length == 0 || !int.TryParse(endpoint[(divider + 1)..].Trim(), out var parsedPort))
        {
            return false;
        }

        host = parsedHost;
        port = parsedPort;
        return true;
    }

    private static bool TryParseCommunicationAddress(string address, out string host, out int port)
    {
        var value = address.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            uri.Port > 0)
        {
            host = uri.Host;
            port = uri.Port;
            return true;
        }

        return TryParseEndpoint(value, out host, out port);
    }

    private static CommunicationCommandTemplate? FindCommandTemplate(string commandText)
    {
        return CommonCommands.FirstOrDefault(command =>
            string.Equals(command.CommandText, commandText.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string ExplainResponse(string response, CommunicationCommandTemplate? commandTemplate)
    {
        if (commandTemplate is not null)
        {
            return commandTemplate.Explain(response);
        }

        var value = response.Trim();
        if (value.Length == 0)
        {
            return "设备返回了空内容。连接可能通了，但这条指令没有拿到有效结果。";
        }

        if (value.StartsWith("ECHO:", StringComparison.OrdinalIgnoreCase))
        {
            return "通信正常：设备收到了刚才发送的内容，并把内容回传了。";
        }

        if (value.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return "通信正常：设备确认执行成功。";
        }

        if (value.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
            || value.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return "设备返回错误：指令可能不支持、格式不对，或设备当前不允许执行。";
        }

        if (value.Contains("ALARM", StringComparison.OrdinalIgnoreCase))
        {
            return "设备返回报警信息：需要查看设备状态或现场传感器。";
        }

        if (value.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            return "设备提示超时：可能是设备忙、网络慢，或指令处理时间太长。";
        }

        return "已收到设备回复，但暂时没有匹配到已知规则。请对照设备协议文档看这段原始内容。";
    }

    private static Color GetStateColor(string state)
    {
        return state switch
        {
            "在线" => PageChrome.AccentGreen,
            "故障" or "错误" or "异常" => PageChrome.AccentOrange,
            "超时" => PageChrome.AccentRed,
            _ => PageChrome.TextMuted
        };
    }

    private static Color GetResultColor(string result)
    {
        return result switch
        {
            "成功" or "正常" or "已发送" or "已收到" or "已清空" => PageChrome.AccentGreen,
            "故障" or "错误" or "异常" => PageChrome.AccentOrange,
            "超时" or "未接收" => PageChrome.AccentRed,
            "未发送" or "提示" or "跳过" => PageChrome.TextMuted,
            _ => PageChrome.TextSecondary
        };
    }

    private void ToggleRealConnection()
    {
        if (_connected)
        {
            DisconnectRealDevice("连接已断开，当前不能发送消息。");
            return;
        }

        using var dialog = new ConnectionDialog(_deviceHost, _devicePort, _recentDevices);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        if (dialog.UseDemoDevice)
        {
            StartDemoDevice();
        }
        else
        {
            StopDemoDevice();
            _deviceHost = dialog.DeviceHost;
            _devicePort = dialog.DevicePort;
        }

        ConnectToDevice();
    }

    private void StartDemoDevice()
    {
        StopDemoDevice();
        _demoDevice = LocalDemoDevice.Start();
        _deviceHost = _demoDevice.Host;
        _devicePort = _demoDevice.Port;
        _sendTextBox.Text = "PING";
        _lastSentTemplate = FindCommandTemplate("PING");
        AddPacket("系统", "模拟设备", $"本机模拟设备已启动：{_deviceHost}:{_devicePort}", "提示");
    }

    private void StopDemoDevice()
    {
        _demoDevice?.Dispose();
        _demoDevice = null;
    }

    private void ConnectToDevice()
    {
        _connectButton.Enabled = false;
        SetDeviceReply("正在连接...");
        try
        {
            var client = new TcpClient();
            client.Connect(_deviceHost, _devicePort);
            _tcpClient = client;
            _tcpStream = client.GetStream();
            _tcpStream.ReadTimeout = 3000;
            _connected = true;
            _connectButton.Text = "断开连接";
            _lastDeviceIndex = 0;
            _lastFlowText = "已连接";
            RememberCurrentDevice();
            UpdateDeviceRow("在线", "0 次", $"已连接 {_deviceHost}:{_devicePort}");
            AddPacket("系统", "控制端", $"已连接 {_deviceHost}:{_devicePort}。", "成功");
            SetDeviceReply(
                "连接成功。请输入发送内容，然后点击 2 发送测试。",
                _demoDevice is null
                    ? "连接已经打通。下一步选一条常用指令，发送后再接收设备回复。"
                    : "本机模拟设备已就绪。最简单演示：按 2 发送测试，再按 3 接收回复。");
        }
        catch (Exception ex)
        {
            CloseTcpConnection();
            StopDemoDevice();
            _connected = false;
            _lastFlowText = "连接失败";
            UpdateDeviceRow("离线", "0 次", "连接失败");
            AddPacket("故障", "控制端", $"连接失败：{ex.Message}", "故障");
            SetDeviceReply($"连接失败：{ex.Message}", "没有连上设备。先检查地址、端口、设备电源和防火墙。");
        }
        finally
        {
            _connectButton.Enabled = true;
            RefreshStatus();
        }
    }

    private void SendTestMessage()
    {
        if (!_connected || _tcpStream is null)
        {
            AddPacket("发送", "控制端", "请先连接设备，再发送测试内容。", "未发送");
            SetDeviceReply("请先连接设备。", "还没有连接设备，所以这条内容没有发出去。");
            return;
        }

        var text = _sendTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            AddPacket("发送", "控制端", "发送内容不能为空。", "未发送");
            SetDeviceReply("发送内容不能为空。", "先选择常用指令，或手动输入一条要发给设备的内容。");
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            _tcpStream.Write(bytes, 0, bytes.Length);
            _tcpStream.Flush();
            _lastSentTemplate = FindCommandTemplate(text);
            _messageCount++;
            _lastDeviceIndex = 0;
            _lastFlowText = "发送";
            UpdateDeviceRow("在线", $"{_messageCount} 次", "已发送测试内容");
            AddPacket("发送", GetCurrentDeviceDisplayName(), $"控制端发送：{Shorten(text)}", "已发送");
            SetDeviceReply(
                "内容已经发出。现在按 3 接收回复，看设备怎么回答。",
                _lastSentTemplate is null
                    ? "这是手动输入的内容，收到回复后按通用规则解释。"
                    : $"使用模板：{_lastSentTemplate.Label}；预期回复：{_lastSentTemplate.ExpectedReply}");
        }
        catch (Exception ex)
        {
            AddPacket("故障", GetCurrentDeviceDisplayName(), $"发送失败：{ex.Message}", "故障");
            SetDeviceReply($"发送失败：{ex.Message}", "发送时连接断了，通常是设备关闭、网络断开，或端口被重置。");
            DisconnectRealDevice("发送失败，连接已关闭。", addLog: false);
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void ReadRealReply()
    {
        if (!_connected || _tcpStream is null)
        {
            AddPacket("接收", "控制端", "请先连接设备，再读取回复。", "未接收");
            SetDeviceReply("请先连接设备。", "还没有连接设备，所以读不到回复。");
            return;
        }

        try
        {
            var buffer = new byte[4096];
            var count = _tcpStream.Read(buffer, 0, buffer.Length);
            if (count <= 0)
            {
                DisconnectRealDevice("设备已关闭连接。");
                SetDeviceReply("设备已关闭连接。", "设备主动断开了连接。需要重新连接后再测试。");
                return;
            }

            var response = Encoding.UTF8.GetString(buffer, 0, count).TrimEnd();
            _replyCount++;
            _lastDeviceIndex = 0;
            _lastFlowText = "接收";
            SetDeviceReply($"原始回复：{response}", ExplainResponse(response, _lastSentTemplate));
            UpdateDeviceRow("在线", $"{Math.Max(_messageCount, _replyCount)} 次", "已收到真实回复");
            AddPacket("接收", GetCurrentDeviceDisplayName(), $"真实回复：{Shorten(response)}", "已收到");
        }
        catch (IOException)
        {
            _lastFlowText = "超时";
            SetDeviceReply("3 秒内没有收到设备回复。", "设备没有及时回答。可能没收到指令、正在忙，或这条指令不需要回复。");
            AddPacket("接收", GetCurrentDeviceDisplayName(), "3 秒内没有收到设备回复。", "超时");
        }
        catch (Exception ex)
        {
            SetDeviceReply($"读取失败：{ex.Message}", "读取回复时连接异常，建议重新连接设备再测一次。");
            AddPacket("故障", GetCurrentDeviceDisplayName(), $"读取失败：{ex.Message}", "故障");
            DisconnectRealDevice("读取失败，连接已关闭。", addLog: false);
        }
        finally
        {
            RefreshStatus();
        }
    }

    private void SimulateError()
    {
        if (!_connected)
        {
            AddPacket("故障", "控制端", "请先连接设备，再模拟故障。", "未接收");
            return;
        }

        var device = _deviceRows[0];
        device.LinkState = "故障";
        device.Latency = "故障";
        device.LastPacket = DateTime.Now.ToString("HH:mm:ss");
        device.Detail = "压力超过安全范围";
        _alarmCount++;
        SetDeviceReply("模拟故障：压力超过安全范围。", "这是手动模拟的故障，用来确认日志、状态和拓扑会不会变红。");
        AddPacket("故障", device.DeviceName, "设备故障：压力超过安全范围，请检查。", "故障");
        RefreshStatus();
    }

    private void ClearRecords()
    {
        _packetRows.Clear();
        _messageCount = 0;
        _replyCount = 0;
        _alarmCount = 0;
        _lastDeviceIndex = -1;
        _lastFlowText = "已清空";
        SetDeviceReply(
            _connected ? "通信记录已清空，可以重新发送测试内容。" : "通信记录已清空。",
            _connected ? "记录已清空，连接还在，可以继续测试。" : "记录已清空。先连接设备，再发送测试内容。");

        foreach (var device in _deviceRows)
        {
            device.DeviceName = GetCurrentDeviceDisplayName();
            device.LinkState = _connected ? "在线" : "离线";
            device.Latency = "0 次";
            device.LastPacket = _connected ? DateTime.Now.ToString("HH:mm:ss") : "--";
            device.Detail = _connected ? "可以收发消息" : "等待连接";
        }

        AddPacket("系统", "控制端", "通信记录已清空，可以重新演示。", "已清空");
        RefreshStatus();
    }

    private void UpdateDeviceRow(string state, string countText, string detail)
    {
        if (_deviceRows.Count == 0)
        {
            return;
        }

        var device = _deviceRows[0];
        device.DeviceName = GetCurrentDeviceDisplayName();
        device.LinkState = state;
        device.Latency = countText;
        device.LastPacket = state == "离线" ? "--" : DateTime.Now.ToString("HH:mm:ss");
        device.Detail = detail;
    }

    private void DisconnectRealDevice(string message, bool addLog = true)
    {
        CloseTcpConnection();
        StopDemoDevice();
        _connected = false;
        _connectButton.Text = "1 连接设备";
        _lastDeviceIndex = -1;
        _lastFlowText = "已断开";
        UpdateDeviceRow("离线", "0 次", "连接已断开");
        SetDeviceReply(message, "连接已经断开。需要继续测试时，请重新连接设备。");
        if (addLog)
        {
            AddPacket("系统", "控制端", message, "成功");
        }

        RefreshStatus();
    }

    private void CloseTcpConnection()
    {
        _tcpStream?.Dispose();
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpStream = null;
        _tcpClient = null;
    }

    private static string Shorten(string text)
    {
        var value = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 80 ? value : value[..80] + "...";
    }

    private void AddPacket(string direction, string deviceName, string payload, string result)
    {
        var deviceIndex = FindDeviceIndex(deviceName);
        if (deviceIndex >= 0)
        {
            _lastDeviceIndex = deviceIndex;
        }

        _pulse++;
        _lastFlowText = direction;
        _packetRows.Insert(0, new PacketLogRow(
            DateTime.Now.ToString("HH:mm:ss"),
            direction,
            deviceName,
            payload,
            result));

        while (_packetRows.Count > 80)
        {
            _packetRows.RemoveAt(_packetRows.Count - 1);
        }
    }

    private int FindDeviceIndex(string deviceName)
    {
        for (var index = 0; index < _deviceRows.Count; index++)
        {
            if (_deviceRows[index].DeviceName == deviceName)
            {
                return index;
            }
        }

        return -1;
    }

    private void RefreshStatus()
    {
        var onlineCount = _deviceRows.Count(row => row.LinkState == "在线" || row.LinkState == "故障");
        var issueCount = _deviceRows.Count(row => row.LinkState == "故障");

        _connectionValueLabel.Text = _connected ? "已连接" : "未连接";
        _connectionValueLabel.ForeColor = _connected ? PageChrome.AccentGreen : PageChrome.TextPrimary;
        _connectionNoteLabel.Text = _connected
            ? $"已连接 {_deviceHost}:{_devicePort}"
            : _hasSelectedDeviceEndpoint ? $"待连接 {_deviceHost}:{_devicePort}" : "连接后才能发送测试消息";
        _onlineValueLabel.Text = $"{onlineCount}/{_deviceRows.Count}";
        _onlineNoteLabel.Text = issueCount > 0
            ? $"{issueCount} 台设备处于故障状态"
            : _connected
                ? $"当前设备 {GetCurrentDeviceDisplayName()} {_deviceHost}:{_devicePort}"
                : HasSelectedDevice() ? $"待测：{GetCurrentDeviceDisplayName()}" : "当前没有设备在线";
        _latencyValueLabel.Text = _lastFlowText;
        _latencyNoteLabel.Text = _connected
            ? $"已发送 {_messageCount} 条，已收到 {_replyCount} 条"
            : "最近做了什么会显示在这里";
        _alarmValueLabel.Text = _alarmCount.ToString();
        _alarmNoteLabel.Text = _alarmCount == 0
            ? "设备故障会写入通信记录"
            : $"已记录 {_alarmCount} 次设备故障";
        _infoLabel.Text = _connected
            ? "请继续按 2 发送测试、按 3 接收回复；需要演示故障时按 4。"
            : _hasSelectedDeviceEndpoint
                ? $"已带入 {GetCurrentDeviceDisplayName()}，请按 1 连接设备。"
                : HasSelectedDevice() ? "已带入设备，但通信地址格式不正确。" : "请先按 1 连接设备。";

        _deviceRows.ResetBindings();
        _topologyCanvas.Connected = _connected;
        _topologyCanvas.Pulse = _pulse;
        _topologyCanvas.ActiveDeviceIndex = _lastDeviceIndex;
        _topologyCanvas.FlowText = _lastFlowText;
        _topologyCanvas.Invalidate();
    }

    private bool HasSelectedDevice()
    {
        return !string.IsNullOrWhiteSpace(_selectedCommunicationAddress) ||
            !string.Equals(_selectedDeviceName, "待测设备", StringComparison.Ordinal);
    }

    private string GetCurrentDeviceDisplayName()
    {
        return string.IsNullOrWhiteSpace(_selectedLineName)
            ? _selectedDeviceName
            : $"{_selectedLineName} / {_selectedDeviceName}";
    }

    private sealed class CommunicationCommandTemplate
    {
        public CommunicationCommandTemplate(
            string label,
            string commandText,
            string purpose,
            string expectedReply,
            IReadOnlyList<string> successKeywords,
            string successExplanation,
            string errorAdvice)
        {
            Label = label;
            CommandText = commandText;
            Purpose = purpose;
            ExpectedReply = expectedReply;
            SuccessKeywords = successKeywords;
            SuccessExplanation = successExplanation;
            ErrorAdvice = errorAdvice;
        }

        public string Label { get; }

        public string CommandText { get; }

        public string Purpose { get; }

        public string ExpectedReply { get; }

        private IReadOnlyList<string> SuccessKeywords { get; }

        private string SuccessExplanation { get; }

        private string ErrorAdvice { get; }

        public string Explain(string response)
        {
            var value = response.Trim();
            if (value.Length == 0)
            {
                return $"设备返回了空内容。{Label} 的预期回复是：{ExpectedReply}";
            }

            if (value.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
                || value.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                return $"设备返回错误：{ErrorAdvice}";
            }

            if (SuccessKeywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return SuccessExplanation;
            }

            return $"已收到回复，但没有命中“{Label}”的预期结果。预期：{ExpectedReply}。请对照设备协议确认。";
        }

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed class CommunicationCommandTemplateConfig
    {
        public string? Label { get; set; }

        public string? CommandText { get; set; }

        public string? Purpose { get; set; }

        public string? ExpectedReply { get; set; }

        public string[]? SuccessKeywords { get; set; }

        public string? SuccessExplanation { get; set; }

        public string? ErrorAdvice { get; set; }

        public CommunicationCommandTemplate? ToTemplate()
        {
            if (string.IsNullOrWhiteSpace(Label)
                || string.IsNullOrWhiteSpace(CommandText)
                || string.IsNullOrWhiteSpace(Purpose)
                || string.IsNullOrWhiteSpace(ExpectedReply)
                || SuccessKeywords is not { Length: > 0 }
                || string.IsNullOrWhiteSpace(SuccessExplanation)
                || string.IsNullOrWhiteSpace(ErrorAdvice))
            {
                return null;
            }

            var successKeywords = SuccessKeywords
                .Select(keyword => keyword.Trim())
                .Where(keyword => keyword.Length > 0)
                .ToArray();
            if (successKeywords.Length == 0)
            {
                return null;
            }

            return new CommunicationCommandTemplate(
                Label.Trim(),
                CommandText.Trim(),
                Purpose.Trim(),
                ExpectedReply.Trim(),
                successKeywords,
                SuccessExplanation.Trim(),
                ErrorAdvice.Trim());
        }
    }

    private sealed class DeviceStatusRow
    {
        public DeviceStatusRow(string deviceName, string linkState, string latency, string lastPacket, string detail)
        {
            DeviceName = deviceName;
            LinkState = linkState;
            Latency = latency;
            LastPacket = lastPacket;
            Detail = detail;
        }

        public string DeviceName { get; set; }

        public string LinkState { get; set; }

        public string Latency { get; set; }

        public string LastPacket { get; set; }

        public string Detail { get; set; }
    }

    private sealed class PacketLogRow
    {
        public PacketLogRow(string time, string direction, string deviceName, string payload, string result)
        {
            Time = time;
            Direction = direction;
            DeviceName = deviceName;
            Payload = payload;
            Result = result;
        }

        public string Time { get; }

        public string Direction { get; }

        public string DeviceName { get; }

        public string Payload { get; }

        public string Result { get; }
    }

    private sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
        }
    }

    private sealed class LocalDemoDevice : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellation = new();

        private LocalDemoDevice(TcpListener listener, int port)
        {
            _listener = listener;
            Port = port;
        }

        public string Host => "127.0.0.1";

        public int Port { get; }

        public static LocalDemoDevice Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var device = new LocalDemoDevice(listener, port);
            _ = Task.Run(device.AcceptClientsAsync);
            return device;
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            _cancellation.Dispose();
        }

        private async Task AcceptClientsAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using var activeClient = client;
            using var stream = activeClient.GetStream();
            var buffer = new byte[4096];

            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    var count = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellation.Token);
                    if (count <= 0)
                    {
                        return;
                    }

                    var request = Encoding.UTF8.GetString(buffer, 0, count).Trim();
                    var reply = Encoding.UTF8.GetBytes(GetReply(request) + Environment.NewLine);
                    await stream.WriteAsync(reply, 0, reply.Length, _cancellation.Token);
                    await stream.FlushAsync(_cancellation.Token);
                }
                catch (IOException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private static string GetReply(string request)
        {
            return request.Trim().ToUpperInvariant() switch
            {
                "PING" => "ECHO: PING",
                "READ_STATUS" => "STATUS: RUNNING",
                "READ_ALARM" => "NO_ALARM",
                "RESET_FAULT" => "RESET_OK",
                "" => "ERR: EMPTY_COMMAND",
                _ => "ERR: UNKNOWN_COMMAND"
            };
        }
    }

    private sealed class ConnectionDialog : Form
    {
        private readonly ComboBox _historyComboBox;
        private readonly TextBox _hostTextBox;
        private readonly TextBox _portTextBox;

        public ConnectionDialog(string host, int port, IReadOnlyList<string> recentDevices)
        {
            Text = "连接设备";
            ClientSize = new Size(560, 360);
            MinimumSize = SizeFromClientSize(new Size(560, 360));
            MaximumSize = SizeFromClientSize(new Size(720, 520));
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageChrome.PageBackground;
            Font = new Font("Microsoft YaHei UI", 9F);

            _historyComboBox = CreateDialogComboBox(recentDevices);
            _historyComboBox.Name = "HistoryComboBox";
            _historyComboBox.SelectedIndexChanged += (_, _) => ApplyHistorySelection();
            _hostTextBox = CreateDialogTextBox(host);
            _hostTextBox.Name = "HostTextBox";
            _portTextBox = CreateDialogTextBox(port.ToString());
            _portTextBox.Name = "PortTextBox";

            if (_historyComboBox.Items.Count > 0)
            {
                _historyComboBox.SelectedIndex = 0;
            }

            var demoButton = CreateDialogButton("一键演示", PageChrome.AccentBlue, true);
            demoButton.Click += (_, _) =>
            {
                UseDemoDevice = true;
                DialogResult = DialogResult.OK;
                Close();
            };

            var connectButton = CreateDialogButton("连接", PageChrome.AccentGreen, true);
            connectButton.Click += (_, _) => Confirm();

            var cancelButton = CreateDialogButton("取消", PageChrome.SurfaceBorder, false);
            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = connectButton;
            CancelButton = cancelButton;

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, 18, 0, 0)
            };
            actions.Controls.Add(connectButton);
            actions.Controls.Add(cancelButton);
            actions.Controls.Add(demoButton);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(26, 26, 26, 24)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));

            var tipLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = PageChrome.TextMuted,
                Text = "有真实设备就填地址；没有设备点一键演示。",
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(tipLabel, 0, 0);
            layout.SetColumnSpan(tipLabel, 2);
            layout.Controls.Add(CreateDialogLabel("最近设备"), 0, 1);
            layout.Controls.Add(_historyComboBox, 1, 1);
            layout.Controls.Add(CreateDialogLabel("设备地址"), 0, 2);
            layout.Controls.Add(_hostTextBox, 1, 2);
            layout.Controls.Add(CreateDialogLabel("端口"), 0, 3);
            layout.Controls.Add(_portTextBox, 1, 3);
            layout.Controls.Add(actions, 0, 5);
            layout.SetColumnSpan(actions, 2);

            Controls.Add(layout);
        }

        public string DeviceHost { get; private set; } = string.Empty;

        public int DevicePort { get; private set; }

        public bool UseDemoDevice { get; private set; }

        private void ApplyHistorySelection()
        {
            if (_historyComboBox.SelectedItem is not string endpoint)
            {
                return;
            }

            if (TryParseEndpoint(endpoint, out var host, out var port))
            {
                _hostTextBox.Text = host;
                _portTextBox.Text = port.ToString();
            }
        }

        private void Confirm()
        {
            var host = _hostTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "请输入设备地址。", "连接设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(_portTextBox.Text.Trim(), out var port))
            {
                MessageBox.Show(this, "端口必须是数字。", "连接设备", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DeviceHost = host;
            DevicePort = port;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static Label CreateDialogLabel(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = PageChrome.TextSecondary,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static TextBox CreateDialogTextBox(string text)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = PageChrome.InputBackground,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = PageChrome.TextPrimary,
                Margin = new Padding(0, 4, 0, 8),
                Text = text
            };
        }

        private static Button CreateDialogButton(string text, Color accent, bool filled)
        {
            var button = new Button
            {
                AutoEllipsis = false,
                AutoSize = false,
                BackColor = filled ? accent : PageChrome.SurfaceRaised,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = PageChrome.TextPrimary,
                Margin = new Padding(8, 0, 0, 0),
                Padding = Padding.Empty,
                Size = new Size(104, 42),
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(filled ? 160 : 88, accent);
            button.FlatAppearance.MouseOverBackColor = filled
                ? PageChrome.MixColor(accent, Color.White, 0.12f)
                : Color.FromArgb(36, accent);
            button.FlatAppearance.MouseDownBackColor = filled
                ? PageChrome.MixColor(accent, Color.Black, 0.12f)
                : Color.FromArgb(58, accent);
            return button;
        }

        private static ComboBox CreateDialogComboBox(IReadOnlyList<string> recentDevices)
        {
            var comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                BackColor = PageChrome.InputBackground,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = PageChrome.TextPrimary,
                Margin = new Padding(0, 4, 0, 8),
                Enabled = recentDevices.Count > 0
            };

            if (recentDevices.Count == 0)
            {
                comboBox.Items.Add("暂无历史连接");
                return comboBox;
            }

            foreach (var device in recentDevices)
            {
                comboBox.Items.Add(device);
            }

            return comboBox;
        }
    }

    private sealed class TopologyCanvas : Control
    {
        private readonly IReadOnlyList<DeviceStatusRow> _devices;

        public TopologyCanvas(IReadOnlyList<DeviceStatusRow> devices)
        {
            _devices = devices;
            BackColor = PageChrome.SurfaceBackground;
            MinimumSize = new Size(360, 300);
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

        public bool Connected { get; set; }

        public int Pulse { get; set; }

        public int ActiveDeviceIndex { get; set; } = -1;

        public string FlowText { get; set; } = "等待连接";

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            using var brush = new SolidBrush(PageChrome.SurfaceBackground);
            pevent.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawAtmosphere(g);
            DrawGateway(g);
            DrawDevices(g);
            DrawLegend(g);
        }

        private void DrawAtmosphere(Graphics g)
        {
            var bounds = ClientRectangle;
            using var glowBrush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(34, PageChrome.AccentBlue),
                Color.FromArgb(4, PageChrome.AccentCyan),
                LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(glowBrush, bounds);

            for (var index = 0; index < 5; index++)
            {
                var size = Math.Max(80, Math.Min(Width, Height) - index * 52);
                var rect = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                using var pen = new Pen(Color.FromArgb(Connected ? 20 : 8, PageChrome.AccentBlue), 1F);
                g.DrawEllipse(pen, rect);
            }
        }

        private void DrawGateway(Graphics g)
        {
            var center = new Point(Width / 2, Height / 2);
            var radius = Math.Max(46, Math.Min(74, Math.Min(Width, Height) / 7));
            var gatewayRect = new Rectangle(center.X - radius, center.Y - radius, radius * 2, radius * 2);

            using var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillEllipse(shadowBrush, Rectangle.Inflate(gatewayRect, 10, 10));

            using var fillBrush = new LinearGradientBrush(
                gatewayRect,
                Color.FromArgb(58, 70, 96),
                Color.FromArgb(22, 26, 38),
                90F);
            g.FillEllipse(fillBrush, gatewayRect);

            using var borderPen = new Pen(Connected ? PageChrome.AccentGreen : PageChrome.SurfaceBorder, 2F);
            g.DrawEllipse(borderPen, gatewayRect);

            if (Connected)
            {
                var pulseSize = radius * 2 + 16 + (Pulse % 4) * 9;
                var pulseRect = new Rectangle(center.X - pulseSize / 2, center.Y - pulseSize / 2, pulseSize, pulseSize);
                using var pulsePen = new Pen(Color.FromArgb(86 - (Pulse % 4) * 18, PageChrome.AccentGreen), 2F);
                g.DrawEllipse(pulsePen, pulseRect);
            }

            using var titleFont = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            using var noteFont = new Font("Microsoft YaHei UI", 8.5F);
            using var titleBrush = new SolidBrush(PageChrome.TextPrimary);
            using var noteBrush = new SolidBrush(PageChrome.TextMuted);
            DrawCenteredText(g, "控制端", titleFont, titleBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 34, gatewayRect.Width, 26));
            DrawCenteredText(g, Connected ? "已连接" : "未连接", noteFont, noteBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 60, gatewayRect.Width, 24));
        }

        private void DrawDevices(Graphics g)
        {
            if (_devices.Count == 0)
            {
                return;
            }

            var center = new Point(Width / 2, Height / 2);
            var layoutRadiusX = Math.Max(130, Width / 2 - 110);
            var layoutRadiusY = Math.Max(96, Height / 2 - 82);
            for (var index = 0; index < _devices.Count; index++)
            {
                var angle = -Math.PI / 2 + index * (Math.PI * 2 / _devices.Count);
                var nodeCenter = new Point(
                    center.X + (int)(Math.Cos(angle) * layoutRadiusX),
                    center.Y + (int)(Math.Sin(angle) * layoutRadiusY));

                var stateColor = _devices[index].LinkState switch
                {
                    "在线" => PageChrome.AccentGreen,
                    "故障" or "错误" or "异常" => PageChrome.AccentOrange,
                    "超时" => PageChrome.AccentRed,
                    _ => PageChrome.SurfaceBorder
                };

                var active = index == ActiveDeviceIndex;
                using var linePen = new Pen(Color.FromArgb(active ? 180 : Connected ? 92 : 32, stateColor), active ? 3.2F : 2F);
                g.DrawLine(linePen, center, nodeCenter);
                if (active && Connected)
                {
                    DrawFlowTag(g, center, nodeCenter, FlowText, stateColor);
                }

                DrawDeviceNode(g, nodeCenter, _devices[index], stateColor, active);
            }
        }

        private static void DrawDeviceNode(Graphics g, Point center, DeviceStatusRow device, Color stateColor, bool active)
        {
            const int width = 132;
            const int height = 58;
            var rect = new Rectangle(center.X - width / 2, center.Y - height / 2, width, height);
            using var path = PageChrome.CreateRoundedPath(rect, 18);

            using var shadowBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0));
            using var shadowPath = PageChrome.CreateRoundedPath(new Rectangle(rect.X + 2, rect.Y + 5, rect.Width, rect.Height), 18);
            g.FillPath(shadowBrush, shadowPath);

            using var fillBrush = new SolidBrush(Color.FromArgb(238, 25, 29, 41));
            using var tintBrush = new SolidBrush(Color.FromArgb(device.LinkState == "离线" ? 10 : 24, stateColor));
            using var borderPen = new Pen(Color.FromArgb(active ? 220 : device.LinkState == "离线" ? 76 : 150, stateColor), active ? 2F : 1.3F);
            g.FillPath(fillBrush, path);
            g.FillPath(tintBrush, path);
            g.DrawPath(borderPen, path);

            using var nameFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            using var detailFont = new Font("Microsoft YaHei UI", 8F);
            using var nameBrush = new SolidBrush(PageChrome.TextPrimary);
            using var detailBrush = new SolidBrush(PageChrome.TextMuted);
            g.DrawString(device.DeviceName, nameFont, nameBrush, rect.X + 12, rect.Y + 10);
            g.DrawString($"{device.LinkState} / {device.Latency}", detailFont, detailBrush, rect.X + 12, rect.Y + 32);
        }

        private static void DrawFlowTag(Graphics g, Point from, Point to, string text, Color accent)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var x = (from.X + to.X) / 2;
            var y = (from.Y + to.Y) / 2;
            var rect = new Rectangle(x - 42, y - 13, 84, 26);
            using var path = PageChrome.CreateRoundedPath(rect, 10);
            using var fillBrush = new SolidBrush(Color.FromArgb(230, 18, 22, 31));
            using var borderPen = new Pen(Color.FromArgb(150, accent), 1F);
            using var font = new Font("Microsoft YaHei UI", 8F, FontStyle.Bold);
            using var brush = new SolidBrush(accent);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);
            DrawCenteredText(g, text, font, brush, rect);
        }

        private void DrawLegend(Graphics g)
        {
            using var font = new Font("Microsoft YaHei UI", 8.5F);
            using var brush = new SolidBrush(PageChrome.TextMuted);
            g.DrawString("Demo: 控制端发送测试消息，设备返回确认；故障设备会变色并写入记录。", font, brush, 18, Height - 32);
        }

        private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, Rectangle bounds)
        {
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            g.DrawString(text, font, brush, bounds, format);
        }
    }
}
