namespace CabinetLogs.Application.Interfaces;

public interface IDatabaseErrorInspector
{
    bool IsUniqueConstraintViolation(Exception exception, string? constraintName = null);
}
