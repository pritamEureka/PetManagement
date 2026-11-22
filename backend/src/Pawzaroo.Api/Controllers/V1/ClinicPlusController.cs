using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Modules.ClinicPlus;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Clinic-plus endpoints: emergency discovery, booking policy/deposits, follow-ups, doctor switching, shared calendars,
/// questionnaires, clinical encounters/referrals/consent and clinic analytics. Authorization is enforced in the services
/// (treating doctor / patient / pet owner checks); no new RBAC permissions are introduced.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public class ClinicPlusController : ControllerBase
{
    private readonly IEmergencyVetService _emergency;
    private readonly IBookingPolicyService _policy;
    private readonly IFollowUpService _followUps;
    private readonly IDoctorSwitchService _switch;
    private readonly ICalendarShareService _shares;
    private readonly IQuestionnaireService _questionnaires;
    private readonly IClinicalEncounterService _clinical;
    private readonly IClinicAnalyticsService _analytics;

    public ClinicPlusController(IEmergencyVetService emergency, IBookingPolicyService policy, IFollowUpService followUps,
        IDoctorSwitchService @switch, ICalendarShareService shares, IQuestionnaireService questionnaires,
        IClinicalEncounterService clinical, IClinicAnalyticsService analytics)
    {
        _emergency = emergency; _policy = policy; _followUps = followUps; _switch = @switch; _shares = shares;
        _questionnaires = questionnaires; _clinical = clinical; _analytics = analytics;
    }

    // ---------- Emergency ----------

    [HttpGet("vets/me/emergency-profile")]
    public async Task<ActionResult<EmergencyProfileDto>> MyEmergencyProfile(CancellationToken ct)
    {
        var p = await _emergency.GetMineAsync(ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPut("vets/me/emergency-profile")]
    public Task<EmergencyProfileDto> UpsertEmergencyProfile([FromBody] UpsertEmergencyProfileInput input, CancellationToken ct)
        => _emergency.UpsertMineAsync(input, ct);

    [HttpGet("vets/emergency")]
    public Task<IReadOnlyList<EmergencyVetDto>> Emergency([FromQuery] double? lat, [FromQuery] double? lng,
        [FromQuery] string? city, [FromQuery] AnimalType? animalType, CancellationToken ct)
        => _emergency.SearchAsync(lat, lng, city, animalType, 30, ct);

    // ---------- Booking policy / deposits ----------

    [HttpGet("vets/me/booking-policy")]
    public async Task<ActionResult<BookingPolicyDto>> MyPolicy(CancellationToken ct)
    {
        var p = await _policy.GetMineAsync(ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPut("vets/me/booking-policy")]
    public Task<BookingPolicyDto> UpsertPolicy([FromBody] BookingPolicyDto input, CancellationToken ct) => _policy.UpsertMineAsync(input, ct);

    [HttpGet("vets/{doctorId:guid}/booking-policy")]
    public async Task<ActionResult<BookingPolicyDto>> DoctorPolicy(Guid doctorId, CancellationToken ct)
    {
        var p = await _policy.GetForDoctorAsync(doctorId, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpGet("appointments/{id:guid}/deposit")]
    public async Task<ActionResult<DepositDto>> Deposit(Guid id, CancellationToken ct)
    {
        var d = await _policy.GetDepositAsync(id, ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("appointments/{id:guid}/cancellation-quote")]
    public Task<CancellationQuoteDto> CancellationQuote(Guid id, CancellationToken ct) => _policy.QuoteCancellationAsync(id, ct);

    [HttpGet("appointments/{id:guid}/cancellation-outcome")]
    public async Task<ActionResult<CancellationOutcomeDto>> CancellationOutcome(Guid id, CancellationToken ct)
    {
        var o = await _policy.GetCancellationOutcomeAsync(id, ct);
        return o is null ? NotFound() : Ok(o);
    }

    // ---------- Follow-ups ----------

    [HttpPost("appointments/{id:guid}/follow-ups/request")]
    public Task<FollowUpDto> RequestFollowUp(Guid id, [FromBody] RequestFollowUpInput input, CancellationToken ct) => _followUps.RequestAsync(id, input, ct);

    [HttpGet("appointments/{id:guid}/follow-ups/prefill")]
    public Task<FollowUpDto> PrefillFollowUp(Guid id, CancellationToken ct) => _followUps.PrefillAsync(id, ct);

    [HttpPost("appointments/{id:guid}/follow-ups/book")]
    [Idempotent]
    [EnableRateLimiting("writes")]
    public Task<AppointmentSummaryDto> BookFollowUp(Guid id, [FromBody] BookFollowUpInput input, CancellationToken ct) => _followUps.BookAsync(id, input, ct);

    [HttpGet("follow-ups/mine")]
    public Task<IReadOnlyList<FollowUpDto>> MyFollowUps(CancellationToken ct) => _followUps.MineAsync(ct);

    [HttpPost("follow-ups/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissFollowUp(Guid id, CancellationToken ct)
    {
        await _followUps.DismissAsync(id, ct);
        return NoContent();
    }

    // ---------- Doctor switching ----------

    [HttpGet("appointments/{id:guid}/switch-candidates")]
    public Task<IReadOnlyList<SwitchCandidateDto>> SwitchCandidates(Guid id, CancellationToken ct) => _switch.CandidatesAsync(id, ct);

    [HttpPost("appointments/{id:guid}/switch-doctor")]
    [Idempotent]
    [EnableRateLimiting("writes")]
    public Task<AppointmentSummaryDto> SwitchDoctor(Guid id, [FromBody] SwitchDoctorInput input, CancellationToken ct) => _switch.SwitchAsync(id, input, ct);

    // ---------- Shared calendars ----------

    [HttpGet("calendar/shares")]
    public Task<IReadOnlyList<CalendarShareDto>> Shares(CancellationToken ct) => _shares.ListAsync(ct);

    [HttpPost("calendar/shares")]
    public Task<CalendarShareDto> CreateShare([FromBody] CreateCalendarShareInput input, CancellationToken ct) => _shares.CreateAsync(input, ct);

    [HttpDelete("calendar/shares/{id:guid}")]
    public async Task<IActionResult> RevokeShare(Guid id, CancellationToken ct)
    {
        await _shares.RevokeAsync(id, ct);
        return NoContent();
    }

    /// <summary>Anonymous, token-gated, read-only feed. Revoked/expired/unknown tokens look identical (404).</summary>
    [HttpGet("calendar/shared/{token}.ics")]
    [AllowAnonymous]
    [Produces("text/calendar")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> SharedFeed(string token, CancellationToken ct)
    {
        var ics = await _shares.FeedAsync(token, ct);
        if (ics is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=300";
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8");
    }

    // ---------- Questionnaires ----------

    [HttpGet("vets/me/questionnaire")]
    public async Task<ActionResult<QuestionnaireTemplateDto>> MyQuestionnaire(CancellationToken ct)
    {
        var t = await _questionnaires.GetMyTemplateAsync(ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPut("vets/me/questionnaire")]
    public Task<QuestionnaireTemplateDto> UpsertQuestionnaire([FromBody] UpsertQuestionnaireInput input, CancellationToken ct)
        => _questionnaires.UpsertMyTemplateAsync(input, ct);

    [HttpGet("appointments/{id:guid}/questionnaire")]
    public Task<AppointmentQuestionnaireDto> AppointmentQuestionnaire(Guid id, CancellationToken ct) => _questionnaires.GetForAppointmentAsync(id, ct);

    [HttpPost("appointments/{id:guid}/questionnaire")]
    public Task<AppointmentQuestionnaireDto> SubmitQuestionnaire(Guid id, [FromBody] SubmitQuestionnaireInput input, CancellationToken ct)
        => _questionnaires.SubmitAsync(id, input, ct);

    // ---------- Clinical encounter ----------

    [HttpGet("appointments/{id:guid}/encounter")]
    public Task<EncounterDto> Encounter(Guid id, CancellationToken ct) => _clinical.GetAsync(id, ct);

    [HttpPut("appointments/{id:guid}/encounter")]
    public Task<EncounterDto> SaveEncounter(Guid id, [FromBody] SaveEncounterDraftInput input, CancellationToken ct) => _clinical.SaveDraftAsync(id, input, ct);

    [HttpPost("appointments/{id:guid}/encounter/finalize")]
    public Task<EncounterDto> FinalizeEncounter(Guid id, CancellationToken ct) => _clinical.FinalizeAsync(id, ct);

    [HttpPost("appointments/{id:guid}/encounter/amend")]
    public Task<EncounterDto> AmendEncounter(Guid id, [FromBody] AmendEncounterInput input, CancellationToken ct) => _clinical.AmendAsync(id, input, ct);

    [HttpPost("appointments/{id:guid}/encounter/referrals")]
    public Task<ReferralDto> CreateReferral(Guid id, [FromBody] CreateReferralInput input, CancellationToken ct) => _clinical.CreateReferralAsync(id, input, ct);

    [HttpGet("referrals/incoming")]
    public Task<IReadOnlyList<ReferralDto>> IncomingReferrals(CancellationToken ct) => _clinical.IncomingReferralsAsync(ct);

    [HttpPost("referrals/{id:guid}/respond")]
    public Task<ReferralDto> RespondReferral(Guid id, [FromBody] RespondReferralInput input, CancellationToken ct) => _clinical.RespondReferralAsync(id, input, ct);

    [HttpPost("record-consents")]
    public Task<RecordConsentDto> GrantConsent([FromBody] GrantRecordConsentInput input, CancellationToken ct) => _clinical.GrantConsentAsync(input, ct);

    [HttpGet("record-consents")]
    public Task<IReadOnlyList<RecordConsentDto>> Consents([FromQuery] Guid petId, CancellationToken ct) => _clinical.ListConsentsAsync(petId, ct);

    [HttpDelete("record-consents/{id:guid}")]
    public async Task<IActionResult> RevokeConsent(Guid id, CancellationToken ct)
    {
        await _clinical.RevokeConsentAsync(id, ct);
        return NoContent();
    }

    // ---------- Analytics ----------

    [HttpGet("vets/me/analytics")]
    public Task<ClinicAnalyticsDto> Analytics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? granularity, CancellationToken ct)
        => _analytics.MineAsync(from, to, granularity, ct);
}
