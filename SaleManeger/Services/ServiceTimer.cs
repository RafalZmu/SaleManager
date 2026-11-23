using System;

namespace SaleManeger.Services
{
    /// <summary>
    /// Measures how long serving one client takes during one visit of the client screen.
    /// It starts when the first bought item with an amount is entered and each save (and the final one when the
    /// screen is closed) adds the time since the previous event. A gap longer than <see cref="IdleCap"/> counts
    /// only as much as the cap, so a client screen left open while the seller wandered off does not spoil the stats.
    /// Entering the data of the order (the left box) never starts it: only the time of handing out goods counts.
    /// </summary>
    public class ServiceTimer
    {
        public static readonly TimeSpan IdleCap = TimeSpan.FromMinutes(5);

        private DateTime? _last;

        public DateTime? Started { get; private set; }

        /// <summary>Active time of this visit up to the last event.</summary>
        public double Seconds { get; private set; }

        public bool IsRunning => Started.HasValue;

        /// <summary>A bought item was typed or changed. The first call starts the timer.</summary>
        public void Activity(DateTime now)
        {
            if (!Started.HasValue)
            {
                Started = now;
                _last = now;
                return;
            }
            Advance(now);
        }

        /// <summary>A save or the close of the screen: adds the time since the last event.</summary>
        public void Checkpoint(DateTime now)
        {
            if (Started.HasValue)
                Advance(now);
        }

        /// <summary>The bought items were all removed again, so nothing is being served.</summary>
        public void Cancel()
        {
            Started = null;
            _last = null;
            Seconds = 0;
        }

        private void Advance(DateTime now)
        {
            var gap = now - _last.Value;
            if (gap > TimeSpan.Zero)
                Seconds += (gap > IdleCap ? IdleCap : gap).TotalSeconds;
            _last = now;
        }
    }
}
