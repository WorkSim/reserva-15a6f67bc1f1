namespace Reserva.Domain;

/// <summary>Identifier for a bookable slot.</summary>
public record SlotId(string Value);

/// <summary>
/// A bookable window of time with a maximum number of simultaneous active bookings.
/// Start/end are always stored in UTC.
/// </summary>
public record Slot(SlotId Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc, int Capacity);

/// <summary>A single reservation against a slot.</summary>
public record Booking(string BookingId, SlotId SlotId, string CustomerId, bool Active);

/// <summary>The outcome of attempting to book a slot.</summary>
public record BookingResult(bool Ok, int StatusCode, string? BookingId);
