using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Infrastructure.Observability;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Redis and Postgres capacity snapshot for the admin dashboard.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/capacity")]
[Authorize]
public class AdminCapacityController : ControllerBase
{
    private readonly IInfrastructureCapacityService _capacity;
    public AdminCapacityController(IInfrastructureCapacityService capacity) => _capacity = capacity;

    [HttpGet]
    [Permission(Permissions.Settings.View)]
    public Task<InfrastructureCapacity> Get(CancellationToken ct) => _capacity.GetAsync(ct);
}
