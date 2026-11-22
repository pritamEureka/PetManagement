using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Application.Modules.VetPlus;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Books a series of appointments by repeating the first slot's start time at the chosen
/// cadence. Each occurrence goes through the normal <see cref="IAppointmentService.BookAsync"/>
/// path (slot lock, payment status, notifications); misses are reported, not fatal.
/// </summary>
public class RecurringBookingService : IRecurringBookingService
{
    private const int MaxOccurrences = 12;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAppointmentService _appointments;

    public RecurringBookingService(ApplicationDbContext db, ICurrentUserService current, IAppointmentService appointments)
    {
        _db = db;
        _current = current;
        _appointments = appointments;
    }

    public async Task<BookSeriesResult> BookSeriesAsync(BookSeriesInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (input.Occurrences < 2 || input.Occurrences > MaxOccurrences)
            throw new AppException("invalid_occurrences", $"Occurrences must be between 2 and {MaxOccurrences}.");

        var first = await _db.DoctorTimeSlots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == input.TimeSlotId && s.DoctorId == input.DoctorId, ct)
            ?? throw new NotFoundException("Slot", input.TimeSlotId);

        var booked = new List<SeriesBookedItem>();
        var failed = new List<SeriesFailedItem>();

        for (var i = 0; i < input.Occurrences; i++)
        {
            var target = NextStart(first.StartUtc, input.Frequency, i);
            Guid? slotId = i == 0 ? first.Id : null;
            if (slotId is null)
            {
                slotId = await _db.DoctorTimeSlots.AsNoTracking()
                    .Where(s => s.DoctorId == input.DoctorId && s.StartUtc == target && s.Status == SlotStatus.Available)
                    .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
                if (slotId is null)
                {
                    failed.Add(new SeriesFailedItem(i + 1, target, "No available slot at that time."));
                    continue;
                }
            }

            try
            {
                var appt = await _appointments.BookAsync(
                    new BookAppointmentInput(input.DoctorId, slotId.Value, input.PetId, input.Type, input.Symptoms), ct);
                booked.Add(new SeriesBookedItem(i + 1, appt.Id, appt.ScheduledAt));
            }
            catch (AppException ex)
            {
                failed.Add(new SeriesFailedItem(i + 1, target, ex.Message));
            }
        }

        Guid? seriesId = null;
        if (booked.Count > 0)
        {
            var series = new AppointmentSeries
            {
                PatientUserId = uid,
                DoctorId = input.DoctorId,
                Frequency = input.Frequency,
                Occurrences = input.Occurrences
            };
            foreach (var b in booked)
                series.Items.Add(new AppointmentSeriesItem { AppointmentId = b.AppointmentId, Sequence = b.Sequence });
            _db.Set<AppointmentSeries>().Add(series);
            await _db.SaveChangesAsync(ct);
            seriesId = series.Id;
        }
        return new BookSeriesResult(seriesId, booked, failed);
    }

    private static DateTime NextStart(DateTime start, RecurrenceFrequency f, int n) => f switch
    {
        RecurrenceFrequency.Weekly => start.AddDays(7 * n),
        RecurrenceFrequency.Biweekly => start.AddDays(14 * n),
        _ => start.AddMonths(n)
    };
}
