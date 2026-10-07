# Reserva — Booking Service

A small booking service: slots with a capacity, reservations against them, and
timezone-correct scheduling. C#, .NET 8.

## Running

### Prerequisites
- .NET SDK 8

```bash
dotnet test                                  # everything
dotnet test tests/Reserva.Tests              # just the tests you can see
```

## Layout

```
src/Reserva/
  Domain/     records: SlotId, Slot, Booking, BookingResult
  Store/      IBookingStore and the in-memory implementation
  Services/   BookingService, ScheduleService — the logic that matters
tests/Reserva.Tests/   tests you can read and run
```

This repository ships with known defects — that is the point. Your tickets say
which one is yours.

`tests/Reserva.Grading` is the suite your PR is graded against. Do not edit it.
