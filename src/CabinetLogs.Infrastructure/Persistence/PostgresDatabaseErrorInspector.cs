using CabinetLogs.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CabinetLogs.Infrastructure.Persistence;

public sealed class PostgresDatabaseErrorInspector : IDatabaseErrorInspector
{
    public bool IsUniqueConstraintViolation(Exception exception, string? constraintName = null)
    {
        var inner = exception is DbUpdateException dbEx ? dbEx.InnerException : exception;

        while (inner is not null)
        {
            if (inner is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return constraintName is null
                    || string.Equals(pgEx.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase);
            }

            // Support SQLite unique constraint error during automated tests
            if (inner.GetType().Name == "SqliteException"
                && inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            inner = inner.InnerException;
        }

        return false;
    }
}
