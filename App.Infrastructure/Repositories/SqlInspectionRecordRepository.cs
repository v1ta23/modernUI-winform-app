using App.Core.Interfaces;
using App.Core.Models;
using App.Infrastructure.Config;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Repositories;

public sealed class SqlInspectionRecordRepository : IInspectionRecordRepository
{
    private readonly string _connectionString;

    public SqlInspectionRecordRepository(SqlServerOptions options)
    {
        _connectionString = options.ConnectionString;
        SqlServerSchemaInitializer.EnsureInitialized(_connectionString);
    }

    public IReadOnlyList<InspectionRecord> GetAll()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        const string sql = """
                           SELECT Id,
                                  LineName,
                                  DeviceName,
                                  InspectionItem,
                                  Inspector,
                                  Status,
                                  MeasuredValue,
                                  CheckedAt,
                                  Remark,
                                  ClosedAt,
                                  ClosedBy,
                                  ClosureRemark,
                                  IsRevoked,
                                  RevokedAt,
                                  RevokedBy,
                                  RevokeReason
                           FROM dbo.InspectionRecords
                           """;

        using var command = new SqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var records = new List<InspectionRecord>();

        while (reader.Read())
        {
            records.Add(new InspectionRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                (InspectionStatus)reader.GetInt32(5),
                reader.GetDecimal(6),
                reader.GetDateTime(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetBoolean(12),
                reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return records;
    }

    public void SaveAll(IReadOnlyList<InspectionRecord> records)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = new SqlCommand("DELETE FROM dbo.InspectionRecords;", connection, transaction))
        {
            deleteCommand.ExecuteNonQuery();
        }

        const string insertSql = """
                                 INSERT INTO dbo.InspectionRecords
                                 (
                                     Id,
                                     LineName,
                                     DeviceName,
                                     InspectionItem,
                                     Inspector,
                                     Status,
                                     MeasuredValue,
                                     CheckedAt,
                                     Remark,
                                     ClosedAt,
                                     ClosedBy,
                                     ClosureRemark,
                                     IsRevoked,
                                     RevokedAt,
                                     RevokedBy,
                                     RevokeReason
                                 )
                                 VALUES
                                 (
                                     @Id,
                                     @LineName,
                                     @DeviceName,
                                     @InspectionItem,
                                     @Inspector,
                                     @Status,
                                     @MeasuredValue,
                                     @CheckedAt,
                                     @Remark,
                                     @ClosedAt,
                                     @ClosedBy,
                                     @ClosureRemark,
                                     @IsRevoked,
                                     @RevokedAt,
                                     @RevokedBy,
                                     @RevokeReason
                                 );
                                 """;

        using var insertCommand = new SqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier);
        insertCommand.Parameters.Add("@LineName", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@DeviceName", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@InspectionItem", System.Data.SqlDbType.NVarChar, 200);
        insertCommand.Parameters.Add("@Inspector", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@Status", System.Data.SqlDbType.Int);
        insertCommand.Parameters.Add("@MeasuredValue", System.Data.SqlDbType.Decimal).Precision = 18;
        insertCommand.Parameters["@MeasuredValue"].Scale = 2;
        insertCommand.Parameters.Add("@CheckedAt", System.Data.SqlDbType.DateTime2);
        insertCommand.Parameters.Add("@Remark", System.Data.SqlDbType.NVarChar, -1);
        insertCommand.Parameters.Add("@ClosedAt", System.Data.SqlDbType.DateTime2);
        insertCommand.Parameters.Add("@ClosedBy", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@ClosureRemark", System.Data.SqlDbType.NVarChar, -1);
        insertCommand.Parameters.Add("@IsRevoked", System.Data.SqlDbType.Bit);
        insertCommand.Parameters.Add("@RevokedAt", System.Data.SqlDbType.DateTime2);
        insertCommand.Parameters.Add("@RevokedBy", System.Data.SqlDbType.NVarChar, 100);
        insertCommand.Parameters.Add("@RevokeReason", System.Data.SqlDbType.NVarChar, -1);

        foreach (var record in records)
        {
            insertCommand.Parameters["@Id"].Value = record.Id;
            insertCommand.Parameters["@LineName"].Value = record.LineName;
            insertCommand.Parameters["@DeviceName"].Value = record.DeviceName;
            insertCommand.Parameters["@InspectionItem"].Value = record.InspectionItem;
            insertCommand.Parameters["@Inspector"].Value = record.Inspector;
            insertCommand.Parameters["@Status"].Value = (int)record.Status;
            insertCommand.Parameters["@MeasuredValue"].Value = record.MeasuredValue;
            insertCommand.Parameters["@CheckedAt"].Value = record.CheckedAt;
            insertCommand.Parameters["@Remark"].Value = record.Remark;
            insertCommand.Parameters["@ClosedAt"].Value = record.ClosedAt.HasValue
                ? record.ClosedAt.Value
                : DBNull.Value;
            insertCommand.Parameters["@ClosedBy"].Value = (object?)record.ClosedBy ?? DBNull.Value;
            insertCommand.Parameters["@ClosureRemark"].Value = (object?)record.ClosureRemark ?? DBNull.Value;
            insertCommand.Parameters["@IsRevoked"].Value = record.IsRevoked;
            insertCommand.Parameters["@RevokedAt"].Value = record.RevokedAt.HasValue
                ? record.RevokedAt.Value
                : DBNull.Value;
            insertCommand.Parameters["@RevokedBy"].Value = (object?)record.RevokedBy ?? DBNull.Value;
            insertCommand.Parameters["@RevokeReason"].Value = (object?)record.RevokeReason ?? DBNull.Value;
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
