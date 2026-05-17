using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Shared.Constants;

namespace Pawzaroo.Application.Modules.Identity;

public class IdentityModule : IModule
{
    public string Name => ModuleNames.Identity;
    public IServiceCollection Register(IServiceCollection services) => services;
    // Handlers (Login, Register, Refresh, Logout) live under Features/Auth/
}
