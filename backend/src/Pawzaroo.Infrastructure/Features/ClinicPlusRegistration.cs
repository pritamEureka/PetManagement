using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.ClinicPlus;

namespace Pawzaroo.Infrastructure.Features;

public static class ClinicPlusRegistration
{
    /// <summary>Emergency discovery, booking policy/deposits, follow-ups, doctor switching, shared calendars,
    /// questionnaires, clinical encounters/referrals/consent and clinic analytics.</summary>
    public static IServiceCollection AddClinicPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<IEmergencyVetService, EmergencyVetService>();
        s.AddScoped<IBookingPolicyService, BookingPolicyService>();
        s.AddScoped<IFollowUpService, FollowUpService>();
        s.AddScoped<IDoctorSwitchService, DoctorSwitchService>();
        s.AddScoped<ICalendarShareService, CalendarShareService>();
        s.AddScoped<IQuestionnaireService, QuestionnaireService>();
        s.AddScoped<IClinicalEncounterService, ClinicalEncounterService>();
        s.AddScoped<IClinicalGate, ClinicalGate>();
        s.AddScoped<IClinicAnalyticsService, ClinicAnalyticsService>();
        return s;
    }
}
