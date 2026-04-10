using App.Core.Interfaces;
using App.Core.Services;
using App.Infrastructure.Config;
using App.Infrastructure.Repositories;
using WinFormsApp.Controllers;
using WinFormsApp.Exports;
using WinFormsApp.Views;

namespace WinFormsApp;

internal sealed class AppCompositionRoot
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IInspectionRecordService _inspectionRecordService;

    public AppCompositionRoot()
    {
        var sqlOptions = new SqlServerOptions
        {
            ConnectionString = "Server=localhost;Database=TestDB;Trusted_Connection=True;TrustServerCertificate=True;"
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
        var inspectionRecordRepository = new SqlInspectionRecordRepository(sqlOptions);
        var inspectionTemplateRepository = new SqlInspectionTemplateRepository(sqlOptions);

        MigrateJsonRecordsIfNeeded(
            inspectionRecordRepository,
            new JsonInspectionRecordRepository(inspectionRecordPath));
        MigrateJsonTemplatesIfNeeded(
            inspectionTemplateRepository,
            new JsonInspectionTemplateRepository(inspectionTemplatePath));

        _authenticationService = new AuthenticationService(userRepository, rememberMeRepository);
        _inspectionRecordService = new InspectionRecordService(
            inspectionRecordRepository,
            inspectionTemplateRepository);
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
        var dashboardController = new DashboardController(_inspectionRecordService);
        var inspectionController = new InspectionController(
            _inspectionRecordService,
            new InspectionExcelExporter());
        return new MainForm(
            dashboardController,
            inspectionController,
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
}
