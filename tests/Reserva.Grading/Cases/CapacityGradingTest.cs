// Authoritative grading suite for BOOK-815 — a capacity-limited slot never
// admits more than `capacity` active bookings, even under a concurrent burst.
// Overlaid by the grader; edits in the student's repo are ignored.
//
// This suite drives InMemoryBookingStore.TryReserve DIRECTLY rather than going
// through BookingService.Book. That is deliberate, and it is what keeps this
// ticket independent of BOOK-801: every seeded defect is planted at once, so a
// suite that routed through Book() would fail here whenever BOOK-801's
// check-then-insert defect were still present — i.e. a student who correctly
// fixed BOOK-815 alone would be graded red for someone else's bug. TryReserve
// is the system's ONLY capacity authority (see IBookingStore), so exercising it
// directly grades exactly this ticket and nothing else.
using Xunit;

public class CapacityGradingTest
{
    private static readonly SlotId Slot = new("slot-capacity-2");
    private const int Capacity = 2;

    private static Booking For(string customerId) =>
        new(Guid.NewGuid().ToString(), Slot, customerId, true);

    [Fact]
    public async Task TryReserveAdmitsExactlyCapacityAndRejectsTheNext()
    {
        var store = new InMemoryBookingStore();

        Assert.True(await store.TryReserve(For("alice"), Capacity));
        Assert.True(await store.TryReserve(For("bob"), Capacity));

        Assert.False(await store.TryReserve(For("carol"), Capacity));
        Assert.Equal(2, await store.ActiveCount(Slot));
    }

    [Fact]
    public async Task ConcurrentBurstNeverAdmitsMoreThanCapacity()
    {
        // An admission check that is off by one, or one that counts and claims
        // outside a single critical section, lets more than `capacity` of these
        // through. A correctly-capped atomic TryReserve admits exactly two, no
        // matter how the callers interleave.
        var store = new InMemoryBookingStore();
        const int attempts = 20;
        using var gate = new Barrier(attempts);

        var admitted = await Task.WhenAll(Enumerable.Range(0, attempts).Select(i => Task.Run(async () =>
        {
            gate.SignalAndWait(); // line everyone up so they all fire together
            return await store.TryReserve(For($"customer-{i}"), Capacity);
        })));

        Assert.Equal(2, admitted.Count(ok => ok));
        Assert.Equal(2, await store.ActiveCount(Slot));
    }

    [Fact]
    public async Task ReleasingFreesCapacityForASubsequentReservation()
    {
        var store = new InMemoryBookingStore();
        var first = For("alice");

        Assert.True(await store.TryReserve(first, Capacity));
        Assert.True(await store.TryReserve(For("bob"), Capacity));
        Assert.False(await store.TryReserve(For("carol"), Capacity));

        Assert.True(await store.Release(first.BookingId));

        Assert.True(await store.TryReserve(For("dave"), Capacity));
        Assert.Equal(2, await store.ActiveCount(Slot));
    }
}
