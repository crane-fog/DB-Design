using Oracle.ManagedDataAccess.Client;

using Org.OpenAPITools.Models;

namespace Backend.Services;

public sealed record ProductionResourceResult<T>(int Code, string Message, T? Data)
{
    public bool Ok => Code == 200;

    public static ProductionResourceResult<T> Success(T data, string message = "操作成功") =>
        new(200, message, data);

    public static ProductionResourceResult<T> Fail(int code, string message) =>
        new(code, message, default);
}

public sealed record ProductionResourcePage<T>(
    List<T> Records,
    int Total,
    int Page,
    int PageSize);

public sealed record CapacityEstimateInput(
    long MaterialId,
    long VersionId,
    decimal PlanQty,
    DateOnly ExpectedDate);

public sealed record MaterialReadiness(
    bool Ready,
    DateOnly? ReadyDate,
    string? Reason);

public static class ProductionLineRunStatusMap
{
    public static class Db
    {
        public const string Idle = "空闲";
        public const string Running = "运行";
        public const string Fault = "故障";
    }

    public static ProductionLineRunStatus FromDb(string? status) =>
        status switch
        {
            Db.Running => ProductionLineRunStatus.RunningEnum,
            Db.Fault => ProductionLineRunStatus.FaultEnum,
            _ => ProductionLineRunStatus.IdleEnum,
        };

    public static string? ToDbOrNull(ProductionLineRunStatus status) =>
        status switch
        {
            ProductionLineRunStatus.IdleEnum => Db.Idle,
            ProductionLineRunStatus.RunningEnum => Db.Running,
            ProductionLineRunStatus.FaultEnum => Db.Fault,
            _ => null,
        };
}

public static class FaultStatusMap
{
    public static class Db
    {
        public const string PendingRepair = "待维修";
        public const string Repairing = "维修中";
        public const string Recovered = "已恢复";
    }

    public static FaultStatus FromDb(string? status) =>
        status switch
        {
            Db.Repairing => FaultStatus.RepairingEnum,
            Db.Recovered => FaultStatus.RecoveredEnum,
            _ => FaultStatus.PendingRepairEnum,
        };

    public static string? ToDbOrNull(FaultStatus status) =>
        status switch
        {
            FaultStatus.PendingRepairEnum => Db.PendingRepair,
            FaultStatus.RepairingEnum => Db.Repairing,
            FaultStatus.RecoveredEnum => Db.Recovered,
            _ => null,
        };

    public static bool CanTransition(string current, string requested) =>
        current == requested
        || (current == Db.PendingRepair && requested == Db.Repairing)
        || (current == Db.Repairing && requested == Db.Recovered);
}

internal static class OracleCommandFactory
{
    public static OracleCommand Create(
        OracleConnection connection,
        string commandText,
        OracleTransaction? transaction = null)
    {
        OracleCommand command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = commandText;
        command.Transaction = transaction;
        return command;
    }

    public static object DbValue<T>(T? value) => value is null ? DBNull.Value : value;

    public static long ReadIdentity(OracleParameter parameter) =>
        Convert.ToInt64(parameter.Value.ToString());
}
