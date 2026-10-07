// Authoritative grading suite for BOOK-808 — slots are stored and compared in
// UTC and presented in the user's own zone. The zone is INJECTED: the sandbox
// runs UTC, where a TimeZoneInfo.Local bug is invisible.
using Xunit;

public class TimezoneGradingTest
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Fact]
    public void SlotIsStoredAsTheSameInstantRegardlessOfPresentationZone()
    {
        var startUtc = DateTimeOffset.Parse("2026-03-01T15:00:00Z");
        var slot = new ScheduleService(NewYork).CreateSlot(new SlotId("s"), startUtc, TimeSpan.FromHours(1), 1);

        Assert.Equal(TimeSpan.Zero, slot.StartUtc.Offset);
        Assert.Equal(startUtc.UtcDateTime, slot.StartUtc.UtcDateTime);
    }

    [Fact]
    public void SlotIsPresentedInTheUsersZone()
    {
        var startUtc = DateTimeOffset.Parse("2026-03-01T15:00:00Z");   // 10:00 in New York, EST
        var svc = new ScheduleService(NewYork);
        var slot = svc.CreateSlot(new SlotId("s"), startUtc, TimeSpan.FromHours(1), 1);

        var shown = svc.PresentStart(slot);

        Assert.Equal(10, shown.Hour);
        Assert.Equal(TimeSpan.FromHours(-5), shown.Offset);
    }

    // A fixed historical date, never "the next transition": a tzdata update must
    // not be able to move what this test asserts.
    [Fact]
    public void DstTransitionDoesNotMoveAnExistingSlotsInstant()
    {
        var beforeDst = DateTimeOffset.Parse("2026-03-08T06:30:00Z");  // 01:30 EST
        var svc = new ScheduleService(NewYork);
        var slot = svc.CreateSlot(new SlotId("s"), beforeDst, TimeSpan.FromHours(1), 1);

        Assert.Equal(beforeDst.UtcDateTime, slot.StartUtc.UtcDateTime);
        Assert.Equal(beforeDst.UtcDateTime.AddHours(1), slot.EndUtc.UtcDateTime);
    }
}
