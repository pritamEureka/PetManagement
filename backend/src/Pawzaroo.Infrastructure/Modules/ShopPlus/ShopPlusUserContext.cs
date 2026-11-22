using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

/// <summary>
/// Scoped holder letting background code act as a specific user inside one DI scope, so the
/// public IOrderService/ICartService (which read ICurrentUserService) can be reused from the worker.
/// Only honoured when AddShopPlusFeatures() ran AFTER the ICurrentUserService registration (it decorates it).
/// </summary>
public sealed class ShopPlusUserContext
{
    public Guid? OverrideUserId { get; set; }
}

/// <summary>Decorator: returns the override user (no permissions) when set, else delegates to the real service.</summary>
public sealed class OverridableCurrentUserService : ICurrentUserService
{
    private readonly ICurrentUserService _inner;
    private readonly ShopPlusUserContext _ctx;

    public OverridableCurrentUserService(ICurrentUserService inner, ShopPlusUserContext ctx)
    {
        _inner = inner;
        _ctx = ctx;
    }

    public Guid? UserId => _ctx.OverrideUserId ?? _inner.UserId;
    public string? Email => _ctx.OverrideUserId is null ? _inner.Email : null;
    public IReadOnlyCollection<string> Permissions => _ctx.OverrideUserId is null ? _inner.Permissions : Array.Empty<string>();
    public bool IsAuthenticated => _ctx.OverrideUserId is not null || _inner.IsAuthenticated;
}
