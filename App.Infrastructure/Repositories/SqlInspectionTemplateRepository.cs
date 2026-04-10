using App.Core.Interfaces;
using App.Core.Models;
using App.Infrastructure.Config;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Repositories;

public sealed class SqlInspectionTemplateRepository : IInspectionTemplateRepository
{
    private readonly string _connectionString;

    public SqlInspectionTemplateRepository(SqlServerOptions options)
    {
        _connectionString = options.ConnectionString;
        SqlServerSchemaInitializer.EnsureInitialized(_connectionString);
    }

    public IReadOnlyList<InspectionTemplate> GetAll()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        const string sql = """
                           SELECT Id,
                                  LineName,
                                  DeviceName,
                                  InspectionItem,
                                  DefaultInspector,
                                  DefaultRemark
                           FROM dbo.InspectionTemplates
                           """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var templates = new List<InspectionTemplate>();

        while (reader.Read())
        {
            templates.Add(new InspectionTemplate(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return templates;
    }

    public void SaveAll(IReadOnlyList<InspectionTemplate> templates)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = new SqlCommand("DELETE FROM dbo.InspectionTemplates;", connection, transaction))
        {
            deleteCommand.ExecuteNonQuery();
        }

        const string insertSql = """
                                 INSERT INTO dbo.InspectionTemplates
                                 (
                                     Id,
                                     LineName,
                                     DeviceName,
                                     InspectionItem,
                                     DefaultInspector,
                                     DefaultRemark
                                 )
                                 VALUES
                                 (
                                     @Id,
                                     @LineName,
                                     @DeviceName,
                                     @InspectionItem,
                                     @DefaultInspector,
                                     @DefaultRemark
                                 );
                                 """;

        using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier);
        insertCommand.Parameters.Add("@LineName", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@DeviceName", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@InspectionItem", System.Data.SqlDbType.NVarChar, 200);
        insertCommand.Parameters.Add("@DefaultInspector", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@DefaultRemark", System.Data.SqlDbType.NVarChar, -1);

        foreach (var template in templates)
        {
            insertCommand.Parameters["@Id"].Value = template.Id;
            insertCommand.Parameters["@LineName"].Value = template.LineName;
            insertCommand.Parameters["@DeviceName"].Value = template.DeviceName;
            insertCommand.Parameters["@InspectionItem"].Value = template.InspectionItem;
            insertCommand.Parameters["@DefaultInspector"].Value = template.DefaultInspector;
            insertCommand.Parameters["@DefaultRemark"].Value = template.DefaultRemark;
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
