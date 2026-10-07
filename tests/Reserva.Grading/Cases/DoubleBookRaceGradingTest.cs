// Authoritative grading suite for BOOK-801 — a slot must hold at most one
// active booking even when two requests arrive concurrently. Overlaid by the
// grader; edits in the student's repo are ignored.
using System.Collections.Concurrent;
using Xunit;

public class DoubleBookRaceGradingTest
{
    // A store that parks both callers inside the check-then-insert gap before
    // letting either proceed. No sleeps and no retries: the interleaving that a
    // real race only sometimes produces happens on every run, deterministically.
    private sealed class InterleavingStore : IBookingStore
    {
        // The rendezvous is BOUNDED. A fix that serialises Book() end-to-end
        // (say, one semaphore around the whole method) while keeping the
        // IsSlotFree pre-check means the second caller can never arrive while
        // the first is still inside this method, so an unbounded wait would
        // hang the whole grading run until the sandbox's 4-minute timeout and
        // report an infrastructure failure. Timing out here instead turns that
        // into an ordinary, readable test failure that names the cause.
        private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(10);

        private readonly ConcurrentDictionary<string, Booking> _bySlot = new();
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _gate = new();
        private int _arrived;

        public async Task<bool> IsSlotFree(SlotId id, CancellationToken ct = default)
        {
            var free = !_bySlot.ContainsKey(id.Value);
            if (Interlocked.Increment(ref _arrived) == 2) _bothArrived.TrySetResult();
            // both callers now hold a stale "free" — or nobody else is coming
            if (await Task.WhenAny(_bothArrived.Task, Task.Delay(RendezvousTimeout)) != _bothArrived.Task)
            {
                throw new TimeoutException(
                    $"Only one caller reached the availability check within {RendezvousTimeout.TotalSeconds:0}s. " +
                    "Book() still reads availability before claiming the slot, and the two requests were " +
                    "serialised so they can never overlap — the check-then-insert gap is still there, it is " +
                    "just held shut by a lock around the whole method. Claim the slot atomically instead " +
                    "(IBookingStore.TryReserve) rather than checking first and inserting after.");
            }
            return free;
        }

        public Task<int> ActiveCount(SlotId id, CancellationToken ct = default) =>
            Task.FromResult(_bySlot.ContainsKey(id.Value) ? 1 : 0);

        public Task Insert(Booking booking, CancellationToken ct = default)
        {
            _bySlot[booking.SlotId.Value] = booking;   // last writer wins: the bug
            return Task.CompletedTask;
        }

        public Task<bool> TryReserve(Booking booking, int capacity, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_bySlot.Count(kv => kv.Value.SlotId == booking.SlotId && kv.Value.Active) >= capacity)
                    return Task.FromResult(false);
                _bySlot[booking.BookingId] = booking;
                return Task.FromResult(true);
            }
        }

        public Task<bool> Release(string bookingId, CancellationToken ct = default)
        {
            var hit = _bySlot.FirstOrDefault(kv => kv.Value.BookingId == bookingId);
            return Task.FromResult(hit.Key is not null && _bySlot.TryRemove(hit.Key, out _));
        }
    }

    private static Slot OneSeat() =>
        new(new SlotId("slot-1"), DateTimeOffset.Parse("2026-03-01T10:00:00Z"), DateTimeOffset.Parse("2026-03-01T11:00:00Z"), 1);

    [Fact]
    public async Task ConcurrentRequestsForOneSlotProduceExactlyOneWinner()
    {
        var svc = new BookingService(new InterleavingStore());
        var slot = OneSeat();

        var results = await Task.WhenAll(svc.Book(slot, "alice"), svc.Book(slot, "bob"));

        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal(409, Assert.Single(results, r => !r.Ok).StatusCode);
    }

    [Fact]
    public async Task CancellingFreesTheSlotForANewBooking()
    {
        var svc = new BookingService(new InterleavingStore());
        var slot = OneSeat();
        var results = await Task.WhenAll(svc.Book(slot, "alice"), svc.Book(slot, "bob"));
        var winner = Assert.Single(results, r => r.Ok);

        Assert.True(await svc.Cancel(winner.BookingId!));
        Assert.True((await svc.Book(slot, "carol")).Ok);
    }
}
