using Microsoft.Extensions.DependencyInjection;

namespace Pawzaroo.Application.Modules;

/// <summary>
/// Each module exposes a single registration entry point. Application.AddApplication()
/// composes them so adding a module is one line in one file.
/// </summary>
public interface IModule
{
    string Name { get; }
    IServiceCollection Register(IServiceCollection services);
}
