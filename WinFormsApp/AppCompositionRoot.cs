using System.Text.Json;
using App.Core.Interfaces;
using App.Core.Services;
using App.Infrastructure.Config;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
using WinFormsApp.Controllers;
using WinFormsApp.Exports;
using WinFormsApp.Services;
using WinFormsApp.Views;

namespace WinFormsApp;

internal sealed class AppCompositionRoot
{
    private const string DefaultConnectionString =
        "Server=localhost;Database=TestDB;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly IAuthenticationService _authenticationService;
    private readonly IInspectionRecordService _inspectionRecordService;
    private readonly IManagedDeviceService _managedDeviceService;
    private readonly IRiskAnalysisService _riskAnalysisService;
    private readonly IAiRiskAnalysisService _aiRiskAnalysisService;
    private readonly AiAnalysisHistoryStore _aiAnalysisHistoryStore;

    public AppCompositionRoot()
    {
        var sqlOptions = new SqlServerOptions
        {
            ConnectionString = LoadConnectionString()
        };

        var rememberMePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remember.txt");
        var userRepository = new SqlUserRepository(sqlOptions);
        var rememberMeRepository = new FileRememberMeRepository(rememberMePath);
        var inspectionRecordPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "inspection-records.json");
        var inspectionTemplatePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "inspection-templates.json");
        var managedDevicePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "managed-devices.json");
        var aiAnalysisHistoryPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "ai-analysis-history.json");
        var inspectionRecordRepository = new SqlInspectionRecordRepository(sqlOptions);
        var inspectionTemplateRepository = new SqlInspectionTemplateRepository(sqlOptions);
        var managedDeviceRepository = new SqlManagedDeviceRepository(sqlOptions);

        MigrateJsonRecordsIfNeeded(
            inspectionRecordRepository,
            new JsonInspectionRecordRepository(inspectionRecordPath));
        MigrateJsonTemplatesIfNeeded(
            inspectionTemplateRepository,
            new JsonInspectionTemplateRepository(inspectionTemplatePath));
        MigrateJsonDevicesIfNeeded(
            managedDeviceRepository,
            new JsonManagedDeviceRepository(managedDevicePath));

        _authenticationService = new AuthenticationService(userRepository, rememberMeRepository);
        _inspectionRecordService = new InspectionRecordService(
            inspectionRecordRepository,
            inspectionTemplateRepository);
        _managedDeviceService = new ManagedDeviceService(
            managedDeviceRepository,
            inspectionTemplateRepository);
        _riskAnalysisService = new LocalRiskAnalysisService();
        _aiRiskAnalysisService = OpenAiCompatibleRiskAnalysisService.FromEnvironment();
        _aiAnalysisHistoryStore = new AiAnalysisHistoryStore(aiAnalysisHistoryPath);
    }

    public LoginForm CreateLoginForm()
    {
        return new LoginForm(new LoginController(_authenticationService), this);
    }

    public RegisterForm CreateRegisterForm()
    {
        return new RegisterForm(new RegisterController(_authenticationService));
    }

    public MainForm CreateDashboardForm(string account)
    {
        var dashboardController = new DashboardController(_inspectionRecordService, _managedDeviceService);
        var inspectionController = new InspectionController(
            _inspectionRecordService,
            _riskAnalysisService,
            _aiRiskAnalysisService,
            _aiAnalysisHistoryStore,
            new InspectionExcelExporter());
        var deviceManagementController = new DeviceManagementController(_managedDeviceService);
        return new MainForm(
            dashboardController,
            inspectionController,
            deviceManagementController,
            account);
    }

    private static void MigrateJsonRecordsIfNeeded(
        IInspectionRecordRepository targetRepository,
        IInspectionRecordRepository sourceRepository)
    {
        if (targetRepository.GetAll().Count > 0)
        {
            return;
        }

        var legacyRecords = sourceRepository.GetAll();
        if (legacyRecords.Count == 0)
        {
            return;
        }

        targetRepository.SaveAll(legacyRecords);
    }

    private static void MigrateJsonTemplatesIfNeeded(
        IInspectionTemplateRepository targetRepository,
        IInspectionTemplateRepository sourceRepository)
    {
        if (targetRepository.GetAll().Count > 0)
        {
            return;
        }

        var legacyTemplates = sourceRepository.GetAll();
        if (legacyTemplates.Count == 0)
        {
            return;
        }

        targetRepository.SaveAll(legacyTemplates);
    }

    private static void MigrateJsonDevicesIfNeeded(
        IManagedDeviceRepository targetRepository,
        IManagedDeviceRepository sourceRepository)
    {
        if (targetRepository.GetAll().Count > 0)
        {
            return;
        }

        var legacyDevices = sourceRepository.GetAll();
        if (legacyDevices.Count == 0)
        {
            return;
        }

        targetRepository.SaveAll(legacyDevices);
    }

    private static string LoadConnectionString()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            return DefaultConnectionString;
        }

        using var stream = File.OpenRead(configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var connectionString = settings?.SqlServer?.ConnectionString;
        return string.IsNullOrWhiteSpace(connectionString)
            ? DefaultConnectionString
            : connectionString;
    }

    private sealed class AppSettings
    {
        public SqlServerSettings? SqlServer { get; init; } = new();
    }

    private sealed class SqlServerSettings
    {
        public string ConnectionString { get; init; } = string.Empty;
    }
}
