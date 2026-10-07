using Reserva.Domain;
using Reserva.Services;
using Reserva.Store;

namespace Reserva.Tests;

public class BookingTests
{
    private static Slot CapacityOneSlot() =>
        new(new SlotId("slot-1"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), Capacity: 1);

    [Fact]
    public async Task Second_booking_of_a_full_slot_is_rejected()
    {
        var service = new BookingService(new InMemoryBookingStore());
        var slot = CapacityOneSlot();

        var first = await service.Book(slot, "alice");
        var second = await service.Book(slot, "bob");

        Assert.True(first.Ok);
        Assert.Equal(201, first.StatusCode);
        Assert.NotNull(first.BookingId);

        Assert.False(second.Ok);
        Assert.Equal(409, second.StatusCode);
        Assert.Null(second.BookingId);
    }

    [Fact]
    public async Task Cancelling_a_booking_frees_the_slot_for_a_new_booking()
    {
        var service = new BookingService(new InMemoryBookingStore());
        var slot = CapacityOneSlot();

        var first = await service.Book(slot, "alice");
        Assert.True(first.Ok);

        var cancelled = await service.Cancel(first.BookingId!);
        Assert.True(cancelled);

        var second = await service.Book(slot, "bob");
        Assert.True(second.Ok);
        Assert.Equal(201, second.StatusCode);
    }
}
