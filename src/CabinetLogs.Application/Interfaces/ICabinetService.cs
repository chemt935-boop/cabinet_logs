using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.Cabinets;
using CabinetLogs.Application.DTOs.Logs;

namespace CabinetLogs.Application.Interfaces;

public interface ICabinetService
{
    Task<PagedResult<CabinetSummaryDto>> GetAllAsync(CabinetQuery query, CancellationToken cancellationToken = default);
    Task<CabinetDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<LogDto>> GetLogsByCabinetIdAsync(Guid cabinetId, LogQuery query, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, bool force = false, CancellationToken cancellationToken = default);
}
