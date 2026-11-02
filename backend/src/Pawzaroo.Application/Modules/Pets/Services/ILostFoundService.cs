using Pawzaroo.Application.Modules.Pets.Dtos;

namespace Pawzaroo.Application.Modules.Pets.Services;

public interface ILostFoundService
{
    Task<PagedLostFound> SearchAsync(LostFoundQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LostFoundSummaryDto>> MineAsync(CancellationToken ct = default);
    Task<LostFoundDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<LostFoundDetailDto> CreateAsync(CreateLostFoundInput input, CancellationToken ct = default);
    Task ResolveAsync(Guid id, ResolveLostFoundInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Opposite-kind open reports that look like the same animal (species, city, colour, breed, timing).</summary>
    Task<IReadOnlyList<LostFoundMatchDto>> MatchesAsync(Guid id, CancellationToken ct = default);
}
