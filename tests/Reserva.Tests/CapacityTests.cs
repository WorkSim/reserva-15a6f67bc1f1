using Reserva.Domain;
using Reserva.Services;
using Reserva.Store;

namespace Reserva.Tests;

public class CapacityTests
{
    [Fact]
    public async Task Capacity_two_slot_accepts_exactly_two_bookings_and_rejects_the_third()
    {
        var service = new BookingService(new InMemoryBookingStore());
        var slot = new Slot(new SlotId("slot-1"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), Capacity: 2);

        var first = await service.Book(slot, "alice");
        var second = await service.Book(slot, "bob");
        var third = await service.Book(slot, "carol");

        Assert.True(first.Ok);
        Assert.True(second.Ok);

        Assert.False(third.Ok);
        Assert.Equal(409, third.StatusCode);
        Assert.Null(third.BookingId);
    }

    [Fact]
    public async Task Cancelling_frees_capacity_for_a_subsequent_booking()
    {
        var service = new BookingService(new InMemoryBookingStore());
        var slot = new Slot(new SlotId("slot-1"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), Capacity: 2);

        var first = await service.Book(slot, "alice");
        var second = await service.Book(slot, "bob");
        Assert.True(first.Ok);
        Assert.True(second.Ok);

        var rejected = await service.Book(slot, "carol");
        Assert.False(rejected.Ok);

        Assert.True(await service.Cancel(first.BookingId!));

        var afterCancel = await service.Book(slot, "dave");
        Assert.True(afterCancel.Ok);
    }

    [Fact]
    public async Task Concurrent_burst_against_a_capacity_two_slot_admits_exactly_two()
    {
        // A non-atomic "count active, then insert" implementation lets this burst
        // race past capacity: every caller can observe room before any of them
        // finishes inserting. A genuinely atomic TryReserve cannot — this is the
        // test that would have failed against the earlier design, where the count
        // check and the claim were two separate, unsynchronized store calls.
        var store = new InMemoryBookingStore();
        var service = new BookingService(store);
        var slot = new Slot(new SlotId("slot-1"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), Capacity: 2);

        const int attempts = 20;
        using var gate = new Barrier(attempts);

        var results = await Task.WhenAll(Enumerable.Range(0, attempts).Select(i => Task.Run(async () =>
        {
            gate.SignalAndWait(); // line everyone up so they all fire together
            return await service.Book(slot, $"customer-{i}");
        })));

        Assert.Equal(2, results.Count(r => r.Ok));
        Assert.Equal(attempts - 2, results.Count(r => !r.Ok && r.StatusCode == 409));
        Assert.Equal(2, await store.ActiveCount(slot.Id));
    }
}
