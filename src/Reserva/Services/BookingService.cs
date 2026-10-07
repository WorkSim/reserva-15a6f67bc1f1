using Reserva.Domain;
using Reserva.Store;

namespace Reserva.Services;

/// <summary>Books and cancels reservations against slots.</summary>
public class BookingService(IBookingStore store)
{
    public async Task<BookingResult> Book(Slot slot, string customerId, CancellationToken ct = default)
    {
        // Bypasses the atomic primitive entirely: reads availability, then inserts.
        if (await store.IsSlotFree(slot.Id, ct))
        {
            var booking = new Booking(Guid.NewGuid().ToString(), slot.Id, customerId, true);
            await store.Insert(booking, ct);           // gap: another caller may have inserted
            return new BookingResult(true, 201, booking.BookingId);
        }
        return new BookingResult(false, 409, null);
    }

    public Task<bool> Cancel(string bookingId, CancellationToken ct = default) =>
        store.Release(bookingId, ct);
}
