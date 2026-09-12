using System;

namespace AdofaiRichPresence.Core {
    // The caller supplies monotonic elapsed time so changing the system clock
    // cannot shorten or extend recovery. Cleanup/report failures are isolated too.
    internal sealed class CallbackRecovery {
        private TimeSpan nextAttempt;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        internal void Run(TimeSpan now, Action update, Action cleanup, Action<Exception> report) {
            if (now < nextAttempt) {
                return;
            }
            try {
                update();
            } catch (Exception error) {
                // Set the deadline first: a failed cleanup must not cause a
                // reconnect/exception loop on subsequent frames.
                nextAttempt = now + RetryDelay;
                ReportSafely(report, error);
                try {
                    cleanup();
                } catch (Exception cleanupError) {
                    ReportSafely(report, cleanupError);
                }
            }
        }

        internal void Reset() {
            nextAttempt = TimeSpan.Zero;
        }

        private static void ReportSafely(Action<Exception> report, Exception error) {
            try {
                report(error);
            } catch {
                // A broken logging sink must not escape the UMM update callback.
            }
        }
    }
}
