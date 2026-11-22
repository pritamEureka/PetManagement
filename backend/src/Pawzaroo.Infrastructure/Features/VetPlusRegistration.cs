using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.VetPlus;

namespace Pawzaroo.Infrastructure.Features;

public static class VetPlusRegistration
{
    public static IServiceCollection AddVetPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<ITelemedicineService, TelemedicineService>();
        s.AddScoped<WaitlistService>();
        s.AddScoped<IWaitlistService>(sp => sp.GetRequiredService<WaitlistService>());
        s.AddScoped<IWaitlistNotifier>(sp => sp.GetRequiredService<WaitlistService>());
        s.AddScoped<IRecurringBookingService, RecurringBookingService>();
        s.AddScoped<ICalendarExportService, CalendarExportService>();
        return s;
    }
}
