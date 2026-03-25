using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace WinFormsLightBlueGlassDemo
{
    internal sealed class BufferedPanel : Panel
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
    }

    public partial class MainForm : Form
    {
        private readonly struct ThemePalette
        {
            public ThemePalette(
                Color background,
                Color card,
                Color cardHover,
                Color sidebar,
                Color textPrimary,
                Color textSecondary,
                Color textMuted,
                Color border,
                Color glass)
            {
                Background = background;
                Card = card;
                CardHover = cardHover;
                Sidebar = sidebar;
                TextPrimary = textPrimary;
                TextSecondary = textSecondary;
                TextMuted = textMuted;
                Border = border;
                Glass = glass;
            }

            public Color Background { get; }
            public Color Card { get; }
            public Color CardHover { get; }
            public Color Sidebar { get; }
            public Color TextPrimary { get; }
            public Color TextSecondary { get; }
            public Color TextMuted { get; }
            public Color Border { get; }
            public Color Glass { get; }
        }

        // ==================== DWM / Acrylic APIs ====================
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        // ==================== 颜色主题 ====================
        private static readonly Color BgDark = Color.FromArgb(10, 10, 15);
        private static readonly Color BgCard = Color.FromArgb(28, 30, 40);
        private static readonly Color BgCardHover = Color.FromArgb(40, 42, 55);
        private static readonly Color BgSidebar = Color.FromArgb(16, 16, 22);
        private static readonly Color AccentBlue = Color.FromArgb(88, 130, 255);
        private static readonly Color AccentPurple = Color.FromArgb(148, 90, 255);
        private static readonly Color AccentCyan = Color.FromArgb(50, 210, 220);
        private static readonly Color AccentGreen = Color.FromArgb(76, 217, 140);
        private static readonly Color AccentOrange = Color.FromArgb(255, 165, 70);
        private static readonly Color AccentPink = Color.FromArgb(255, 100, 150);
        private static readonly ThemePalette DarkTheme = new(
            BgDark,
            BgCard,
            BgCardHover,
            BgSidebar,
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(210, 215, 230),
            Color.FromArgb(160, 170, 190),
            Color.FromArgb(80, 85, 110),
            Color.FromArgb(204, 10, 10, 15));
        private static readonly ThemePalette LightTheme = new(
            Color.FromArgb(225, 230, 238),
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(248, 250, 252),
            Color.FromArgb(215, 222, 235),
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(40, 45, 50),
            Color.FromArgb(90, 100, 115),
            Color.FromArgb(150, 160, 180),
            Color.FromArgb(110, 225, 230, 238));

        // ==================== 动画相关 ====================
        private System.Windows.Forms.Timer _animTimer = null!;
        private float _animProgress = 0f;
        private int _activeNavIndex = 0;
        private readonly List<Panel> _navItems = new List<Panel>();
        private bool _isDarkTheme = true;
        private Panel _navIndicator = null!;
        private Panel _sidebar = null!;
        private Panel _mainArea = null!;
        private Button _themeToggleButton = null!;

        public MainForm()
        {
            // 基础设置
            this.Text = "Glass Dashboard · 毛玻璃仪表板";
            this.Size = new Size(1280, 780);
            this.MinimumSize = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = CurrentTheme.Background;
            this.DoubleBuffered = true;
            this.Font = new Font("Segoe UI", 9F);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();

            // 构建界面
            BuildLayout();
            ApplyTheme();

            // 入场动画
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
        }

        // ==================== 启用深色标题栏 (Win11优先) ====================
        private ThemePalette CurrentTheme => _isDarkTheme ? DarkTheme : LightTheme;

        private Color MainAreaBackground => _isDarkTheme
            ? CurrentTheme.Background
            : Color.FromArgb(225, 230, 238);

        private void SetDarkTitleBar()
        {
            try
            {
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
                int darkMode = _isDarkTheme ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, 20, ref darkMode, sizeof(int));

                // DWMWA_WINDOW_CORNER_PREFERENCE = 33 (圆角 = 2)
                int roundCorner = 2;
                DwmSetWindowAttribute(this.Handle, 33, ref roundCorner, sizeof(int));
            }
            catch { /* Win10 可能不支持部分属性 */ }
        }

        // ==================== 毛玻璃/亚克力效果 ====================
        private void EnableAcrylicBlur()
        {
            try
            {
                // AccentState = 4 → ACCENT_ENABLE_ACRYLICBLURBEHIND (Win10 1803+)
                // AccentState = 3 → ACCENT_ENABLE_BLURBEHIND (Win10 fallback)
                var accent = new AccentPolicy
                {
                    AccentState = 3,
                    AccentFlags = 2,
                    GradientColor = ToAbgr(CurrentTheme.Glass)
                };

                int accentSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19,  // WCA_ACCENT_POLICY
                    Data = accentPtr,
                    SizeOfData = accentSize
                };

                SetWindowCompositionAttribute(this.Handle, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { /* 降级处理 */ }
        }

        // ==================== 构建主布局 ====================
        private void BuildLayout()
        {
            SuspendLayout();

            // 右侧主区域（先添加 Fill，再添加 Left，WinForms 按逆序 Dock）
            var mainArea = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = MainAreaBackground,
                Padding = new Padding(30, 20, 30, 20)
            };
            _mainArea = mainArea;

            // 底部内容区（Fill 面板要最先加入 mainArea）
            mainArea.SuspendLayout();
            var bottomPanel = CreateBottomArea();
            mainArea.Controls.Add(bottomPanel);

            // 统计卡片区
            var cardsPanel = CreateCardsArea();
            mainArea.Controls.Add(cardsPanel);

            // 顶部标题区
            var headerPanel = CreateHeader();
            mainArea.Controls.Add(headerPanel);

            this.Controls.Add(mainArea);

            // 左侧导航栏（后添加 → Dock 优先级更高 → 不被遮挡）
            var sidebar = CreateSidebar();
            this.Controls.Add(sidebar);
            mainArea.ResumeLayout(false);
            ResumeLayout(false);
        }

        private void ApplyTheme()
        {
            var theme = CurrentTheme;
            BackColor = theme.Background;
            if (_mainArea != null)
                _mainArea.BackColor = MainAreaBackground;
            if (_sidebar != null)
                _sidebar.BackColor = theme.Sidebar;

            SetDarkTitleBar();
            EnableAcrylicBlur();
            UpdateThemeToggleButton();
            InvalidateControlTree(this);
        }

        private void UpdateThemeToggleButton()
        {
            if (_themeToggleButton == null)
                return;

            _themeToggleButton.Text = _isDarkTheme ? "☀" : "☾";
            _themeToggleButton.ForeColor = _isDarkTheme ? AccentOrange : AccentBlue;
            _themeToggleButton.BackColor = _isDarkTheme
                ? Color.FromArgb(28, 255, 255, 255)
                : Color.FromArgb(246, 251, 255);
            _themeToggleButton.FlatAppearance.BorderColor = _isDarkTheme
                ? Color.FromArgb(55, 255, 255, 255)
                : CurrentTheme.Border;
        }

        private void ToggleTheme()
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme();
        }

        // ==================== 侧栏 ====================
        private Panel CreateSidebar()
        {
            var sidebar = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = 72,
                BackColor = CurrentTheme.Sidebar,
                Padding = new Padding(0, 15, 0, 15)
            };
            _sidebar = sidebar;

            // 侧栏绘制
            sidebar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // 右侧分割线（纯色细线，不用渐变）
                using var divPen = new Pen(CurrentTheme.Border, 1);
                g.DrawLine(divPen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);

                // Logo 区域 — 纯色哑光圆角方块 + 细边框
                var logoRect = new Rectangle(16, 14, 40, 36);
                var logoPath = CreateRoundRectPath(logoRect, 10);
                using var logoBg = new SolidBrush(_isDarkTheme
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(248, 251, 255));
                g.FillPath(logoBg, logoPath);
                using var logoBorder = new Pen(_isDarkTheme
                    ? Color.FromArgb(45, 255, 255, 255)
                    : CurrentTheme.Border, 1);
                g.DrawPath(logoBorder, logoPath);

                // Logo 文字
                using var logoFont = new Font("Segoe UI", 13, FontStyle.Bold);
                using var logoTextBrush = new SolidBrush(CurrentTheme.TextPrimary);
                g.DrawString("G", logoFont, logoTextBrush, 28, 18);
            };

            // 活动指示器
            _navIndicator = new Panel
            {
                Size = new Size(3, 32),
                Location = new Point(0, 70),
                BackColor = AccentBlue
            };
            sidebar.Controls.Add(_navIndicator);

            // 导航项（Unicode图标）
            string[] icons = { "⬡", "📊", "⚡", "🔔", "⚙" };
            string[] tips = { "首页", "数据", "性能", "通知", "设置" };

            for (int i = 0; i < icons.Length; i++)
            {
                int idx = i;
                bool isHovered = false;
                var navBtn = new BufferedPanel
                {
                    Size = new Size(72, 48),
                    Location = new Point(0, 65 + i * 52),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var tt = new ToolTip();
                tt.SetToolTip(navBtn, tips[i]);

                string icon = icons[i];

                navBtn.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    bool active = (idx == _activeNavIndex);
                    // 激活=蓝色  悬停=亮白  默认=柔灰
                    Color color;
                    if (active)
                        color = AccentBlue;
                    else if (isHovered)
                        color = _isDarkTheme ? Color.FromArgb(210, 210, 225) : CurrentTheme.TextPrimary;
                    else
                        color = CurrentTheme.TextSecondary;

                    // 悬停/激活背景
                    if (active)
                    {
                        var bgRect = new Rectangle(8, 6, 56, 36);
                        var bgPath = CreateRoundRectPath(bgRect, 10);
                        using var bgBrush = new SolidBrush(_isDarkTheme
                            ? Color.FromArgb(25, AccentBlue)
                            : Color.FromArgb(42, AccentBlue));
                        g.FillPath(bgBrush, bgPath);
                    }
                    else if (isHovered)
                    {
                        var bgRect = new Rectangle(8, 6, 56, 36);
                        var bgPath = CreateRoundRectPath(bgRect, 10);
                        using var bgBrush = new SolidBrush(_isDarkTheme
                            ? Color.FromArgb(15, 255, 255, 255)
                            : Color.FromArgb(238, 245, 252));
                        g.FillPath(bgBrush, bgPath);
                    }

                    using var font = new Font("Segoe UI Emoji", 16);
                    var textSize = g.MeasureString(icon, font);
                    using var iconBrush = new SolidBrush(color);
                    g.DrawString(icon, font, iconBrush,
                        (72 - textSize.Width) / 2, (48 - textSize.Height) / 2);
                };

                navBtn.Click += (s, e) =>
                {
                    _activeNavIndex = idx;
                    _navIndicator.Location = new Point(0, 70 + idx * 52 + 8);
                    foreach (var item in _navItems)
                        item.Invalidate();
                };

                navBtn.MouseEnter += (s, e) => { isHovered = true; navBtn.Invalidate(); };
                navBtn.MouseLeave += (s, e) => { isHovered = false; navBtn.Invalidate(); };

                _navItems.Add(navBtn);
                sidebar.Controls.Add(navBtn);
            }

            return sidebar;
        }

        // ==================== 顶部标题 ====================
        private Panel CreateHeader()
        {
            var header = new BufferedPanel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 15, 10, 0)
            };

            _themeToggleButton = new Button
            {
                Size = new Size(34, 34),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 1 },
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Symbol", 11F, FontStyle.Regular),
                TabStop = false,
                UseVisualStyleBackColor = false
            };
            _themeToggleButton.Click += (s, e) => ToggleTheme();
            header.Controls.Add(_themeToggleButton);
            header.Resize += (s, e) =>
            {
                _themeToggleButton.Location = new Point(header.Width - 98, 46);
            };

            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                float alpha = Math.Min(1f, _animProgress * 2f);

                // 标题
                using var titleFont = new Font("Segoe UI", 26, FontStyle.Bold);
                int titleA = _isDarkTheme ? 235 : 255;
                using var titleBrush = new SolidBrush(Color.FromArgb((int)(alpha * titleA), CurrentTheme.TextPrimary));
                g.DrawString("仪表板", titleFont, titleBrush, 10, 15);

                // 副标题
                using var subFont = new Font("Segoe UI", 11);
                int subA = _isDarkTheme ? 155 : 220;
                using var subBrush = new SolidBrush(Color.FromArgb((int)(alpha * subA), CurrentTheme.TextSecondary));
                g.DrawString("欢迎回来！这是你的系统概览。", subFont, subBrush, 12, 58);

                // 右上角 - 时间显示
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                var timeSize = g.MeasureString(timeStr, subFont);
                int timeA = _isDarkTheme ? 120 : 180;
                using var timeBrush = new SolidBrush(Color.FromArgb((int)(alpha * timeA), CurrentTheme.TextSecondary));
                g.DrawString(timeStr, subFont, timeBrush, header.Width - timeSize.Width - 112, 20);

                // 右上角 - 用户头像圈（纯色细描边，不用渐变）
                int avatarX = header.Width - 50;
                int avatarY = 48;
                using var avatarPen = new Pen(_isDarkTheme
                    ? Color.FromArgb(60, 255, 255, 255)
                    : CurrentTheme.Border, 1.5f);
                g.DrawEllipse(avatarPen, avatarX, avatarY, 32, 32);
                using var avatarFont = new Font("Segoe UI", 11, FontStyle.Bold);
                using var avatarTextBrush = new SolidBrush(CurrentTheme.TextSecondary);
                g.DrawString("A", avatarFont, avatarTextBrush, avatarX + 9, avatarY + 6);
            };

            UpdateThemeToggleButton();
            return header;
        }

        // ==================== 统计卡片区 ====================
        private Panel CreateCardsArea()
        {
            var container = new BufferedPanel
            {
                Dock = DockStyle.Top,
                Height = 190,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 10)
            };

            // 4 张卡片数据
            var cardData = new[]
            {
                new { Title = "系统运行时间", Value = "128 天", Sub = "▲ 99.8% 正常", Color = AccentBlue, Icon = "◈" },
                new { Title = "活跃连接", Value = "2,847", Sub = "▲ +12.5% 较昨日", Color = AccentGreen, Icon = "◉" },
                new { Title = "CPU 使用率", Value = "67%", Sub = "■ 中等负载", Color = AccentOrange, Icon = "◆" },
                new { Title = "内存占用", Value = "14.2 GB", Sub = "▼ 总计 32 GB", Color = AccentPurple, Icon = "◇" },
            };

            container.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int totalWidth = container.Width - 20;
                int cardWidth = (totalWidth - 45) / 4;
                int cardHeight = 150;
                int y = 15;

                for (int i = 0; i < cardData.Length; i++)
                {
                    float delay = i * 0.15f;
                    float progress = Math.Max(0, Math.Min(1, (_animProgress - delay) * 2.5f));
                    float eased = EaseOutCubic(progress);

                    int x = 10 + i * (cardWidth + 15);
                    int offsetY = (int)((1 - eased) * 30);
                    int alpha = (int)(eased * 255);

                    var rect = new Rectangle(x, y + offsetY, cardWidth, cardHeight);
                    if (_isDarkTheme)
                    {
                        DrawDarkSurfaceShadow(g, rect, 16);
                        
                        var cardPath = CreateRoundRectPath(rect, 16);
                        using var cardBrush = new SolidBrush(Color.FromArgb(Math.Min(255, (int)(eased * 255)), CurrentTheme.Card));
                        g.FillPath(cardBrush, cardPath);

                        // 边框（微弱发光）
                        using var borderPen = new Pen(Color.FromArgb(Math.Min(80, alpha / 3), 255, 255, 255), 1.2f);
                        g.DrawPath(borderPen, cardPath);
                    }
                    else
                    {
                        DrawSunken3DPanel(g, rect, 16, alpha);
                    }

                    if (alpha < 10) continue;

                    // 图标背景圈
                    int iconBgSize = 40;
                    var iconBgRect = new Rectangle(rect.X + 20, rect.Y + 22, iconBgSize, iconBgSize);
                    using var iconBgBrush = new SolidBrush(_isDarkTheme
                        ? Color.FromArgb(Math.Min(50, alpha / 5), cardData[i].Color)
                        : Color.FromArgb(35, cardData[i].Color));
                    var iconBgPath = CreateRoundRectPath(iconBgRect, 12);
                    g.FillPath(iconBgBrush, iconBgPath);

                    // 图标
                    using var iconFont = new Font("Segoe UI", 16);
                    using var iconBrush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), cardData[i].Color));
                    g.DrawString(cardData[i].Icon, iconFont, iconBrush, rect.X + 28, rect.Y + 30);

                    // 数值
                    using var valueFont = new Font("Segoe UI", 22, FontStyle.Bold);
                    using var valueBrush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), CurrentTheme.TextPrimary));
                    g.DrawString(cardData[i].Value, valueFont, valueBrush, rect.X + 20, rect.Y + 72);

                    // 标题
                    using var labelFont = new Font("Segoe UI", 9);
                    using var labelBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(230, alpha) : Math.Min(255, alpha), CurrentTheme.TextSecondary));
                    g.DrawString(cardData[i].Title, labelFont, labelBrush, rect.X + 20, rect.Y + 108);

                    // 子文本
                    using var subFont = new Font("Segoe UI", 8);
                    using var subBrush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), cardData[i].Color));
                    g.DrawString(cardData[i].Sub, subFont, subBrush, rect.X + 20, rect.Y + 126);
                }
            };

            return container;
        }

        // ==================== 底部内容区 ====================
        private Panel CreateBottomArea()
        {
            var bottom = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 10)
            };

            bottom.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                float delay = 0.4f;
                float progress = Math.Max(0, Math.Min(1, (_animProgress - delay) * 2f));
                float eased = EaseOutCubic(progress);
                int alpha = (int)(eased * 255);

                int totalW = bottom.Width - 20;
                int leftW = (int)(totalW * 0.62);
                int rightW = totalW - leftW - 15;
                int panelH = bottom.Height - 30;

                // ===== 左侧：活动日志面板 =====
                var leftRect = new Rectangle(10, 10, leftW, panelH);
                if (_isDarkTheme)
                {
                    var leftPath = CreateRoundRectPath(leftRect, 16);
                    DrawDarkSurfaceShadow(g, leftRect, 16);
                    using var bgBrush = new SolidBrush(Color.FromArgb(Math.Min(255, (int)(eased * 255)), CurrentTheme.Card));
                    g.FillPath(bgBrush, leftPath);
                    using var borderPen = new Pen(Color.FromArgb(Math.Min(80, alpha / 3), 255, 255, 255), 1.2f);
                    g.DrawPath(borderPen, leftPath);
                }
                else
                {
                    DrawSunken3DPanel(g, leftRect, 16, alpha);
                }

                if (alpha > 10)
                {
                    // 面板标题
                    using var panelTitleFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    using var panelTitleBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(235, alpha) : Math.Min(255, alpha), CurrentTheme.TextPrimary));
                    g.DrawString("最近活动", panelTitleFont, panelTitleBrush, leftRect.X + 24, leftRect.Y + 18);

                    // 分割线
                    using var dividerPen = new Pen(_isDarkTheme
                        ? Color.FromArgb(Math.Min(40, alpha / 6), 255, 255, 255)
                        : CurrentTheme.Border, 1);
                    g.DrawLine(dividerPen, leftRect.X + 24, leftRect.Y + 52, leftRect.Right - 24, leftRect.Y + 52);

                    // 日志条目
                    var logs = new[]
                    {
                        new { Time = "14:32", Text = "系统健康检查完成", Status = "✓", SColor = AccentGreen },
                        new { Time = "13:18", Text = "数据库备份已创建", Status = "✓", SColor = AccentGreen },
                        new { Time = "12:05", Text = "检测到 CPU 峰值 (89%)", Status = "⚠", SColor = AccentOrange },
                        new { Time = "11:42", Text = "新用户注册 +23", Status = "●", SColor = AccentBlue },
                        new { Time = "10:30", Text = "SSL 证书即将到期", Status = "⚠", SColor = AccentPink },
                        new { Time = "09:15", Text = "服务器重启完成", Status = "✓", SColor = AccentGreen },
                    };

                    int logY = leftRect.Y + 64;
                    int logH = Math.Min(42, (panelH - 80) / logs.Length);

                    for (int i = 0; i < logs.Length && logY + logH < leftRect.Bottom - 10; i++)
                    {
                        float logDelay = delay + 0.08f * i;
                        float logProgress = Math.Max(0, Math.Min(1, (_animProgress - logDelay) * 3f));
                        float logEased = EaseOutCubic(logProgress);
                        int logAlpha = (int)(logEased * 255);

                        if (logAlpha < 5) continue;

                        // 行背景（交替）
                        if (i % 2 == 0)
                        {
                            var rowRect = new Rectangle(leftRect.X + 12, logY, leftW - 24, logH);
                            var rowPath = CreateRoundRectPath(rowRect, 8);
                            using var rowBrush = new SolidBrush(_isDarkTheme
                                ? Color.FromArgb(Math.Min(15, logAlpha / 16), 255, 255, 255)
                                : Color.FromArgb(240, 244, 250));
                            g.FillPath(rowBrush, rowPath);
                        }

                        // 状态图标
                        using var statusFont = new Font("Segoe UI", 10);
                        using var statusBrush = new SolidBrush(Color.FromArgb(Math.Min(255, logAlpha), logs[i].SColor));
                        g.DrawString(logs[i].Status, statusFont, statusBrush, leftRect.X + 24, logY + (logH - 16) / 2);

                        // 日志文本
                        using var logFont = new Font("Segoe UI", 10);
                        using var logBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(210, logAlpha) : Math.Min(255, logAlpha), CurrentTheme.TextPrimary));
                        g.DrawString(logs[i].Text, logFont, logBrush, leftRect.X + 52, logY + (logH - 16) / 2);

                        // 时间
                        using var timeFont = new Font("Segoe UI", 9);
                        string timeStr = logs[i].Time;
                        var timeSize = g.MeasureString(timeStr, timeFont);
                        using var timeBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(120, logAlpha) : Math.Min(190, logAlpha), CurrentTheme.TextMuted));
                        g.DrawString(timeStr, timeFont, timeBrush, leftRect.Right - timeSize.Width - 24, logY + (logH - 14) / 2);

                        logY += logH;
                    }
                }

                // ===== 右侧：快捷操作面板 =====
                var rightRect = new Rectangle(10 + leftW + 15, 10, rightW, panelH);
                if (_isDarkTheme)
                {
                    var rightPath = CreateRoundRectPath(rightRect, 16);
                    DrawDarkSurfaceShadow(g, rightRect, 16);
                    using var bgBrush = new SolidBrush(Color.FromArgb(Math.Min(255, (int)(eased * 255)), CurrentTheme.Card));
                    g.FillPath(bgBrush, rightPath);
                    using var borderPen = new Pen(Color.FromArgb(Math.Min(80, alpha / 3), 255, 255, 255), 1.2f);
                    g.DrawPath(borderPen, rightPath);
                }
                else
                {
                    DrawSunken3DPanel(g, rightRect, 16, alpha);
                }

                if (alpha > 10)
                {
                    using var rtFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    using var rtBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(235, alpha) : Math.Min(255, alpha), CurrentTheme.TextPrimary));
                    g.DrawString("快捷操作", rtFont, rtBrush, rightRect.X + 24, rightRect.Y + 18);

                    using var divPen = new Pen(_isDarkTheme
                        ? Color.FromArgb(Math.Min(40, alpha / 6), 255, 255, 255)
                        : CurrentTheme.Border, 1);
                    g.DrawLine(divPen, rightRect.X + 24, rightRect.Y + 52, rightRect.Right - 24, rightRect.Y + 52);

                    // 操作按钮
                    var actions = new[]
                    {
                        new { Text = "系统诊断", Icon = "🔍", Color1 = AccentBlue, Color2 = AccentCyan },
                        new { Text = "性能优化", Icon = "🚀", Color1 = AccentPurple, Color2 = AccentPink },
                        new { Text = "数据备份", Icon = "💾", Color1 = AccentGreen, Color2 = AccentCyan },
                        new { Text = "安全扫描", Icon = "🛡️", Color1 = AccentOrange, Color2 = AccentPink },
                    };

                    int btnY = rightRect.Y + 66;
                    int btnH = Math.Min(52, (panelH - 90) / actions.Length);
                    int btnW = rightW - 48;

                    for (int i = 0; i < actions.Length && btnY + btnH < rightRect.Bottom - 10; i++)
                    {
                        float btnDelay = delay + 0.1f + 0.1f * i;
                        float btnProgress = Math.Max(0, Math.Min(1, (_animProgress - btnDelay) * 2.5f));
                        float btnEased = EaseOutCubic(btnProgress);
                        int btnAlpha = (int)(btnEased * 255);

                        if (btnAlpha < 5) continue;

                        var btnRect = new Rectangle(rightRect.X + 24, btnY, btnW, btnH - 8);
                        var btnPath = CreateRoundRectPath(btnRect, 12);

                        // 渐变按钮背景
                        if (_isDarkTheme)
                        {
                            using var btnGradient = new LinearGradientBrush(btnRect,
                                Color.FromArgb(Math.Min(35, btnAlpha / 7), actions[i].Color1),
                                Color.FromArgb(Math.Min(15, btnAlpha / 16), actions[i].Color2), 0f);
                            g.FillPath(btnGradient, btnPath);
                        }
                        else
                        {
                            using var btnBg = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
                            g.FillPath(btnBg, btnPath);
                            using var btnTint = new SolidBrush(Color.FromArgb(50, actions[i].Color1));
                            g.FillPath(btnTint, btnPath);
                        }

                        // 边框
                        using var btnBorderPen = new Pen(_isDarkTheme
                            ? Color.FromArgb(Math.Min(50, btnAlpha / 5), actions[i].Color1)
                            : Color.FromArgb(Math.Min(220, btnAlpha), actions[i].Color1), 1.2f);
                        g.DrawPath(btnBorderPen, btnPath);

                        // 图标
                        using var btnIconFont = new Font("Segoe UI Emoji", 14);
                        using var btnIconBrush = new SolidBrush(Color.FromArgb(Math.Min(255, btnAlpha), actions[i].Color1));
                        g.DrawString(actions[i].Icon, btnIconFont, btnIconBrush, btnRect.X + 14, btnRect.Y + (btnRect.Height - 22) / 2);

                        // 文字
                        using var btnTextFont = new Font("Segoe UI", 11, FontStyle.Bold);
                        using var btnTextBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(220, btnAlpha) : Math.Min(255, btnAlpha), CurrentTheme.TextPrimary));
                        g.DrawString(actions[i].Text, btnTextFont, btnTextBrush, btnRect.X + 48, btnRect.Y + (btnRect.Height - 18) / 2);

                        // 箭头
                        using var arrowFont = new Font("Segoe UI", 11);
                        using var arrowBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(100, btnAlpha) : Math.Min(180, btnAlpha), CurrentTheme.TextMuted));
                        g.DrawString("→", arrowFont, arrowBrush, btnRect.Right - 30, btnRect.Y + (btnRect.Height - 18) / 2);

                        btnY += btnH;
                    }
                }
            };

            return bottom;
        }

        // ==================== 动画回调 ====================
        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_animProgress < 1.0f)
            {
                _animProgress += 0.018f;
                this.Invalidate(true);

                // 通知所有子面板重绘
            }
            else
            {
                _animProgress = 1.0f;
                _animTimer.Stop();
                this.Invalidate(true);
            }
        }

        // ==================== 工具方法 ====================
        private static GraphicsPath CreateRoundRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static uint ToAbgr(Color color)
        {
            return (uint)(color.A << 24 | color.B << 16 | color.G << 8 | color.R);
        }

        private static void DrawLightSurfaceShadow(Graphics g, Rectangle rect, int radius)
        {
            var shadowRect = rect;
            shadowRect.Offset(0, 8);
            shadowRect.Inflate(2, 3);
            using var shadowPath = CreateRoundRectPath(shadowRect, radius + 2);
            using var shadowBrush = new SolidBrush(Color.FromArgb(45, 118, 137, 163));
            g.FillPath(shadowBrush, shadowPath);

            var ambientRect = rect;
            ambientRect.Offset(0, 3);
            ambientRect.Inflate(1, 1);
            using var ambientPath = CreateRoundRectPath(ambientRect, radius + 1);
            using var ambientBrush = new SolidBrush(Color.FromArgb(12, 255, 255, 255));
            g.FillPath(ambientBrush, ambientPath);
        }

        private static void DrawDarkSurfaceShadow(Graphics g, Rectangle rect, int radius)
        {
            var shadowRect = rect;
            shadowRect.Offset(0, 8);
            shadowRect.Inflate(2, 4);
            using var shadowPath = CreateRoundRectPath(shadowRect, radius + 2);
            using var shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        private static void DrawSunken3DPanel(Graphics g, Rectangle rect, int radius, int parentAlpha)
        {
            float maxA = parentAlpha / 255f;
            var cardPath = CreateRoundRectPath(rect, radius);

            using var baseBrush = new SolidBrush(Color.FromArgb(parentAlpha, 222, 228, 236));
            g.FillPath(baseBrush, cardPath);

            g.SetClip(cardPath);

            // Top-Left Soft Inner Shadow
            for (int i = 1; i <= 8; i++)
            {
                var shadowRect = rect;
                shadowRect.Width += 40;
                shadowRect.Height += 40;
                shadowRect.Offset(-i, -i); 
                using var innerPath = CreateRoundRectPath(shadowRect, radius);
                
                int alpha = (int)(Math.Max(0, 50 - i * 6) * maxA);
                if (alpha > 0)
                {
                    using var innerPen = new Pen(Color.FromArgb(alpha, 50, 70, 100), i);
                    g.DrawPath(innerPen, innerPath);
                }
            }

            // Bottom-Right Soft Inner Highlight
            for (int i = 1; i <= 6; i++)
            {
                var hlRect = rect;
                hlRect.X -= 40;
                hlRect.Y -= 40;
                hlRect.Width += 40;
                hlRect.Height += 40;
                hlRect.Offset(i, i); 
                using var innerPath = CreateRoundRectPath(hlRect, radius);
                
                int alpha = (int)(Math.Max(0, 100 - i * 15) * maxA);
                if (alpha > 0)
                {
                    using var innerPen = new Pen(Color.FromArgb(alpha, 255, 255, 255), i);
                    g.DrawPath(innerPen, innerPath);
                }
            }
            g.ResetClip();

            // Outer Lip (Simulates edge catching light/shadow)
            using var rimBrush = new LinearGradientBrush(rect,
                Color.FromArgb((int)(255 * maxA), 255, 255, 255),
                Color.FromArgb((int)(255 * maxA), 160, 175, 195),
                45f);
            using var rimPen = new Pen(rimBrush, 1.2f);
            g.DrawPath(rimPen, cardPath);
        }

        private static void InvalidateControlTree(Control root)
        {
            root.Invalidate(true);
            foreach (Control child in root.Controls)
                InvalidateControlTree(child);
        }

        private static float EaseOutCubic(float t)
        {
            return 1 - (float)Math.Pow(1 - t, 3);
        }

        // 双缓冲减少闪烁
    }
}
