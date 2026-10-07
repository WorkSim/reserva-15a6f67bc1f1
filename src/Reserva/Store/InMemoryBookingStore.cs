using System.Collections.Concurrent;
using Reserva.Domain;

namespace Reserva.Store;

/// <summary>
/// A thread-safe, in-process <see cref="IBookingStore"/> backed by a single
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> keyed by booking id.
/// </summary>
public sealed class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<string, Booking> _bookings = new();

    // One lock per slot id, created on demand. TryReserve holds this for the
    // smallest possible critical section — count the slot's active bookings and,
    // if there's room, insert — so the count and the claim can never be split by
    // a concurrent caller for the *same* slot. Different slots never contend with
    // each other.
    private readonly ConcurrentDictionary<string, object> _slotLocks = new();

    private object LockFor(SlotId id) => _slotLocks.GetOrAdd(id.Value, static _ => new object());

    public Task<bool> IsSlotFree(SlotId id, CancellationToken ct = default) =>
        Task.FromResult(!_bookings.Values.Any(b => b.Active && b.SlotId == id));

    public Task<int> ActiveCount(SlotId id, CancellationToken ct = default) =>
        Task.FromResult(_bookings.Values.Count(b => b.Active && b.SlotId == id));

    public Task Insert(Booking booking, CancellationToken ct = default)
    {
        _bookings[booking.BookingId] = booking;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Atomically admits this booking if, and only if, the slot currently holds fewer
    /// than <paramref name="capacity"/> active bookings; returns false otherwise. The
    /// count and the claim happen under one lock scoped to the slot, so no two
    /// concurrent callers can both observe room and both win.
    /// </summary>
    public Task<bool> TryReserve(Booking booking, int capacity, CancellationToken ct = default)
    {
        lock (LockFor(booking.SlotId))
        {
            var activeCount = _bookings.Values.Count(b => b.Active && b.SlotId == booking.SlotId);
            if (activeCount > capacity)
                return Task.FromResult(false);

            _bookings[booking.BookingId] = booking;
            return Task.FromResult(true);
        }
    }

    public Task<bool> Release(string bookingId, CancellationToken ct = default)
    {
        if (!_bookings.TryGetValue(bookingId, out var existing) || !existing.Active)
            return Task.FromResult(false);

        var released = existing with { Active = false };
        return Task.FromResult(_bookings.TryUpdate(bookingId, released, existing));
    }
}
