using Reserva.Domain;

namespace Reserva.Services;

/// <summary>
/// Creates slots and presents their times to a user. Slot times are always stored
/// and compared in UTC; presentation converts to the caller-supplied <paramref name="userZone"/>
/// (injected — never <see cref="TimeZoneInfo.Local"/> or <see cref="DateTime.Now"/>) so behavior
/// is identical no matter what time zone the process itself is running in.
/// </summary>
public class ScheduleService(TimeZoneInfo userZone)
{
    public Slot CreateSlot(SlotId id, DateTimeOffset startUtc, TimeSpan duration, int capacity) =>
        new(id, startUtc.DateTime, startUtc.DateTime + duration, capacity);

    public DateTimeOffset PresentStart(Slot slot) =>
        slot.StartUtc;
}
