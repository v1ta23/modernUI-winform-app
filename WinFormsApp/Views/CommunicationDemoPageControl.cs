using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinFormsApp.Views;

internal sealed class CommunicationDemoPageControl : UserControl, IInteractiveResizeAware
{
    private readonly BindingList<DeviceStatusRow> _deviceRows = new();
    private readonly BindingList<PacketLogRow> _packetRows = new();
    private readonly System.Windows.Forms.Timer _simulationTimer;
    private readonly Random _random = new(17);
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
    private readonly TopologyCanvas _topologyCanvas;
    private readonly DataGridView _deviceGrid;
    private readonly DataGridView _packetGrid;

    private bool _connected;
    private int _pulse;
    private int _alarmCount;

    public CommunicationDemoPageControl()
    {
        Dock = DockStyle.Fill;
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _infoLabel = PageChrome.CreateInfoLabel("Demo 模式：还没有接真实设备，先演示页面和交互。");
        _connectionValueLabel = PageChrome.CreateValueLabel(18F, "未连接");
        _onlineValueLabel = PageChrome.CreateValueLabel(18F, "0/4");
        _latencyValueLabel = PageChrome.CreateValueLabel(18F, "-- ms");
        _alarmValueLabel = PageChrome.CreateValueLabel(18F, "0");
        _connectionNoteLabel = PageChrome.CreateNoteLabel("点击连接后开始模拟网关上报");
        _onlineNoteLabel = PageChrome.CreateNoteLabel("当前没有设备在线");
        _latencyNoteLabel = PageChrome.CreateNoteLabel("等待第一条心跳");
        _alarmNoteLabel = PageChrome.CreateNoteLabel("异常包会进入右侧日志");

        _connectButton = PageChrome.CreateActionButton("连接网关", PageChrome.AccentGreen, true);
        var heartbeatButton = PageChrome.CreateActionButton("发送心跳", PageChrome.AccentBlue, false);
        var faultButton = PageChrome.CreateActionButton("模拟异常", PageChrome.AccentOrange, false);

        _connectButton.Click += (_, _) => ToggleConnection();
        heartbeatButton.Click += (_, _) => SendHeartbeat();
        faultButton.Click += (_, _) => SimulateFault();

        _topologyCanvas = new TopologyCanvas(_deviceRows)
        {
            Dock = DockStyle.Fill
        };

        _deviceGrid = CreateGrid();
        _packetGrid = CreateGrid();
        ConfigureDeviceGrid();
        ConfigurePacketGrid();

        _simulationTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _simulationTimer.Tick += (_, _) => SimulatePacket();

        SeedDemoData();
        _layoutRoot = BuildLayout(_connectButton, heartbeatButton, faultButton);
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
            _simulationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control BuildLayout(Button connectButton, Button heartbeatButton, Button faultButton)
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
            "通信功能 Demo",
            "先看效果：模拟网关连接、设备心跳、异常上报和命令下发，不改正式业务。",
            _infoLabel,
            faultButton,
            heartbeatButton,
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
        metrics.Controls.Add(PageChrome.CreateMetricCard("在线设备", PageChrome.AccentCyan, _onlineValueLabel, _onlineNoteLabel), 1, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("最近延迟", PageChrome.AccentBlue, _latencyValueLabel, _latencyNoteLabel), 2, 0);
        metrics.Controls.Add(PageChrome.CreateMetricCard("异常上报", PageChrome.AccentOrange, _alarmValueLabel, _alarmNoteLabel, new Padding(0)), 3, 0);
        return metrics;
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

        var topologyShell = PageChrome.CreateSectionShell(
            "通信拓扑",
            "左边网关负责收设备包，右边设备节点会跟着模拟数据变色。",
            out _,
            _topologyCanvas,
            new Padding(0, 0, 12, 0));

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
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 43F));
        rightSide.RowStyles.Add(new RowStyle(SizeType.Percent, 57F));
        rightSide.Controls.Add(PageChrome.CreateSectionShell("设备状态", "这一块以后可以接真实设备状态表。", out _, _deviceGrid), 0, 0);
        rightSide.Controls.Add(PageChrome.CreateSectionShell("通信包日志", "模拟收到心跳、状态包、异常包和下发命令。", out _, _packetGrid, new Padding(0)), 0, 1);

        workspace.Controls.Add(topologyShell, 0, 0);
        workspace.Controls.Add(rightSide, 1, 0);
        return workspace;
    }

    private void SeedDemoData()
    {
        _deviceRows.Add(new DeviceStatusRow("PLC-A01", "离线", "--", "--", "等待连接"));
        _deviceRows.Add(new DeviceStatusRow("泵站-B02", "离线", "--", "--", "等待连接"));
        _deviceRows.Add(new DeviceStatusRow("温控-C03", "离线", "--", "--", "等待连接"));
        _deviceRows.Add(new DeviceStatusRow("输送-D04", "离线", "--", "--", "等待连接"));

        _deviceGrid.DataSource = _deviceRows;
        _packetGrid.DataSource = _packetRows;
        AddPacket("SYS", "网关", "Demo 页面已就绪，等待连接。", "待连接");
    }

    private void ConfigureDeviceGrid()
    {
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.DeviceName), "设备", 100));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LinkState), "链路", 76));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Latency), "延迟", 70));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.LastPacket), "最后上报", 92));
        _deviceGrid.Columns.Add(CreateColumn(nameof(DeviceStatusRow.Detail), "说明", 140));
    }

    private void ConfigurePacketGrid()
    {
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Time), "时间", 78));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Direction), "方向", 56));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.DeviceName), "设备", 92));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Payload), "内容", 180));
        _packetGrid.Columns.Add(CreateColumn(nameof(PacketLogRow.Result), "结果", 74));
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
            MinimumWidth = 58
        };
    }

    private void ToggleConnection()
    {
        _connected = !_connected;
        _connectButton.Text = _connected ? "断开网关" : "连接网关";

        if (_connected)
        {
            foreach (var device in _deviceRows)
            {
                device.LinkState = "在线";
                device.Latency = $"{_random.Next(18, 48)} ms";
                device.LastPacket = DateTime.Now.ToString("HH:mm:ss");
                device.Detail = "心跳正常";
            }

            _simulationTimer.Start();
            AddPacket("SYS", "网关", "已连接 tcp://demo-gateway:9001", "成功");
        }
        else
        {
            _simulationTimer.Stop();
            foreach (var device in _deviceRows)
            {
                device.LinkState = "离线";
                device.Latency = "--";
                device.LastPacket = "--";
                device.Detail = "连接已断开";
            }

            AddPacket("SYS", "网关", "连接已断开，停止接收设备包。", "断开");
        }

        RefreshStatus();
    }

    private void SendHeartbeat()
    {
        if (!_connected)
        {
            AddPacket("TX", "网关", "PING 被拦截：请先连接网关。", "未发送");
            return;
        }

        AddPacket("TX", "网关", "PING / 请求全部设备立即上报", "已发送");
        SimulatePacket();
    }

    private void SimulateFault()
    {
        if (!_connected)
        {
            AddPacket("RX", "泵站-B02", "FAULT 被拦截：网关未连接。", "未接收");
            return;
        }

        var device = _deviceRows[1];
        device.LinkState = "异常";
        device.Latency = $"{_random.Next(72, 118)} ms";
        device.LastPacket = DateTime.Now.ToString("HH:mm:ss");
        device.Detail = "压力波动超过阈值";
        _alarmCount++;
        AddPacket("RX", device.DeviceName, "FAULT pressure=0.82MPa code=P-204", "异常");
        RefreshStatus();
    }

    private void SimulatePacket()
    {
        if (!_connected || _deviceRows.Count == 0)
        {
            return;
        }

        _pulse++;
        var device = _deviceRows[_pulse % _deviceRows.Count];
        var latency = _random.Next(16, 64);
        device.LinkState = "在线";
        device.Latency = $"{latency} ms";
        device.LastPacket = DateTime.Now.ToString("HH:mm:ss");
        device.Detail = _pulse % 5 == 0 ? "状态包已刷新" : "心跳正常";

        var payload = _pulse % 5 == 0
            ? $"STATUS speed={_random.Next(72, 96)}% temp={_random.Next(28, 43)}C"
            : "HEARTBEAT seq=" + _pulse;
        AddPacket("RX", device.DeviceName, payload, "正常");
        RefreshStatus();
    }

    private void AddPacket(string direction, string deviceName, string payload, string result)
    {
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

    private void RefreshStatus()
    {
        var onlineCount = _deviceRows.Count(row => row.LinkState == "在线" || row.LinkState == "异常");
        var abnormalCount = _deviceRows.Count(row => row.LinkState == "异常");
        var latestLatency = _deviceRows
            .Where(row => row.Latency.EndsWith(" ms", StringComparison.Ordinal))
            .Select(row => int.TryParse(row.Latency.Replace(" ms", string.Empty), out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        _connectionValueLabel.Text = _connected ? "已连接" : "未连接";
        _connectionValueLabel.ForeColor = _connected ? PageChrome.AccentGreen : PageChrome.TextPrimary;
        _connectionNoteLabel.Text = _connected ? "Demo 网关正在模拟收包" : "点击连接后开始模拟网关上报";
        _onlineValueLabel.Text = $"{onlineCount}/{_deviceRows.Count}";
        _onlineNoteLabel.Text = abnormalCount > 0 ? $"{abnormalCount} 台设备处于异常态" : "在线设备心跳正常";
        _latencyValueLabel.Text = latestLatency > 0 ? $"{latestLatency} ms" : "-- ms";
        _latencyNoteLabel.Text = latestLatency > 64 ? "最近一次延迟偏高" : "按收到的包动态刷新";
        _alarmValueLabel.Text = _alarmCount.ToString();
        _alarmNoteLabel.Text = _alarmCount == 0 ? "异常包会进入右侧日志" : "已有模拟异常上报";
        _infoLabel.Text = _connected
            ? $"Demo 模式：最近刷新 {DateTime.Now:HH:mm:ss}，真实开发时这里接 TCP/HTTP 服务。"
            : "Demo 模式：还没有接真实设备，先演示页面和交互。";

        _deviceRows.ResetBindings();
        _topologyCanvas.Connected = _connected;
        _topologyCanvas.Pulse = _pulse;
        _topologyCanvas.Invalidate();
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

        public string DeviceName { get; }

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
            DrawCenteredText(g, "网关", titleFont, titleBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 34, gatewayRect.Width, 26));
            DrawCenteredText(g, Connected ? "TCP Demo" : "未连接", noteFont, noteBrush, new Rectangle(gatewayRect.X, gatewayRect.Y + 60, gatewayRect.Width, 24));
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
                    "异常" => PageChrome.AccentOrange,
                    _ => PageChrome.SurfaceBorder
                };

                using var linePen = new Pen(Color.FromArgb(Connected ? 92 : 32, stateColor), 2F);
                g.DrawLine(linePen, center, nodeCenter);
                DrawDeviceNode(g, nodeCenter, _devices[index], stateColor);
            }
        }

        private static void DrawDeviceNode(Graphics g, Point center, DeviceStatusRow device, Color stateColor)
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
            using var borderPen = new Pen(Color.FromArgb(device.LinkState == "离线" ? 76 : 150, stateColor), 1.3F);
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

        private void DrawLegend(Graphics g)
        {
            using var font = new Font("Microsoft YaHei UI", 8.5F);
            using var brush = new SolidBrush(PageChrome.TextMuted);
            g.DrawString("Demo: 页面用定时器模拟收包，后面可替换成真实 TCP/HTTP 通信服务。", font, brush, 18, Height - 32);
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
