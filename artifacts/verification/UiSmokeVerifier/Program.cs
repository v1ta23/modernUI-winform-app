using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var dashboardForm = CreateDashboardForm("smoke-user");
        dashboardForm.StartPosition = FormStartPosition.CenterScreen;
        dashboardForm.Size = new Size(1180, 720);
        dashboardForm.MinimumSize = new Size(1180, 720);
        dashboardForm.Show();
        PumpUi();

        VerifyDeviceMonitor(dashboardForm);
        VerifyAlarmCenter(dashboardForm);
        VerifyAnalytics(dashboardForm);
        VerifyDataInsight(dashboardForm);
        VerifyResizeSnapshot(dashboardForm);
        VerifyInspectionResizeCompatibility(dashboardForm);

        dashboardForm.Close();
        PumpUi();
        Console.WriteLine("UI smoke passed");
    }

    private static Form CreateDashboardForm(string account)
    {
        var appAssembly = Assembly.Load("WinFormsApp");
        var compositionRootType = appAssembly.GetType("WinFormsApp.AppCompositionRoot", throwOnError: true)!;
        var compositionRoot = Activator.CreateInstance(compositionRootType, nonPublic: true)!;
        return (Form)compositionRootType
            .GetMethod("CreateDashboardForm", InstanceFlags, [typeof(string)])!
            .Invoke(compositionRoot, [account])!;
    }

    private static void VerifyDeviceMonitor(Form dashboardForm)
    {
        SwitchSection(dashboardForm, 1);
        var page = GetRequiredPage(dashboardForm, "_monitorPage");
        var pageType = page.GetType();
        var summaryLayout = GetField<TableLayoutPanel>(page, "_summaryLayout");
        var bodyLayout = GetField<TableLayoutPanel>(page, "_bodyLayout");
        var deviceGrid = GetField<DataGridView>(page, "_deviceGrid");
        var attentionGrid = GetField<DataGridView>(page, "_attentionGrid");
        var issueNoteLabel = GetField<Label>(page, "_issueDeviceNoteLabel");

        Assert(summaryLayout.ColumnCount == 2 && summaryLayout.RowCount == 2, "Device page summary cards did not switch to 2x2 at 1180px.");
        Assert(bodyLayout.ColumnCount == 1 && bodyLayout.RowCount == 2, "Device page body did not stack at 1180px.");
        Assert(deviceGrid.Rows.Count > 0, "Device page main grid is empty.");
        Assert(attentionGrid.Rows.Count > 0, "Device page attention grid is empty.");
        Assert(deviceGrid.Columns.Count > 1 && deviceGrid.Columns[^1].AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill, "Device page grid no longer keeps only the last column flexible.");
        Assert(deviceGrid.Columns[0].AutoSizeMode == DataGridViewAutoSizeColumnMode.None, "Device page grid still recalculates every column width.");
        AssertLabelFits(issueNoteLabel, "Device page summary note is clipped.");
        AssertWithinParent(GetField<Control>(page, "_summaryLayout"), "Device page summary layout overflowed its parent.");
    }

    private static void VerifyAlarmCenter(Form dashboardForm)
    {
        SwitchSection(dashboardForm, 2);
        var page = GetRequiredPage(dashboardForm, "_alarmPage");
        var summaryLayout = GetField<TableLayoutPanel>(page, "_summaryLayout");
        var bodyLayout = GetField<TableLayoutPanel>(page, "_bodyLayout");
        var pendingGrid = GetField<DataGridView>(page, "_pendingGrid");
        var historyGrid = GetField<DataGridView>(page, "_historyGrid");

        Assert(summaryLayout.ColumnCount == 2 && summaryLayout.RowCount == 2, "Alarm page summary cards did not switch to 2x2 at 1180px.");
        Assert(bodyLayout.ColumnCount == 1 && bodyLayout.RowCount == 2, "Alarm page content layout is not the expected vertical stack.");
        Assert(pendingGrid.Rows.Count > 0, "Alarm page pending grid is empty.");
        Assert(historyGrid.Columns.Count > 1 && historyGrid.Columns[^1].AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill, "Alarm history grid lost the single flexible column.");
        Assert(historyGrid.Columns[0].AutoSizeMode == DataGridViewAutoSizeColumnMode.None, "Alarm history grid still uses heavy autosizing.");
    }

    private static void VerifyAnalytics(Form dashboardForm)
    {
        SwitchSection(dashboardForm, 4);
        var page = GetRequiredPage(dashboardForm, "_analyticsPage");
        var summaryLayout = GetField<TableLayoutPanel>(page, "_summaryLayout");
        var aiSummaryLayout = GetField<TableLayoutPanel>(page, "_aiSummaryLayout");
        var lineSummaryGrid = GetField<DataGridView>(page, "_lineSummaryGrid");
        var issueGrid = GetField<DataGridView>(page, "_issueGrid");
        var decisionValueLabel = GetField<Label>(page, "_decisionValueLabel");
        var decisionNoteLabel = GetField<Label>(page, "_decisionNoteLabel");
        var totalNoteLabel = GetField<Label>(page, "_totalNoteLabel");

        Assert(summaryLayout.ColumnCount == 2 && summaryLayout.RowCount == 2, "Analytics summary cards did not switch to 2x2 at 1180px.");
        Assert(aiSummaryLayout.ColumnCount == 1 && aiSummaryLayout.RowCount == 2, "Analytics AI area did not stack at 1180px.");
        Assert(lineSummaryGrid.Rows.Count > 0, "Analytics line summary grid is empty.");
        Assert(issueGrid.Rows.Count > 0, "Analytics issue grid is empty.");
        AssertLabelFits(decisionValueLabel, "Analytics decision headline is clipped.");
        AssertLabelFits(decisionNoteLabel, "Analytics decision note is clipped.");
        AssertLabelFits(totalNoteLabel, "Analytics summary note is clipped.");
    }

    private static void VerifyDataInsight(Form dashboardForm)
    {
        SwitchSection(dashboardForm, 5);
        var page = GetRequiredPage(dashboardForm, "_dataInsightPage");
        var pageType = page.GetType();
        var tempFile = Path.Combine(Path.GetTempPath(), $"codex-ui-smoke-{Guid.NewGuid():N}.csv");

        File.WriteAllLines(
            tempFile,
            [
                "产线,设备名称,点检项目,状态,点检时间,测量值,备注",
                $"{DateTime.Today:yyyy-MM-dd},Smoke-A01,压力,正常,{DateTime.Now.AddMinutes(-2):yyyy-MM-dd HH:mm:ss},20,首轮校验",
                $"{DateTime.Today:yyyy-MM-dd},Smoke-A02,振动,异常,{DateTime.Now.AddMinutes(-1):yyyy-MM-dd HH:mm:ss},21,需要复核",
                $"{DateTime.Today:yyyy-MM-dd},Smoke-B01,温度,预警,{DateTime.Now:yyyy-MM-dd HH:mm:ss},22,需要跟进"
            ]);

        try
        {
            var actionLayout = GetField<TableLayoutPanel>(page, "_actionBarLayout");
            var previewValidationLayout = GetField<TableLayoutPanel>(page, "_previewValidationLayout");
            var bottomInfoLayout = GetField<TableLayoutPanel>(page, "_bottomInfoLayout");
            var currentFileLabel = GetField<Label>(page, "_currentFileLabel");

            Assert(actionLayout.ColumnCount == 1 && actionLayout.RowCount == 2, "Data insight action bar did not wrap into two rows at 1180px.");
            Assert(previewValidationLayout.ColumnCount == 1 && previewValidationLayout.RowCount == 2, "Data insight preview and validation panels did not stack at 1180px.");
            Assert(
                (bottomInfoLayout.ColumnCount == 2 && bottomInfoLayout.RowCount == 1) ||
                (bottomInfoLayout.ColumnCount == 1 && bottomInfoLayout.RowCount == 2),
                "Data insight bottom cards did not resolve to a stable horizontal or vertical layout.");
            AssertLabelFits(currentFileLabel, "Data insight current-file label is clipped before loading data.");

            pageType.GetMethod("LoadCsvFile", InstanceFlags, [typeof(string)])!.Invoke(page, [tempFile]);
            PumpUi();

            var previewGrid = GetField<DataGridView>(page, "_previewGrid");
            var validationSummaryLabel = GetField<Label>(page, "_validationSummaryLabel");

            Assert(previewGrid.Rows.Count > 0, "Data insight preview grid did not load CSV rows.");
            Assert(previewGrid.Columns.Count > 1 && previewGrid.Columns[^1].AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill, "Data insight preview grid lost the single fill column.");
            Assert(previewGrid.Columns[0].AutoSizeMode == DataGridViewAutoSizeColumnMode.None, "Data insight preview grid still uses Fill for every column.");
            AssertLabelFits(validationSummaryLabel, "Data insight validation summary is clipped after loading data.");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static void VerifyResizeSnapshot(Form dashboardForm)
    {
        var dashboardType = dashboardForm.GetType();
        var beginInteractiveResize = dashboardType.GetMethod("BeginInteractiveResize", InstanceFlags)!;
        var endInteractiveResize = dashboardType.GetMethod("EndInteractiveResize", InstanceFlags)!;
        var snapshotOverlay = GetField<PictureBox>(dashboardForm, "_resizeSnapshotOverlay");
        var mainArea = GetField<Control>(dashboardForm, "_mainArea");
        var sidebar = GetField<Control>(dashboardForm, "_sidebar");

        beginInteractiveResize.Invoke(dashboardForm, null);
        PumpUi();

        Assert(snapshotOverlay.Visible, "Resize snapshot overlay did not appear when interactive resize started.");
        Assert(snapshotOverlay.Image is not null, "Resize snapshot overlay did not capture an image.");
        Assert(!mainArea.Visible && !sidebar.Visible, "Real dashboard controls were not hidden during snapshot resize mode.");

        dashboardForm.Size = new Size(1400, 860);
        PumpUi();
        dashboardForm.Size = new Size(1180, 720);
        PumpUi();

        endInteractiveResize.Invoke(dashboardForm, null);
        PumpUi();

        Assert(!snapshotOverlay.Visible, "Resize snapshot overlay stayed visible after interactive resize ended.");
        Assert(mainArea.Visible && sidebar.Visible, "Dashboard controls did not return after interactive resize.");
    }

    private static void VerifyInspectionResizeCompatibility(Form dashboardForm)
    {
        SwitchSection(dashboardForm, 3);
        var inspectionPage = GetRequiredPage(dashboardForm, "_inspectionPage");
        Assert(inspectionPage.Visible, "Inspection page did not become visible.");

        var dashboardType = dashboardForm.GetType();
        dashboardType.GetMethod("BeginInteractiveResize", InstanceFlags)!.Invoke(dashboardForm, null);
        PumpUi();
        dashboardType.GetMethod("EndInteractiveResize", InstanceFlags)!.Invoke(dashboardForm, null);
        PumpUi();

        var snapshotOverlay = GetField<PictureBox>(dashboardForm, "_resizeSnapshotOverlay");
        Assert(!snapshotOverlay.Visible, "Inspection page left the snapshot overlay stuck on screen.");
    }

    private static void SwitchSection(Form dashboardForm, int index)
    {
        dashboardForm.GetType()
            .GetMethod("SwitchSection", InstanceFlags)!
            .Invoke(dashboardForm, [index, true, true, true, true]);
        PumpUi();
    }

    private static Control GetRequiredPage(Form dashboardForm, string fieldName)
    {
        var page = GetField<Control?>(dashboardForm, fieldName);
        Assert(page is not null, $"Dashboard page field {fieldName} was not created.");
        return page!;
    }

    private static T GetField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, InstanceFlags)
            ?? throw new InvalidOperationException($"Missing field {fieldName} on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }

    private static void AssertLabelFits(Label label, string message)
    {
        if (label.AutoSize)
        {
            Assert(label.Height == label.PreferredHeight || label.Height >= label.PreferredHeight - 2, message);
            return;
        }

        var preferred = label.GetPreferredSize(new Size(Math.Max(120, label.Width), 0));
        Assert(preferred.Height <= label.Height + 2, message);
    }

    private static void AssertWithinParent(Control control, string message)
    {
        if (control.Parent is null)
        {
            return;
        }

        Assert(control.Right <= control.Parent.ClientSize.Width + 2, message);
        Assert(control.Bottom <= control.Parent.ClientSize.Height + 2, message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void PumpUi(int cycles = 4, int delayMs = 80)
    {
        for (var index = 0; index < cycles; index++)
        {
            Application.DoEvents();
            Thread.Sleep(delayMs);
        }
    }
}
