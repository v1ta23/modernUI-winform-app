using WinFormsApp.Controllers;
using WinFormsApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinFormsApp.Views
{
    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
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
    }

    internal partial class MainForm : Form
    {
        private const int HomeSectionIndex = 0;
        private const int MonitorSectionIndex = 1;
        private const int AlarmSectionIndex = 2;
        private const int InspectionSectionIndex = 3;
        private const int AnalyticsSectionIndex = 4;
        private const int DataInsightSectionIndex = 5;
        private const int WmEnterSizeMove = 0x0231;
        private const int WmSizing = 0x0214;
        private const int WmExitSizeMove = 0x0232;

        private readonly DashboardController _dashboardController;
        private DashboardViewModel _dashboard;
        private readonly DeviceMonitorPageControl _monitorPage;
        private readonly AlarmCenterPageControl _alarmPage;
        private readonly InspectionPageControl _inspectionPage;
        private readonly InspectionAnalyticsControl _analyticsPage;
        private readonly DataInsightPageControl _dataInsightPage;
        private readonly string _account;
        private readonly ContextMenuStrip _accountMenu;

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

        private const string SidebarIconFontFamily = "Segoe MDL2 Assets";

        private enum SidebarGlyph
        {
            Home,
            Devices,
            Warning,
            Page,
            Chart,
            Import,
            Notification,
            Setting,
            User
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
        private static readonly Color BgSidebar = Color.FromArgb(22, 24, 33);
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
        private static readonly Font[] HeaderTitleFonts =
        {
            new Font("Microsoft YaHei UI", 24F, FontStyle.Bold),
            new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
            new Font("Microsoft YaHei UI", 20F, FontStyle.Bold)
        };
        private static readonly Font[] HeaderSubtitleFonts =
        {
            new Font("Microsoft YaHei UI", 11F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
        };
        private static readonly Font[] CardValueFonts =
        {
            new Font("Segoe UI", 22F, FontStyle.Bold),
            new Font("Segoe UI", 20F, FontStyle.Bold),
            new Font("Segoe UI", 18F, FontStyle.Bold)
        };
        private static readonly Font[] CardLabelFonts =
        {
            new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular)
        };
        private static readonly Font[] CardDetailFonts =
        {
            new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 8.2F, FontStyle.Regular),
            new Font("Microsoft YaHei UI", 8F, FontStyle.Regular)
        };
        private static readonly Font[] ActivityFonts =
        {
            new Font("Segoe UI", 10F, FontStyle.Regular),
            new Font("Segoe UI", 9.5F, FontStyle.Regular),
            new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        private static readonly Font[] QuickActionFonts =
        {
            new Font("Segoe UI", 11F, FontStyle.Bold),
            new Font("Segoe UI", 10.5F, FontStyle.Bold),
            new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        private static readonly Font PanelTitleFont = new("Segoe UI", 14F, FontStyle.Bold);
        private static readonly Font StatusBadgeFont = new("Segoe UI", 8.5F, FontStyle.Bold);
        private static readonly Font ActivityTimeFont = new("Segoe UI", 9F, FontStyle.Regular);
        private static readonly Font ArrowFont = new("Segoe UI", 11F, FontStyle.Regular);
        private static readonly StringFormat CenteredSingleLineTextFormat = CreateSingleLineTextFormat(StringAlignment.Center);
        private static readonly StringFormat TopAlignedSingleLineTextFormat = CreateSingleLineTextFormat(StringAlignment.Near);
        private static readonly StringFormat WrappedTextFormat = CreateWrappedTextFormat();

        // ==================== 动画相关 ====================
        private System.Windows.Forms.Timer _animTimer = null!;
        private float _animProgress = 0f;
        private int _activeNavIndex = 0;
        private readonly List<Panel> _navItems = new List<Panel>();
        private bool _isDarkTheme = true;
        private bool _isInteractiveResize;
        private IInteractiveResizeAware? _activeInteractiveResizePage;
        private Panel _navIndicator = null!;
        private Panel _sidebar = null!;
        private Panel _mainArea = null!;
        private Panel _homeView = null!;
        private Panel _homeHeader = null!;
        private Panel _homeCardsArea = null!;
        private Panel _homeBottomArea = null!;
        private BufferedPanel _avatarButton = null!;
        private BufferedPanel _themeToggleButton = null!;
        private readonly List<DashboardHitRegion> _cardHitRegions = new();
        private readonly List<DashboardHitRegion> _quickActionHitRegions = new();

        private readonly struct DashboardHitRegion
        {
            public DashboardHitRegion(Rectangle bounds, DashboardNavigationTarget target)
            {
                Bounds = bounds;
                Target = target;
            }

            public Rectangle Bounds { get; }

            public DashboardNavigationTarget Target { get; }
        }

        private sealed class AccountMenuColorTable : ProfessionalColorTable
        {
            private readonly MainForm _owner;

            public AccountMenuColorTable(MainForm owner)
            {
                _owner = owner;
            }

            public override Color ToolStripDropDownBackground => _owner.CurrentTheme.Card;

            public override Color MenuBorder => Color.Transparent;

            public override Color MenuItemBorder => Color.Transparent;

            public override Color MenuItemSelected => Color.Transparent;

            public override Color MenuItemSelectedGradientBegin => Color.Transparent;

            public override Color MenuItemSelectedGradientEnd => Color.Transparent;

            public override Color MenuItemPressedGradientBegin => Color.Transparent;

            public override Color MenuItemPressedGradientMiddle => Color.Transparent;

            public override Color MenuItemPressedGradientEnd => Color.Transparent;

            public override Color ImageMarginGradientBegin => Color.Transparent;

            public override Color ImageMarginGradientMiddle => Color.Transparent;

            public override Color ImageMarginGradientEnd => Color.Transparent;

            public override Color SeparatorDark => Color.FromArgb(48, 255, 255, 255);

            public override Color SeparatorLight => Color.Transparent;
        }

        private sealed class AccountMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly MainForm _owner;

            public AccountMenuRenderer(MainForm owner)
                : base(new AccountMenuColorTable(owner))
            {
                _owner = owner;
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                using var path = MainForm.CreateRoundRectPath(rect, 18);
                using var fillBrush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(244, 31, 35, 47),
                    Color.FromArgb(244, 24, 27, 38),
                    90f);
                g.FillPath(fillBrush, path);

                var glowRect = new Rectangle(14, -18, Math.Max(60, rect.Width - 28), 78);
                using var glowBrush = new LinearGradientBrush(
                    glowRect,
                    Color.FromArgb(34, AccentBlue),
                    Color.FromArgb(0, AccentBlue),
                    90f);
                g.FillRectangle(glowBrush, glowRect);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                using var path = MainForm.CreateRoundRectPath(rect, 18);
                using var borderPen = new Pen(Color.FromArgb(82, 106, 124, 158), 1f);
                g.DrawPath(borderPen, path);

                var innerRect = Rectangle.Inflate(rect, -1, -1);
                using var innerPath = MainForm.CreateRoundRectPath(innerRect, 17);
                using var innerPen = new Pen(Color.FromArgb(24, 255, 255, 255), 1f);
                g.DrawPath(innerPen, innerPath);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item is not ToolStripMenuItem || !e.Item.Selected)
                {
                    return;
                }

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(8, 2, e.Item.Width - 16, e.Item.Height - 4);
                using var path = MainForm.CreateRoundRectPath(rect, 12);
                using var fillBrush = new SolidBrush(Color.FromArgb(26, AccentBlue));
                using var tintBrush = new SolidBrush(Color.FromArgb(12, 255, 255, 255));
                using var borderPen = new Pen(Color.FromArgb(72, AccentBlue), 1f);
                g.FillPath(fillBrush, path);
                g.FillPath(tintBrush, path);
                g.DrawPath(borderPen, path);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var color = e.Item.ForeColor;
                if (e.Item is ToolStripMenuItem)
                {
                    color = e.Item.Selected
                        ? Color.FromArgb(244, 247, 255)
                        : _owner.CurrentTheme.TextPrimary;
                }

                var textRect = e.TextRectangle;
                if (e.Item is ToolStripMenuItem)
                {
                    textRect.Offset(6, 0);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    e.Text,
                    e.TextFont,
                    textRect,
                    color,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                var y = e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2);
                using var pen = new Pen(Color.FromArgb(48, 255, 255, 255), 1f);
                e.Graphics.DrawLine(pen, 18, y, e.Item.Width - 18, y);
            }
        }

        public event EventHandler? SwitchAccountRequested;

        public event EventHandler? LogoutRequested;

        public MainForm(
            DashboardController dashboardController,
            InspectionController inspectionController,
            string account)
        {
            _dashboardController = dashboardController;
            _dashboard = dashboardController.Load(account);
            _account = account;
            _accountMenu = CreateAccountMenu();
            _monitorPage = new DeviceMonitorPageControl(inspectionController)
            {
                Visible = false
            };
            _alarmPage = new AlarmCenterPageControl(account, inspectionController)
            {
                Visible = false
            };
            _alarmPage.DataChanged += OnInspectionDataChanged;
            _inspectionPage = new InspectionPageControl(account, inspectionController)
            {
                Visible = false
            };
            _inspectionPage.DataChanged += OnInspectionDataChanged;
            _analyticsPage = new InspectionAnalyticsControl(inspectionController)
            {
                Visible = false
            };
            _dataInsightPage = new DataInsightPageControl(inspectionController, account)
            {
                Visible = false
            };
            _dataInsightPage.DataChanged += OnInspectionDataChanged;
            _dataInsightPage.ViewImportedRequested += OnDataInsightViewImportedRequested;
            _dataInsightPage.ViewPendingRequested += OnDataInsightViewPendingRequested;

            // 基础设置
            this.Text = "Glass Dashboard · 毛玻璃仪表板";
            this.Text = string.Empty;
            this.Size = new Size(1440, 900);
            this.MinimumSize = new Size(1180, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = CurrentTheme.Background;
            this.DoubleBuffered = true;
            this.Font = new Font("Microsoft YaHei UI", 9F);
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

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmEnterSizeMove || m.Msg == WmSizing)
            {
                BeginInteractiveResize();
            }

            base.WndProc(ref m);

            if (m.Msg == WmExitSizeMove)
            {
                EndInteractiveResize();
            }
        }

        // ==================== 启用深色标题栏 (Win11优先) ====================
        private ThemePalette CurrentTheme => DarkTheme;

        private Color MainAreaBackground => CurrentTheme.Background;

        private void SetDarkTitleBar()
        {
            try
            {
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
                int darkMode = 1;
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

        private void DisableAcrylicBlur()
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = 0,
                    AccentFlags = 2,
                    GradientColor = 0
                };

                int accentSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19,
                    Data = accentPtr,
                    SizeOfData = accentSize
                };

                SetWindowCompositionAttribute(this.Handle, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { }
        }

        private void BeginInteractiveResize()
        {
            if (_isInteractiveResize)
            {
                return;
            }

            _isInteractiveResize = true;
            if (_animTimer.Enabled)
            {
                _animTimer.Stop();
            }

            _activeInteractiveResizePage = GetVisibleInteractiveResizeAware();
            _activeInteractiveResizePage?.BeginInteractiveResize();
        }

        private void EndInteractiveResize()
        {
            if (!_isInteractiveResize)
            {
                return;
            }

            _isInteractiveResize = false;
            _activeInteractiveResizePage?.EndInteractiveResize();
            _activeInteractiveResizePage = null;

            InvalidateHomeSections();
            Invalidate(true);
        }

        private IInteractiveResizeAware? GetVisibleInteractiveResizeAware()
        {
            if (_inspectionPage.Visible)
            {
                return _inspectionPage;
            }

            if (_monitorPage.Visible)
            {
                return _monitorPage;
            }

            if (_alarmPage.Visible)
            {
                return _alarmPage;
            }

            if (_analyticsPage.Visible)
            {
                return _analyticsPage;
            }

            if (_dataInsightPage.Visible)
            {
                return _dataInsightPage;
            }

            return null;
        }

        private void SetResizeRedrawEnabled(bool enabled)
        {
            SetStyle(ControlStyles.ResizeRedraw, enabled);
            UpdateStyles();
        }

        // ==================== 构建主布局 ====================
        private void BuildLayout()
        {
            SuspendLayout();

            var mainArea = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = MainAreaBackground,
                Padding = new Padding(30, 20, 30, 20)
            };
            _mainArea = mainArea;

            mainArea.SuspendLayout();
            var homeView = BuildHomeView();
            _homeView = homeView;

            _monitorPage.Dock = DockStyle.Fill;
            _alarmPage.Dock = DockStyle.Fill;
            _inspectionPage.Dock = DockStyle.Fill;
            _analyticsPage.Dock = DockStyle.Fill;
            _dataInsightPage.Dock = DockStyle.Fill;
            mainArea.Controls.Add(_monitorPage);
            mainArea.Controls.Add(_alarmPage);
            mainArea.Controls.Add(_inspectionPage);
            mainArea.Controls.Add(_analyticsPage);
            mainArea.Controls.Add(_dataInsightPage);
            mainArea.Controls.Add(homeView);

            this.Controls.Add(mainArea);

            var sidebar = CreateSidebar();
            this.Controls.Add(sidebar);
            mainArea.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel BuildHomeView()
        {
            var homeView = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            var bottomArea = CreateBottomArea();
            var cardsArea = CreateCardsArea();
            var header = CreateHeader();

            homeView.Controls.Add(bottomArea);
            homeView.Controls.Add(cardsArea);
            homeView.Controls.Add(header);

            _homeHeader = header;
            _homeCardsArea = cardsArea;
            _homeBottomArea = bottomArea;
            return homeView;
        }

        private Control BuildHomeMetricsArea()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                ColumnCount = Math.Max(1, _dashboard.Cards.Count),
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 12),
                Height = 168
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var cardCount = Math.Max(1, _dashboard.Cards.Count);
            for (var index = 0; index < cardCount; index++)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / cardCount));
            }

            if (_dashboard.Cards.Count == 0)
            {
                var emptyPanel = PageChrome.CreateSurfacePanel(new Padding(20));
                emptyPanel.Margin = Padding.Empty;
                emptyPanel.Controls.Add(PageChrome.CreateEmptyStateLabel("当前没有统计卡片"));
                layout.Controls.Add(emptyPanel, 0, 0);
                return layout;
            }

            for (var index = 0; index < _dashboard.Cards.Count; index++)
            {
                layout.Controls.Add(CreateDashboardMetricCard(_dashboard.Cards[index], index == _dashboard.Cards.Count - 1), index, 0);
            }

            return layout;
        }

        private Control BuildHomeBottomArea()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var activityList = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoScroll = true
            };
            activityList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            if (_dashboard.Activities.Count == 0)
            {
                activityList.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                activityList.Controls.Add(PageChrome.CreateEmptyStateLabel("当前还没有最近活动。"), 0, 0);
            }
            else
            {
                for (var index = 0; index < _dashboard.Activities.Count; index++)
                {
                    activityList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    activityList.Controls.Add(CreateActivityRow(_dashboard.Activities[index], index == _dashboard.Activities.Count - 1), 0, index);
                }
            }

            var actionList = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = Math.Max(1, _dashboard.QuickActions.Count),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            actionList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            if (_dashboard.QuickActions.Count == 0)
            {
                actionList.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                actionList.Controls.Add(PageChrome.CreateEmptyStateLabel("当前没有快捷操作。"), 0, 0);
            }
            else
            {
                for (var index = 0; index < _dashboard.QuickActions.Count; index++)
                {
                    actionList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    actionList.Controls.Add(CreateQuickActionRow(_dashboard.QuickActions[index], index == _dashboard.QuickActions.Count - 1), 0, index);
                }
            }

            layout.Controls.Add(PageChrome.CreateSectionShell("最近活动", "按时间倒序，保留最新的巡检动态。", out _, activityList, new Padding(0, 0, 12, 0)), 0, 0);
            layout.Controls.Add(PageChrome.CreateSectionShell("快捷操作", "直接跳到最常用的处理动作。", out _, actionList), 1, 0);
            return layout;
        }

        private Control CreateDashboardMetricCard(DashboardCardViewModel cardData, bool isLast)
        {
            var card = PageChrome.CreateSurfacePanel(new Padding(18, 16, 18, 18));
            card.Margin = isLast ? Padding.Empty : new Padding(0, 0, 12, 0);
            card.MinimumSize = new Size(0, 156);
            card.Paint += (_, e) =>
            {
                using var accentPen = new Pen(cardData.AccentColor, 2F);
                e.Graphics.DrawLine(accentPen, 18, 12, 64, 12);
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var badge = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(36, cardData.AccentColor),
                ForeColor = cardData.AccentColor,
                Font = new Font("Microsoft YaHei UI", 8.6F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(10, 4, 10, 4),
                Text = cardData.Icon
            };

            var valueLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = PageChrome.TextPrimary,
                Height = 40,
                Margin = new Padding(0, 0, 0, 4),
                Text = cardData.Value
            };

            var titleLabel = PageChrome.CreateTextLabel(cardData.Title, 9F, FontStyle.Bold, PageChrome.TextMuted, new Padding(0, 0, 0, 6));
            var detailLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 8.8F),
                ForeColor = cardData.AccentColor,
                Margin = Padding.Empty,
                Text = cardData.Detail
            };

            content.Controls.Add(badge, 0, 0);
            content.Controls.Add(valueLabel, 0, 1);
            content.Controls.Add(titleLabel, 0, 2);
            content.Controls.Add(detailLabel, 0, 3);
            card.Controls.Add(content);
            BindDashboardNavigation(card, cardData.NavigationTarget);
            return card;
        }

        private Control CreateActivityRow(DashboardActivityViewModel activity, bool isLast)
        {
            var row = PageChrome.CreateSurfacePanel(
                new Padding(14, 12, 14, 12),
                radius: 12,
                fillColor: PageChrome.SurfaceRaised,
                borderColor: Color.FromArgb(62, PageChrome.SurfaceBorder));
            row.Margin = isLast ? Padding.Empty : new Padding(0, 0, 0, 10);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var statusLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(34, activity.AccentColor),
                ForeColor = activity.AccentColor,
                Font = new Font("Microsoft YaHei UI", 8.4F, FontStyle.Bold),
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(10, 4, 10, 4),
                Text = activity.Status
            };

            var textLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9.2F),
                ForeColor = PageChrome.TextPrimary,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = activity.Text
            };

            var timeLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Right,
                Font = new Font("Microsoft YaHei UI", 8.8F),
                ForeColor = PageChrome.TextMuted,
                Margin = new Padding(12, 0, 0, 0),
                Text = activity.Time
            };

            layout.Controls.Add(statusLabel, 0, 0);
            layout.Controls.Add(textLabel, 1, 0);
            layout.Controls.Add(timeLabel, 2, 0);
            row.Controls.Add(layout);
            return row;
        }

        private Control CreateQuickActionRow(DashboardQuickActionViewModel action, bool isLast)
        {
            var button = PageChrome.CreateActionButton($"{action.Icon}  {action.Text}", action.PrimaryAccent, false);
            button.Dock = DockStyle.Top;
            button.AutoSize = false;
            button.Height = 46;
            button.Margin = isLast ? Padding.Empty : new Padding(0, 0, 0, 10);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(14, 8, 14, 8);
            if (action.NavigationTarget != DashboardNavigationTarget.None)
            {
                button.Click += (_, _) => NavigateTo(action.NavigationTarget);
            }
            else
            {
                button.Enabled = false;
            }

            return button;
        }

        private void BindDashboardNavigation(Control control, DashboardNavigationTarget target)
        {
            if (target == DashboardNavigationTarget.None)
            {
                return;
            }

            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => NavigateTo(target);
            foreach (Control child in control.Controls)
            {
                BindDashboardNavigation(child, target);
            }
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
            
            _monitorPage?.ApplyTheme();
            _alarmPage?.ApplyTheme();
            _inspectionPage?.ApplyTheme();
            _analyticsPage?.ApplyTheme();
            _dataInsightPage?.ApplyTheme();
            InvalidateControlTree(this);
        }

        private void ReloadDashboard()
        {
            _dashboard = _dashboardController.Load(_account);
            if (_mainArea is null)
            {
                return;
            }

            var wasVisible = _homeView?.Visible ?? true;
            var childIndex = _homeView is not null
                ? _mainArea.Controls.GetChildIndex(_homeView)
                : 0;

            _mainArea.SuspendLayout();
            if (_homeView is not null)
            {
                _mainArea.Controls.Remove(_homeView);
                _homeView.Dispose();
            }

            _homeView = BuildHomeView();
            _homeView.Visible = wasVisible;
            _mainArea.Controls.Add(_homeView);
            _mainArea.Controls.SetChildIndex(_homeView, childIndex);
            _mainArea.ResumeLayout();
        }

        private void OnInspectionDataChanged(object? sender, EventArgs e)
        {
            ReloadDashboard();
            if (_inspectionPage.Visible)
            {
                _inspectionPage.RefreshData();
            }

            if (_monitorPage.Visible)
            {
                _monitorPage.RefreshData();
            }

            if (_alarmPage.Visible)
            {
                _alarmPage.RefreshData();
            }

            if (_analyticsPage.Visible)
            {
                _analyticsPage.RefreshData();
            }
        }

        private void OnDataInsightViewImportedRequested(object? sender, EventArgs e)
        {
            OpenImportedBatchReview(false);
        }

        private void OnDataInsightViewPendingRequested(object? sender, EventArgs e)
        {
            OpenImportedBatchReview(true);
        }

        private void OpenImportedBatchReview(bool pendingOnly)
        {
            var batchKeyword = _dataInsightPage.LastImportedBatchKeyword;
            if (string.IsNullOrWhiteSpace(batchKeyword))
            {
                return;
            }

            UpdateNavigationSelection(InspectionSectionIndex);
            _inspectionPage.ShowImportedBatch(batchKeyword, pendingOnly);
            SwitchSection(InspectionSectionIndex, refreshInspectionPage: false);
        }

        private void SwitchSection(
            int index,
            bool refreshInspectionPage = true,
            bool refreshAnalyticsPage = true,
            bool refreshMonitorPage = true,
            bool refreshAlarmPage = true)
        {
            var showHome = index == HomeSectionIndex;
            var showMonitor = index == MonitorSectionIndex;
            var showAlarm = index == AlarmSectionIndex;
            var showInspection = index == InspectionSectionIndex;
            var showAnalytics = index == AnalyticsSectionIndex;
            var showDataInsight = index == DataInsightSectionIndex;
            var previousResizeAware = GetVisibleInteractiveResizeAware();

            previousResizeAware?.EndInteractiveResize();
            if (ReferenceEquals(_activeInteractiveResizePage, previousResizeAware))
            {
                _activeInteractiveResizePage = null;
            }

            if (showHome)
            {
                ReloadDashboard();
            }
            else
            {
                if (showMonitor && refreshMonitorPage)
                {
                    _monitorPage.RefreshData();
                }

                if (showAlarm && refreshAlarmPage)
                {
                    _alarmPage.RefreshData();
                }

                if (showInspection)
                {
                    if (refreshInspectionPage)
                    {
                        _inspectionPage.RefreshData();
                    }
                }

                if (showAnalytics && refreshAnalyticsPage)
                {
                    _analyticsPage.RefreshData();
                }
            }

            if (_mainArea is null)
            {
                return;
            }

            _mainArea.SuspendLayout();

            if (_homeView != null)
            {
                _homeView.Visible = false;
            }

            _monitorPage.Visible = false;
            _alarmPage.Visible = false;
            _inspectionPage.Visible = false;
            _analyticsPage.Visible = false;
            _dataInsightPage.Visible = false;

            Control? activeSection = showHome
                ? _homeView
                : showMonitor
                    ? _monitorPage
                    : showAlarm
                        ? _alarmPage
                        : showInspection
                            ? _inspectionPage
                            : showAnalytics
                                ? _analyticsPage
                                : showDataInsight
                                    ? _dataInsightPage
                                    : _homeView;

            if (activeSection is not null)
            {
                activeSection.Visible = true;
                activeSection.BringToFront();
                activeSection.PerformLayout();
            }

            _mainArea.ResumeLayout(true);
            _mainArea.PerformLayout();
            _mainArea.Invalidate(true);
            if (activeSection is not null)
            {
                InvalidateControlTree(activeSection);
                activeSection.Update();
            }

            _mainArea.Update();
        }

        private void UpdateNavigationSelection(int index)
        {
            _activeNavIndex = index;
            if (index >= 0 && index < _navItems.Count)
            {
                var navItem = _navItems[index];
                _navIndicator.Location = new Point(10, navItem.Top + (navItem.Height - _navIndicator.Height) / 2);
            }

            foreach (var item in _navItems)
            {
                item.Invalidate();
            }
        }

        private void NavigateTo(DashboardNavigationTarget target)
        {
            switch (target)
            {
                case DashboardNavigationTarget.InspectionToday:
                    UpdateNavigationSelection(InspectionSectionIndex);
                    _inspectionPage.ShowTodayRecords();
                    SwitchSection(InspectionSectionIndex, refreshInspectionPage: false);
                    break;
                case DashboardNavigationTarget.InspectionPending:
                    UpdateNavigationSelection(InspectionSectionIndex);
                    _inspectionPage.ShowPendingRecords();
                    SwitchSection(InspectionSectionIndex, refreshInspectionPage: false);
                    break;
                case DashboardNavigationTarget.InspectionAbnormal:
                    UpdateNavigationSelection(InspectionSectionIndex);
                    _inspectionPage.ShowAbnormalRecords();
                    SwitchSection(InspectionSectionIndex, refreshInspectionPage: false);
                    break;
                case DashboardNavigationTarget.InspectionCreate:
                    UpdateNavigationSelection(InspectionSectionIndex);
                    SwitchSection(InspectionSectionIndex);
                    _inspectionPage.StartNewEntryFromHome();
                    break;
                case DashboardNavigationTarget.Analytics:
                    UpdateNavigationSelection(AnalyticsSectionIndex);
                    SwitchSection(AnalyticsSectionIndex);
                    break;
            }
        }

        // ==================== 侧栏 ====================
        private ContextMenuStrip CreateAccountMenu()
        {
            const int menuWidth = 228;
            var menu = new ContextMenuStrip
            {
                AutoSize = false,
                Size = new Size(menuWidth, 174),
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Padding = new Padding(8, 10, 8, 10),
                Margin = Padding.Empty,
                BackColor = CurrentTheme.Card,
                ForeColor = CurrentTheme.TextPrimary,
                DropShadowEnabled = false,
                Font = new Font("Microsoft YaHei UI", 9F),
                Renderer = new AccountMenuRenderer(this)
            };

            menu.Opening += (_, _) => UpdateAccountMenuStyle(menu);
            menu.SizeChanged += (_, _) => UpdateAccountMenuRegion(menu);

            var titleLabel = new ToolStripLabel("\u5f53\u524d\u8d26\u53f7")
            {
                AutoSize = false,
                Size = new Size(menuWidth - 16, 22),
                Margin = Padding.Empty,
                Padding = new Padding(20, 4, 16, 0),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = CurrentTheme.TextMuted,
                TextAlign = ContentAlignment.BottomLeft
            };
            var accountLabel = new ToolStripLabel(_account)
            {
                AutoSize = false,
                Size = new Size(menuWidth - 16, 34),
                Margin = Padding.Empty,
                Padding = new Padding(20, 0, 16, 8),
                Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold),
                ForeColor = CurrentTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var separator = new ToolStripSeparator
            {
                Margin = new Padding(0, 2, 0, 6)
            };
            var switchAccountItem = CreateAccountMenuItem("\u5207\u6362\u8d26\u53f7");
            switchAccountItem.Click += (_, _) => OnSwitchAccountRequested();
            var logoutItem = CreateAccountMenuItem("\u9000\u51fa\u767b\u5f55");
            logoutItem.Click += (_, _) => OnLogoutRequested();

            menu.Items.Add(titleLabel);
            menu.Items.Add(accountLabel);
            menu.Items.Add(separator);
            menu.Items.Add(switchAccountItem);
            menu.Items.Add(logoutItem);
            UpdateAccountMenuStyle(menu);
            return menu;
        }

        private ToolStripMenuItem CreateAccountMenuItem(string text)
        {
            return new ToolStripMenuItem(text)
            {
                AutoSize = false,
                Size = new Size(212, 42),
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(28, 12, 18, 12),
                ForeColor = CurrentTheme.TextPrimary,
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
        }

        private void UpdateAccountMenuStyle(ContextMenuStrip menu)
        {
            menu.BackColor = CurrentTheme.Card;
            menu.ForeColor = CurrentTheme.TextPrimary;
            UpdateAccountMenuRegion(menu);
        }

        private static void UpdateAccountMenuRegion(ToolStripDropDown menu)
        {
            if (menu.Width <= 0 || menu.Height <= 0)
            {
                return;
            }

            var oldRegion = menu.Region;
            using var path = CreateRoundRectPath(new Rectangle(0, 0, menu.Width - 1, menu.Height - 1), 18);
            menu.Region = new Region(path);
            oldRegion?.Dispose();
        }

        private void ShowAccountMenu()
        {
            if (_accountMenu.Visible)
            {
                _accountMenu.Close();
                return;
            }

            UpdateAccountMenuStyle(_accountMenu);
            _accountMenu.Show(
                _sidebar,
                new Point(_sidebar.Width + 12, _avatarButton.Top - 6));
        }

        private void OnSwitchAccountRequested()
        {
            _accountMenu.Close();
            SwitchAccountRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnLogoutRequested()
        {
            _accountMenu.Close();
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        private Panel CreateSidebar()
        {
            const int sidebarWidth = 96;
            const int avatarSize = 48;
            const int navButtonSize = 56;
            const int navButtonStep = 62;
            const int navStartY = 88;

            var sidebar = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = sidebarWidth,
                BackColor = CurrentTheme.Sidebar,
                Padding = new Padding(0, 18, 0, 18)
            };
            _sidebar = sidebar;

            // 侧栏绘制
            sidebar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // 右侧分割线（纯色细线，不用渐变）
                var sidebarRect = new Rectangle(0, 0, sidebar.Width - 1, sidebar.Height - 1);
                using var bgBrush = new SolidBrush(CurrentTheme.Sidebar);
                g.FillRectangle(bgBrush, sidebarRect);

                using var topGlow = new LinearGradientBrush(
                    new Rectangle(0, 0, sidebar.Width, 180),
                    Color.FromArgb(8, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    90f);
                g.FillRectangle(topGlow, 0, 0, sidebar.Width, 180);

                using var accentGlow = new LinearGradientBrush(
                    new Rectangle(0, navStartY - 28, sidebar.Width, 260),
                    Color.FromArgb(5, AccentBlue),
                    Color.FromArgb(0, AccentBlue),
                    90f);
                g.FillRectangle(accentGlow, 0, navStartY - 28, sidebar.Width, 260);

                using var innerHighlightPen = new Pen(Color.FromArgb(14, 255, 255, 255), 1);
                g.DrawLine(innerHighlightPen, 0, 0, 0, sidebar.Height);

                using var edgePen = new Pen(Color.FromArgb(54, 88, 98, 126), 1);
                g.DrawLine(edgePen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);

                using var separatorPen = new Pen(Color.FromArgb(10, 255, 255, 255), 1);
                g.DrawLine(separatorPen, sidebar.Width - 2, 0, sidebar.Width - 2, sidebar.Height);
            };

            // 顶部账号入口
            var avatarPanel = new BufferedPanel
            {
                Size = new Size(avatarSize, avatarSize),
                Location = new Point((sidebarWidth - avatarSize) / 2, 18),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _avatarButton = avatarPanel;
            avatarPanel.Click += (s, e) => ShowAccountMenu();
            avatarPanel.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    ShowAccountMenu();
                }
            };
            avatarPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var shadowRect = new Rectangle(1, 3, avatarSize - 4, avatarSize - 4);
                using var shadowPath = CreateRoundRectPath(shadowRect, 16);
                using var shadowBrush = new SolidBrush(Color.FromArgb(22, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);

                var tileRect = new Rectangle(0, 0, avatarSize - 1, avatarSize - 1);
                using var tilePath = CreateRoundRectPath(tileRect, 16);
                using var bgBrush = new LinearGradientBrush(
                    tileRect,
                    Color.FromArgb(46, 50, 65),
                    Color.FromArgb(27, 31, 43),
                    90f);
                g.FillPath(bgBrush, tilePath);

                using var tintBrush = new SolidBrush(Color.FromArgb(16, AccentBlue));
                g.FillPath(tintBrush, tilePath);

                using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f);
                g.DrawPath(borderPen, tilePath);

                var innerRect = Rectangle.Inflate(tileRect, -6, -6);
                using var innerPath = CreateRoundRectPath(innerRect, 12);
                using var innerPen = new Pen(Color.FromArgb(28, 255, 255, 255), 1f);
                g.DrawPath(innerPen, innerPath);

                DrawSidebarGlyph(
                    g,
                    new Rectangle(0, 0, avatarSize, avatarSize),
                    SidebarGlyph.User,
                    Color.FromArgb(232, 236, 244),
                    20);
            };
            sidebar.Controls.Add(avatarPanel);

            // 活动指示器
            _navIndicator = new Panel
            {
                Size = new Size(2, 18),
                Location = new Point(10, navStartY + 22),
                BackColor = Color.FromArgb(150, AccentBlue)
            };
            sidebar.Controls.Add(_navIndicator);

            // 导航项
            SidebarGlyph[] glyphs =
            {
                SidebarGlyph.Home,
                SidebarGlyph.Devices,
                SidebarGlyph.Warning,
                SidebarGlyph.Page,
                SidebarGlyph.Chart
            };
            string[] tips = { "首页", "点检记录", "统计分析", "通知", "设置" };

            tips = new[] { "首页", "设备监控", "报警中心", "巡检管理", "统计分析" };

            glyphs = new[]
            {
                SidebarGlyph.Home,
                SidebarGlyph.Devices,
                SidebarGlyph.Warning,
                SidebarGlyph.Page,
                SidebarGlyph.Chart,
                SidebarGlyph.Import
            };
            tips = new[] { "首页", "设备监控", "报警中心", "巡检管理", "统计分析", "数据导入" };

            for (int i = 0; i < glyphs.Length; i++)
            {
                int idx = i;
                bool isHovered = false;
                var navBtn = new BufferedPanel
                {
                    Size = new Size(sidebarWidth, navButtonStep),
                    Location = new Point(0, navStartY + i * navButtonStep),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var tt = new ToolTip();
                tt.SetToolTip(navBtn, tips[i]);

                var glyph = glyphs[i];

                navBtn.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    bool active = (idx == _activeNavIndex);
                    // 激活=蓝色  悬停=亮白  默认=柔灰
                    Color color;
                    if (active)
                        color = Color.FromArgb(122, 158, 255);
                    else if (isHovered)
                        color = Color.FromArgb(236, 240, 247);
                    else
                        color = Color.FromArgb(176, 184, 199);

                    // 悬停/激活背景
                    if (active)
                    {
                        var bgRect = new Rectangle((navBtn.Width - navButtonSize) / 2, 5, navButtonSize, navButtonSize);
                        var bgPath = CreateRoundRectPath(bgRect, 15);
                        using var bgBrush = new SolidBrush(_isDarkTheme
                            ? Color.FromArgb(42, 46, 62)
                            : Color.FromArgb(42, AccentBlue));
                        g.FillPath(bgBrush, bgPath);
                        using var tintBrush = new SolidBrush(Color.FromArgb(14, AccentBlue));
                        g.FillPath(tintBrush, bgPath);
                        using var borderPen = new Pen(Color.FromArgb(92, 148, 174, 255), 1f);
                        g.DrawPath(borderPen, bgPath);
                    }
                    else if (isHovered)
                    {
                        var bgRect = new Rectangle((navBtn.Width - navButtonSize) / 2, 5, navButtonSize, navButtonSize);
                        var bgPath = CreateRoundRectPath(bgRect, 15);
                        using var bgBrush = new SolidBrush(_isDarkTheme
                            ? Color.FromArgb(34, 38, 50)
                            : Color.FromArgb(238, 245, 252));
                        g.FillPath(bgBrush, bgPath);
                        using var borderPen = new Pen(Color.FromArgb(36, 255, 255, 255), 1f);
                        g.DrawPath(borderPen, bgPath);
                    }

                    DrawSidebarGlyph(
                        g,
                        new Rectangle((navBtn.Width - navButtonSize) / 2, 5, navButtonSize, navButtonSize),
                        glyph,
                        color,
                        active ? 24 : 23);
                };

                navBtn.Click += (s, e) =>
                {
                    UpdateNavigationSelection(idx);
                    SwitchSection(idx);
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
                Height = 120,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 18, 10, 0)
            };

            _themeToggleButton = new BufferedPanel
            {
                Size = new Size(40, 44),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
                Enabled = false,
                Visible = false
            };

            bool isThemeHovered = false;
            _themeToggleButton.MouseEnter += (s, e) => { isThemeHovered = true; _themeToggleButton.Invalidate(); };
            _themeToggleButton.MouseLeave += (s, e) => { isThemeHovered = false; _themeToggleButton.Invalidate(); };

            _themeToggleButton.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int yOffset = isThemeHovered ? 0 : 3;
                var rect = new Rectangle(0, yOffset, 39, 39);

                // 绘制投影效果（仅在悬停空间内出现）
                if (isThemeHovered)
                {
                    var shadowRect = new Rectangle(0, 4, 39, 39);
                    var shadowPath = CreateRoundRectPath(shadowRect, 8);
                    using var shadowBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? 20 : 15, 0, 0, 0));
                    g.FillPath(shadowBrush, shadowPath);
                }

                var path = CreateRoundRectPath(rect, 8);
                
                using var bgBrush = new SolidBrush(_isDarkTheme
                    ? (isThemeHovered ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(40, 255, 255, 255))
                    : (isThemeHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(245, 250, 255)));
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(_isDarkTheme
                    ? (isThemeHovered ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(60, 255, 255, 255))
                    : (isThemeHovered ? Color.FromArgb(144, 160, 184) : Color.FromArgb(158, 172, 192)), 1.2f);
                g.DrawPath(borderPen, path);

                string icon = _isDarkTheme ? "☀" : "☾";
                Color iconColor = _isDarkTheme ? AccentOrange : AccentBlue;
                using var font = new Font("Segoe UI Symbol", 13F, FontStyle.Regular);
                using var iconBrush = new SolidBrush(iconColor);
                var ts = g.MeasureString(icon, font);
                g.DrawString(icon, font, iconBrush, (40 - ts.Width) / 2 + 1, yOffset + (40 - ts.Height) / 2 + 1);
            };
            header.Controls.Add(_themeToggleButton);
            header.Resize += (s, e) =>
            {
                _themeToggleButton.Location = new Point(header.Width - 55, 9);
            };

            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode = _isInteractiveResize ? SmoothingMode.HighSpeed : SmoothingMode.AntiAlias;

                float alpha = Math.Min(1f, _animProgress * 2f);

                // 标题
                Font titleFont;
                int titleA = _isDarkTheme ? 235 : 255;
                int subA = _isDarkTheme ? 155 : 255;
                Color headerTextColor = _isDarkTheme ? CurrentTheme.TextSecondary : Color.FromArgb(34, 44, 58);
                var titleColor = Color.FromArgb((int)(alpha * titleA), CurrentTheme.TextPrimary);
                var subtitleColor = Color.FromArgb((int)(alpha * subA), headerTextColor);

                // 右上角 - 时间显示
                string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                var timeFont = SelectSingleLineFont(timeStr, HeaderSubtitleFonts, 180F);
                var timeSize = MeasureSingleLineText(timeStr, timeFont);
                int timeA = _isDarkTheme ? 180 : 255;
                using var timeBrush = new SolidBrush(Color.FromArgb((int)(alpha * timeA), headerTextColor));
                var timeX = header.Width - timeSize.Width - 18F;
                g.DrawString(
                    timeStr,
                    timeFont,
                    timeBrush,
                    new RectangleF(timeX, 24F, timeSize.Width + 2F, timeSize.Height + 4F),
                    TopAlignedSingleLineTextFormat);

                var contentWidth = Math.Max(240F, timeX - 28F);
                titleFont = SelectSingleLineFont(_dashboard.HeaderTitle, HeaderTitleFonts, contentWidth);
                var titleSize = MeasureSingleLineText(_dashboard.HeaderTitle, titleFont);
                var titleRect = new RectangleF(10F, 10F, contentWidth, titleSize.Height + 6F);
                var subtitleTop = titleRect.Bottom + 6F;
                var subtitleHeight = Math.Max(24F, header.Height - subtitleTop - 12F);
                var subtitleFont = SelectWrappedFont(g, _dashboard.HeaderSubtitle, HeaderSubtitleFonts, contentWidth, subtitleHeight);
                var subtitleNeedsWrap = MeasureSingleLineText(_dashboard.HeaderSubtitle, subtitleFont).Width > contentWidth;
                var subtitleSize = subtitleNeedsWrap
                    ? MeasureWrappedText(g, _dashboard.HeaderSubtitle, subtitleFont, contentWidth)
                    : MeasureSingleLineText(_dashboard.HeaderSubtitle, subtitleFont);
                var subtitleRect = new RectangleF(12F, subtitleTop, contentWidth, Math.Max(subtitleHeight, subtitleSize.Height + 4F));
                using var titleBrush = new SolidBrush(titleColor);
                using var subtitleBrush = new SolidBrush(subtitleColor);
                g.DrawString(_dashboard.HeaderTitle, titleFont, titleBrush, titleRect, TopAlignedSingleLineTextFormat);
                g.DrawString(
                    _dashboard.HeaderSubtitle,
                    subtitleFont,
                    subtitleBrush,
                    subtitleRect,
                    subtitleNeedsWrap ? WrappedTextFormat : TopAlignedSingleLineTextFormat);
            };

            return header;
        }

        // ==================== 统计卡片区 ====================
        private Panel CreateCardsArea()
        {
            var container = new BufferedPanel
            {
                Dock = DockStyle.Top,
                Height = 208,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 10)
            };

            WireDashboardRegionEvents(container, _cardHitRegions);

            container.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = _isInteractiveResize ? SmoothingMode.HighSpeed : SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                _cardHitRegions.Clear();

                var cardData = _dashboard.Cards;
                int cardCount = Math.Max(1, cardData.Count);
                int totalWidth = container.Width - 20;
                int cardWidth = (totalWidth - 15 * (cardCount - 1)) / cardCount;
                int cardHeight = 168;
                int y = 15;

                for (int i = 0; i < cardData.Count; i++)
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
                        if (!_isInteractiveResize)
                        {
                            DrawDarkSurfaceShadow(g, rect, 16);
                        }
                        
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

                    if (cardData[i].NavigationTarget != DashboardNavigationTarget.None)
                    {
                        _cardHitRegions.Add(new DashboardHitRegion(rect, cardData[i].NavigationTarget));
                    }

                    var cardIconText = cardData[i].Icon;
                    var cardIconFontSize = cardIconText.Length switch
                    {
                        >= 4 => 8.5F,
                        3 => 9.5F,
                        _ => 11F
                    };
                    using var iconFont = new Font("Microsoft YaHei UI", cardIconFontSize, FontStyle.Bold);
                    var iconTextSize = MeasureSingleLineText(cardIconText, iconFont);
                    var iconBgWidth = Math.Max(44, iconTextSize.Width + 22);
                    const int iconBgHeight = 32;
                    var iconBgRect = new Rectangle(rect.X + 20, rect.Y + 26, iconBgWidth, iconBgHeight);
                    using var iconBgBrush = new SolidBrush(_isDarkTheme
                        ? Color.FromArgb(Math.Min(50, alpha / 5), cardData[i].AccentColor)
                        : Color.FromArgb(35, cardData[i].AccentColor));
                    var iconBgPath = CreateRoundRectPath(iconBgRect, 12);
                    g.FillPath(iconBgBrush, iconBgPath);

                    using var iconBrush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), cardData[i].AccentColor));
                    g.DrawString(
                        cardIconText,
                        iconFont,
                        iconBrush,
                        iconBgRect.X + (iconBgRect.Width - iconTextSize.Width) / 2,
                        iconBgRect.Y + (iconBgRect.Height - iconTextSize.Height) / 2F - 1F);

                    // 数值与说明按固定文本区域绘制，避免中文字体高度变化时互相重叠
                    var contentWidth = rect.Width - 40;
                    var valueFont = SelectSingleLineFont(cardData[i].Value, CardValueFonts, contentWidth);
                    var labelFont = SelectSingleLineFont(cardData[i].Title, CardLabelFonts, contentWidth);
                    var subFont = SelectSingleLineFont(cardData[i].Detail, CardDetailFonts, contentWidth);
                    var valueSize = MeasureSingleLineText(cardData[i].Value, valueFont);
                    var labelSize = MeasureSingleLineText(cardData[i].Title, labelFont);
                    var detailSize = MeasureSingleLineText(cardData[i].Detail, subFont);
                    var valueRect = new RectangleF(rect.X + 20F, rect.Y + 68F, contentWidth, valueSize.Height + 6F);
                    var titleRect = new RectangleF(rect.X + 20F, valueRect.Bottom + 2F, contentWidth, labelSize.Height + 4F);
                    var detailRect = new RectangleF(rect.X + 20F, titleRect.Bottom + 2F, contentWidth, detailSize.Height + 4F);
                    var overflow = detailRect.Bottom - (rect.Bottom - 16F);
                    if (overflow > 0)
                    {
                        valueRect.Y -= overflow;
                        titleRect.Y -= overflow;
                        detailRect.Y -= overflow;
                    }
                    var valueColor = Color.FromArgb(Math.Min(255, alpha), CurrentTheme.TextPrimary);
                    var labelColor = Color.FromArgb(_isDarkTheme ? Math.Min(230, alpha) : Math.Min(255, alpha), CurrentTheme.TextSecondary);
                    var detailColor = Color.FromArgb(Math.Min(255, alpha), cardData[i].AccentColor);

                    using var valueBrush = new SolidBrush(valueColor);
                    using var labelBrush = new SolidBrush(labelColor);
                    using var detailBrush = new SolidBrush(detailColor);
                    g.DrawString(cardData[i].Value, valueFont, valueBrush, valueRect, TopAlignedSingleLineTextFormat);
                    g.DrawString(cardData[i].Title, labelFont, labelBrush, titleRect, TopAlignedSingleLineTextFormat);
                    g.DrawString(cardData[i].Detail, subFont, detailBrush, detailRect, TopAlignedSingleLineTextFormat);
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

            WireDashboardRegionEvents(bottom, _quickActionHitRegions);

            bottom.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = _isInteractiveResize ? SmoothingMode.HighSpeed : SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                _quickActionHitRegions.Clear();

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
                    if (!_isInteractiveResize)
                    {
                        DrawDarkSurfaceShadow(g, leftRect, 16);
                    }
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
                    
                    var panelTitleFont = PanelTitleFont;
                    using var panelTitleBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(235, alpha) : Math.Min(255, alpha), CurrentTheme.TextPrimary));
                    g.DrawString("最近活动", panelTitleFont, panelTitleBrush, leftRect.X + 24, leftRect.Y + 18);

                    // 分割线
                    using var dividerPen = new Pen(_isDarkTheme
                        ? Color.FromArgb(Math.Min(40, alpha / 6), 255, 255, 255)
                        : CurrentTheme.Border, 1);
                    g.DrawLine(dividerPen, leftRect.X + 24, leftRect.Y + 52, leftRect.Right - 24, leftRect.Y + 52);

                    // 日志条目
                    var logs = _dashboard.Activities;

                    int logY = leftRect.Y + 64;
                    int logH = Math.Min(42, (panelH - 80) / Math.Max(1, logs.Count));

                    for (int i = 0; i < logs.Count && logY + logH < leftRect.Bottom - 10; i++)
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
                        var statusFont = StatusBadgeFont;
                        var statusText = logs[i].Status;
                        var statusSize = MeasureSingleLineText(statusText, statusFont);
                        var statusRect = new Rectangle(
                            leftRect.X + 24,
                            logY + Math.Max(0, (logH - 22) / 2),
                            statusSize.Width + 18,
                            22);
                        using (var statusPath = CreateRoundRectPath(statusRect, 11))
                        {
                            using var statusFill = new SolidBrush(Color.FromArgb(Math.Min(54, logAlpha / 5), logs[i].AccentColor));
                            using var statusBorder = new Pen(Color.FromArgb(Math.Min(120, logAlpha / 2), logs[i].AccentColor), 1f);
                            g.FillPath(statusFill, statusPath);
                            g.DrawPath(statusBorder, statusPath);
                        }

                        using var statusBrush = new SolidBrush(Color.FromArgb(Math.Min(255, logAlpha), logs[i].AccentColor));
                        g.DrawString(
                            statusText,
                            statusFont,
                            statusBrush,
                            new RectangleF(statusRect.X + 9F, statusRect.Y, statusRect.Width - 12F, statusRect.Height),
                            CenteredSingleLineTextFormat);

                        // 日志文本
                        var logFont = SelectSingleLineFont(logs[i].Text, ActivityFonts, Math.Max(40F, leftRect.Right - (statusRect.Right + 12F) - 120F));
                        using var logBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(210, logAlpha) : Math.Min(255, logAlpha), CurrentTheme.TextPrimary));
                        var logText = logs[i].Text;

                        // 时间
                        var timeFont = ActivityTimeFont;
                        string timeStr = logs[i].Time;
                        var timeSize = MeasureSingleLineText(timeStr, timeFont);
                        using var timeBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(120, logAlpha) : Math.Min(190, logAlpha), CurrentTheme.TextMuted));
                        g.DrawString(
                            timeStr,
                            timeFont,
                            timeBrush,
                            new RectangleF(leftRect.Right - timeSize.Width - 24F, logY, timeSize.Width + 2F, logH),
                            CenteredSingleLineTextFormat);
                        var logRect = new RectangleF(
                            statusRect.Right + 12,
                            logY,
                            Math.Max(40, leftRect.Right - timeSize.Width - 36 - (statusRect.Right + 12)),
                            logH);
                        g.DrawString(logText, logFont, logBrush, logRect, CenteredSingleLineTextFormat);

                        logY += logH;
                    }
                }

                // ===== 右侧：快捷操作面板 =====
                var rightRect = new Rectangle(10 + leftW + 15, 10, rightW, panelH);
                if (_isDarkTheme)
                {
                    var rightPath = CreateRoundRectPath(rightRect, 16);
                    if (!_isInteractiveResize)
                    {
                        DrawDarkSurfaceShadow(g, rightRect, 16);
                    }
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
                    var rtFont = PanelTitleFont;
                    using var rtBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(235, alpha) : Math.Min(255, alpha), CurrentTheme.TextPrimary));
                    g.DrawString("快捷操作", rtFont, rtBrush, rightRect.X + 24, rightRect.Y + 18);

                    using var divPen = new Pen(_isDarkTheme
                        ? Color.FromArgb(Math.Min(40, alpha / 6), 255, 255, 255)
                        : CurrentTheme.Border, 1);
                    g.DrawLine(divPen, rightRect.X + 24, rightRect.Y + 52, rightRect.Right - 24, rightRect.Y + 52);

                    // 操作按钮
                    var actions = _dashboard.QuickActions;

                    int btnY = rightRect.Y + 66;
                    int btnH = Math.Min(52, (panelH - 90) / Math.Max(1, actions.Count));
                    int btnW = rightW - 48;

                    for (int i = 0; i < actions.Count && btnY + btnH < rightRect.Bottom - 10; i++)
                    {
                        float btnDelay = delay + 0.1f + 0.1f * i;
                        float btnProgress = Math.Max(0, Math.Min(1, (_animProgress - btnDelay) * 2.5f));
                        float btnEased = EaseOutCubic(btnProgress);
                        int btnAlpha = (int)(btnEased * 255);

                        if (btnAlpha < 5) continue;

                        var btnRect = new Rectangle(rightRect.X + 24, btnY, btnW, btnH - 8);
                        var btnPath = CreateRoundRectPath(btnRect, 12);

                        if (actions[i].NavigationTarget != DashboardNavigationTarget.None)
                        {
                            _quickActionHitRegions.Add(new DashboardHitRegion(btnRect, actions[i].NavigationTarget));
                        }

                        // 渐变按钮背景
                        if (_isDarkTheme)
                        {
                            using var btnGradient = new LinearGradientBrush(btnRect,
                                Color.FromArgb(Math.Min(35, btnAlpha / 7), actions[i].PrimaryAccent),
                                Color.FromArgb(Math.Min(15, btnAlpha / 16), actions[i].SecondaryAccent), 0f);
                            g.FillPath(btnGradient, btnPath);
                        }
                        else
                        {
                            using var btnBg = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
                            g.FillPath(btnBg, btnPath);
                            using var btnTint = new SolidBrush(Color.FromArgb(50, actions[i].PrimaryAccent));
                            g.FillPath(btnTint, btnPath);
                        }

                        // 边框
                        using var btnBorderPen = new Pen(_isDarkTheme
                            ? Color.FromArgb(Math.Min(50, btnAlpha / 5), actions[i].PrimaryAccent)
                            : Color.FromArgb(Math.Min(220, btnAlpha), actions[i].PrimaryAccent), 1.2f);
                        g.DrawPath(btnBorderPen, btnPath);

                        var actionIconText = actions[i].Icon;
                        var actionIconFontSize = actionIconText.Length >= 3 ? 8.8F : 10.2F;
                        using var btnIconFont = new Font("Microsoft YaHei UI", actionIconFontSize, FontStyle.Bold);
                        using var btnIconBrush = new SolidBrush(Color.FromArgb(Math.Min(255, btnAlpha), actions[i].PrimaryAccent));
                        var btnIconSize = MeasureSingleLineText(actionIconText, btnIconFont);
                        var btnBadgeWidth = Math.Max(36, btnIconSize.Width + 18);
                        var btnBadgeRect = new Rectangle(btnRect.X + 14, btnRect.Y + (btnRect.Height - 26) / 2, btnBadgeWidth, 26);
                        using var btnBadgePath = CreateRoundRectPath(btnBadgeRect, 9);
                        using var btnBadgeBrush = new SolidBrush(Color.FromArgb(Math.Min(34, btnAlpha / 6), actions[i].PrimaryAccent));
                        using var btnBadgeBorder = new Pen(Color.FromArgb(Math.Min(86, btnAlpha / 3), actions[i].PrimaryAccent), 1f);
                        g.FillPath(btnBadgeBrush, btnBadgePath);
                        g.DrawPath(btnBadgeBorder, btnBadgePath);
                        g.DrawString(
                            actionIconText,
                            btnIconFont,
                            btnIconBrush,
                            btnBadgeRect.X + (btnBadgeRect.Width - btnIconSize.Width) / 2,
                            btnBadgeRect.Y + (btnBadgeRect.Height - btnIconSize.Height) / 2F - 1F);

                        // 文字
                        var btnTextFont = SelectSingleLineFont(actions[i].Text, QuickActionFonts, Math.Max(60F, btnRect.Width - btnBadgeRect.Width - 72F));
                        using var btnTextBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(220, btnAlpha) : Math.Min(255, btnAlpha), CurrentTheme.TextPrimary));
                        var arrowText = "\u2192";
                        var arrowSize = MeasureSingleLineText(arrowText, ArrowFont);
                        var textRect = new RectangleF(
                            btnBadgeRect.Right + 14F,
                            btnRect.Y,
                            Math.Max(60F, btnRect.Right - btnBadgeRect.Right - arrowSize.Width - 56F),
                            btnRect.Height);
                        g.DrawString(actions[i].Text, btnTextFont, btnTextBrush, textRect, CenteredSingleLineTextFormat);

                        // 箭头
                        
                        var arrowFont = ArrowFont;
                        using var arrowBrush = new SolidBrush(Color.FromArgb(_isDarkTheme ? Math.Min(100, btnAlpha) : Math.Min(180, btnAlpha), CurrentTheme.TextMuted));
                        g.DrawString("→", arrowFont, arrowBrush, btnRect.Right - 30, btnRect.Y + (btnRect.Height - 18) / 2);

                        btnY += btnH;
                    }
                }
            };

            return bottom;
        }

        // ==================== 动画回调 ====================
        private void WireDashboardRegionEvents(Control control, IReadOnlyList<DashboardHitRegion> regions)
        {
            control.MouseMove += (_, e) =>
            {
                control.Cursor = TryGetDashboardTarget(regions, e.Location, out DashboardNavigationTarget _)
                    ? Cursors.Hand
                    : Cursors.Default;
            };
            control.MouseLeave += (_, _) => control.Cursor = Cursors.Default;
            control.MouseClick += (_, e) =>
            {
                if (TryGetDashboardTarget(regions, e.Location, out var target))
                {
                    NavigateTo(target);
                }
            };
        }

        private static bool TryGetDashboardTarget(
            IReadOnlyList<DashboardHitRegion> regions,
            Point location,
            out DashboardNavigationTarget target)
        {
            for (var index = regions.Count - 1; index >= 0; index--)
            {
                if (!regions[index].Bounds.Contains(location))
                {
                    continue;
                }

                target = regions[index].Target;
                return true;
            }

            target = DashboardNavigationTarget.None;
            return false;
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            if (_animProgress < 1.0f)
            {
                _animProgress += 0.018f;
                InvalidateHomeSections();
                _sidebar?.Invalidate();

                // 通知所有子面板重绘
            }
            else
            {
                _animProgress = 1.0f;
                _animTimer.Stop();
                InvalidateHomeSections();
                _sidebar?.Invalidate();
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

        private static void DrawSidebarGlyph(
            Graphics g,
            Rectangle bounds,
            SidebarGlyph glyph,
            Color color,
            float size)
        {
            using var font = new Font(SidebarIconFontFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(
                g,
                glyph switch
                {
                    SidebarGlyph.Home => "\uE80F",
                    SidebarGlyph.Devices => "\uE772",
                    SidebarGlyph.Warning => "\uE7BA",
                    SidebarGlyph.Page => "\uE7C3",
                    SidebarGlyph.Chart => "\uE9D9",
                    SidebarGlyph.Import => "\uE8B5",
                    SidebarGlyph.Notification => "\uE7E7",
                    SidebarGlyph.Setting => "\uE713",
                    SidebarGlyph.User => "\uE77B",
                    _ => string.Empty
                },
                font,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
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

        private void InvalidateHomeSections()
        {
            _homeHeader?.Invalidate();
            _homeCardsArea?.Invalidate();
            _homeBottomArea?.Invalidate();
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

        private static Size MeasureSingleLineText(string text, Font font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Size.Empty;
            }

            return TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private static SizeF MeasureWrappedText(Graphics g, string text, Font font, float width)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SizeF.Empty;
            }

            return g.MeasureString(
                text,
                font,
                new SizeF(Math.Max(1F, width), 200F),
                WrappedTextFormat);
        }

        private static Font SelectSingleLineFont(string text, Font[] candidates, float maxWidth)
        {
            foreach (var candidate in candidates)
            {
                if (MeasureSingleLineText(text, candidate).Width <= maxWidth)
                {
                    return candidate;
                }
            }

            return candidates[^1];
        }

        private static Font SelectWrappedFont(Graphics g, string text, Font[] candidates, float maxWidth, float maxHeight)
        {
            foreach (var candidate in candidates)
            {
                if (MeasureWrappedText(g, text, candidate, maxWidth).Height <= maxHeight + 1F)
                {
                    return candidate;
                }
            }

            return candidates[^1];
        }

        private static StringFormat CreateSingleLineTextFormat(StringAlignment lineAlignment)
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = lineAlignment,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
        }

        private static StringFormat CreateWrappedTextFormat()
        {
            return new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisWord
            };
        }

        // 双缓冲减少闪烁
    }
}
