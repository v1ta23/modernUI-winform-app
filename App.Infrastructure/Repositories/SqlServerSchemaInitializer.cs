using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Repositories;

internal static class SqlServerSchemaInitializer
{
    private static readonly ConcurrentDictionary<string, byte> InitializedConnections = new(StringComparer.OrdinalIgnoreCase);

    public static void EnsureInitialized(string connectionString)
    {
        if (!InitializedConnections.TryAdd(connectionString, 0))
        {
            return;
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        const string sql = """
                           IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.Users
                               (
                                   account NVARCHAR(50) NOT NULL PRIMARY KEY,
                                   password NVARCHAR(100) NOT NULL
                               );
                           END;

                           IF OBJECT_ID(N'dbo.InspectionRecords', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.InspectionRecords
                               (
                                   Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                                   LineName NVARCHAR(100) NOT NULL,
                                   DeviceName NVARCHAR(100) NOT NULL,
                                   InspectionItem NVARCHAR(200) NOT NULL,
                                   Inspector NVARCHAR(100) NOT NULL,
                                   Status INT NOT NULL,
                                   MeasuredValue DECIMAL(18, 2) NOT NULL,
                                   CheckedAt DATETIME2 NOT NULL,
                                   Remark NVARCHAR(MAX) NOT NULL,
                                   ClosedAt DATETIME2 NULL,
                                   ClosedBy NVARCHAR(100) NULL,
                                   ClosureRemark NVARCHAR(MAX) NULL,
                                   IsRevoked BIT NOT NULL,
                                   RevokedAt DATETIME2 NULL,
                                   RevokedBy NVARCHAR(100) NULL,
                                   RevokeReason NVARCHAR(MAX) NULL
                               );
                           END;

                           IF OBJECT_ID(N'dbo.InspectionTemplates', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.InspectionTemplates
                               (
                                   Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                                   LineName NVARCHAR(100) NOT NULL,
                                   DeviceName NVARCHAR(100) NOT NULL,
                                   InspectionItem NVARCHAR(200) NOT NULL,
                                   DefaultInspector NVARCHAR(100) NOT NULL,
                                   DefaultRemark NVARCHAR(MAX) NOT NULL
                               );
                           END;
                           """;

        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
