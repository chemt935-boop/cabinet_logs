using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.Logs;

namespace CabinetLogs.Application.Interfaces;

public interface ILogService
{
    Task<LogDto> CreateAsync(
        CreateLogRequest request,
        IReadOnlyList<PhotoUploadFile>? photos,
        CancellationToken cancellationToken = default);

    Task<PagedResult<LogDto>> GetAllAsync(LogQuery query, CancellationToken cancellationToken = default);
    Task<LogDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<LogDto> UpdateAsync(
        Guid id,
        UpdateLogRequest request,
        IReadOnlyList<PhotoUploadFile>? newPhotos,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PhotoUrlResponse> GetPhotoUrlAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogPhotoUrlDto>> GetPhotoUrlsAsync(Guid id, CancellationToken cancellationToken = default);
}
