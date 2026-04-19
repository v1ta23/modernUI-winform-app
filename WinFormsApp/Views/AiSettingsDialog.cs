using App.Core.Models;
using System.Net.Http;

namespace WinFormsApp.Views;

internal sealed class AiSettingsDialog : Form
{
    private readonly TextBox _apiKeyTextBox;
    private readonly TextBox _baseUrlTextBox;
    private readonly TextBox _modelTextBox;
    private readonly CheckBox _showKeyCheckBox;
    private readonly Func<AiRiskAnalysisSettings, CancellationToken, Task> _testConnectionAsync;
    private readonly Button _testButton;

    public AiRiskAnalysisSettings Settings { get; private set; }

    public AiSettingsDialog(
        AiRiskAnalysisSettings settings,
        Func<AiRiskAnalysisSettings, CancellationToken, Task> testConnectionAsync)
    {
        Settings = settings;
        _testConnectionAsync = testConnectionAsync;

        Text = "AI 接口设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 420);
        BackColor = PageChrome.PageBackground;
        Font = new Font("Microsoft YaHei UI", 9F);

        _apiKeyTextBox = CreateInput(settings.ApiKey, "请输入 API Key");
        _apiKeyTextBox.UseSystemPasswordChar = true;
        _baseUrlTextBox = CreateInput(settings.BaseUrl, AiRiskAnalysisSettings.DefaultBaseUrl);
        _modelTextBox = CreateInput(settings.Model, AiRiskAnalysisSettings.DefaultModel);
        _testButton = PageChrome.CreateActionButton("测试连接", PageChrome.AccentGreen, false);
        _testButton.Click += async (_, _) => await TestConnectionAsync();

        _showKeyCheckBox = new CheckBox
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = PageChrome.TextMuted,
            Margin = new Padding(0, 2, 0, 8),
            Text = "显示密钥"
        };
        _showKeyCheckBox.CheckedChanged += (_, _) =>
        {
            _apiKeyTextBox.UseSystemPasswordChar = !_showKeyCheckBox.Checked;
        };

        Controls.Add(BuildContent());
    }

    private Control BuildContent()
    {
        var shell = PageChrome.CreateSurfacePanel(new Padding(22), 16);
        shell.Margin = Padding.Empty;

        var titleLabel = PageChrome.CreateTextLabel(
            "手动配置 AI 接口",
            15F,
            FontStyle.Bold,
            PageChrome.TextPrimary,
            new Padding(0, 0, 0, 6));
        var noteLabel = PageChrome.CreateTextLabel(
            "保存后只写入本机环境变量，不会进入代码仓库。接口地址默认使用 CodeAPI。",
            9F,
            FontStyle.Regular,
            PageChrome.TextMuted,
            new Padding(0, 0, 0, 14));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 10,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var index = 0; index < 9; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(noteLabel, 0, 1);
        layout.Controls.Add(CreateFieldLabel("API Key"), 0, 2);
        layout.Controls.Add(_apiKeyTextBox, 0, 3);
        layout.Controls.Add(_showKeyCheckBox, 0, 4);
        layout.Controls.Add(CreateFieldLabel("接口地址"), 0, 5);
        layout.Controls.Add(_baseUrlTextBox, 0, 6);
        layout.Controls.Add(CreateFieldLabel("模型"), 0, 7);
        layout.Controls.Add(_modelTextBox, 0, 8);
        layout.Controls.Add(BuildActions(), 0, 9);

        shell.Controls.Add(layout);
        return shell;
    }

    private Control BuildActions()
    {
        var saveButton = PageChrome.CreateActionButton("保存设置", PageChrome.AccentBlue, true);
        var cancelButton = PageChrome.CreateActionButton("取消", PageChrome.SurfaceBorder, false);
        _testButton.Margin = new Padding(10, 0, 0, 0);
        cancelButton.Margin = new Padding(10, 0, 0, 0);

        saveButton.Click += (_, _) => Confirm();
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 20, 0, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);
        actions.Controls.Add(_testButton);
        return actions;
    }

    private void Confirm()
    {
        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        Settings = settings;
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task TestConnectionAsync()
    {
        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        var originalText = _testButton.Text;
        _testButton.Enabled = false;
        _testButton.Text = "测试中...";
        UseWaitCursor = true;

        try
        {
            await _testConnectionAsync(settings, CancellationToken.None);
            MessageBox.Show(this, "连接正常，当前 Key 和模型可以使用。", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or UriFormatException)
        {
            MessageBox.Show(this, $"连接失败：{ex.Message}", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _testButton.Text = originalText;
            _testButton.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private bool TryBuildSettings(out AiRiskAnalysisSettings settings)
    {
        settings = AiRiskAnalysisSettings.Empty;
        var apiKey = _apiKeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this, "API Key 不能为空。", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _apiKeyTextBox.Focus();
            return false;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_baseUrlTextBox.Text)
            ? AiRiskAnalysisSettings.DefaultBaseUrl
            : _baseUrlTextBox.Text.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            MessageBox.Show(this, "接口地址格式不对，比如：https://codeapi.icu", "AI 接口设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _baseUrlTextBox.Focus();
            return false;
        }

        var model = string.IsNullOrWhiteSpace(_modelTextBox.Text)
            ? AiRiskAnalysisSettings.DefaultModel
            : _modelTextBox.Text.Trim();

        settings = new AiRiskAnalysisSettings(apiKey, baseUrl, model);
        return true;
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

    private static TextBox CreateInput(string text, string placeholder)
    {
        return new TextBox
        {
            BackColor = PageChrome.InputBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 10F),
            ForeColor = PageChrome.TextPrimary,
            Margin = new Padding(0, 0, 0, 2),
            PlaceholderText = placeholder,
            Text = text
        };
    }
}
