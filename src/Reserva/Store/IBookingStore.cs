using Reserva.Domain;

namespace Reserva.Store;

/// <summary>Storage for bookings, keyed by slot and by booking id.</summary>
public interface IBookingStore
{
    /// <summary>True when the slot currently has no active bookings.</summary>
    Task<bool> IsSlotFree(SlotId id, CancellationToken ct = default);

    /// <summary>The number of active bookings currently held against the slot.</summary>
    Task<int> ActiveCount(SlotId id, CancellationToken ct = default);

    /// <summary>Unconditionally stores the booking.</summary>
    Task Insert(Booking booking, CancellationToken ct = default);

    /// <summary>
    /// Atomically admits this booking if, and only if, the slot currently holds fewer
    /// than <paramref name="capacity"/> active bookings; returns false otherwise.
    /// Capacity is passed in rather than checked beforehand because there is no
    /// separate availability check to race against — the count and the claim happen
    /// under one atomic operation. This is the only capacity authority in the system:
    /// callers ask with the right capacity, they do not pre-check.
    /// </summary>
    Task<bool> TryReserve(Booking booking, int capacity, CancellationToken ct = default);

    /// <summary>Marks the booking inactive, freeing its slot. Returns false if not found.</summary>
    Task<bool> Release(string bookingId, CancellationToken ct = default);
}
