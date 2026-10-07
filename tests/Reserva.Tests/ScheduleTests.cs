using Reserva.Domain;
using Reserva.Services;

namespace Reserva.Tests;

public class ScheduleTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Fact]
    public void Slot_start_presents_as_the_matching_wall_clock_time_in_the_user_zone()
    {
        var service = new ScheduleService(NewYork);

        // 2026-01-15T15:00:00Z is winter, so New York is UTC-5 (EST, no DST): 10:00 local.
        var startUtc = new DateTimeOffset(2026, 1, 15, 15, 0, 0, TimeSpan.Zero);
        var slot = service.CreateSlot(new SlotId("slot-1"), startUtc, TimeSpan.FromHours(1), capacity: 1);

        var presented = service.PresentStart(slot);

        Assert.Equal(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.FromHours(-5)), presented);
        Assert.Equal(startUtc, presented); // same instant, different offset
    }

    [Fact]
    public void Slot_time_is_stable_across_a_dst_transition()
    {
        var service = new ScheduleService(NewYork);

        // US spring-forward in 2026 is 2026-03-08. Book a slot for the day after,
        // once New York has moved to EDT (UTC-4), and confirm the presented wall-clock
        // time still matches what a user in New York would expect — the underlying
        // instant is unaffected by the transition because everything is stored in UTC.
        var startUtc = new DateTimeOffset(2026, 3, 9, 14, 0, 0, TimeSpan.Zero);
        var slot = service.CreateSlot(new SlotId("slot-2"), startUtc, TimeSpan.FromHours(1), capacity: 1);

        var presented = service.PresentStart(slot);

        Assert.Equal(new DateTimeOffset(2026, 3, 9, 10, 0, 0, TimeSpan.FromHours(-4)), presented);
        Assert.Equal(startUtc, presented);
        Assert.Equal(startUtc, slot.StartUtc); // storage stays UTC regardless of DST
    }
}
