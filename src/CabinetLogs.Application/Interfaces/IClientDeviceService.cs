using CabinetLogs.Application.Common;
using CabinetLogs.Application.DTOs.ClientDevices;

namespace CabinetLogs.Application.Interfaces;

public interface IClientDeviceService
{
    Task<ClientDeviceDto> CreateAsync(CreateClientDeviceRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<ClientDeviceDto>> GetAllAsync(ClientDeviceQuery query, CancellationToken cancellationToken = default);
    Task<ClientDeviceDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientDeviceDto> UpdateAsync(Guid id, UpdateClientDeviceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
