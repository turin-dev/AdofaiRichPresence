using System;
using AdofaiRichPresence.Core;

internal static class Program {
    private static int Main() {
        try {
            CheckSuccessfulFrames();
            CheckRetryBoundary();
            CheckCleanupAndLoggerFailures();
            CheckReset();
            Console.WriteLine("PASS: 4 production CallbackRecovery regression scenarios.");
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Equal(int expected, int actual, string label) {
        if (expected != actual) throw new Exception($"{label}: expected {expected}, got {actual}");
    }

    private static void CheckSuccessfulFrames() {
        var gate = new CallbackRecovery();
        int calls = 0, cleanup = 0, reports = 0;
        for (int frame = 0; frame < 120; frame++)
            gate.Run(TimeSpan.FromSeconds(frame / 60.0), () => calls++, () => cleanup++, _ => reports++);
        Equal(120, calls, "successful frames are not throttled");
        Equal(0, cleanup, "successful cleanup");
        Equal(0, reports, "successful reports");
    }

    private static void CheckRetryBoundary() {
        var gate = new CallbackRecovery();
        int calls = 0, cleanup = 0, reports = 0;
        Action fail = () => { calls++; throw new Exception("update failed"); };
        for (int frame = 0; frame < 1800; frame++)
            gate.Run(TimeSpan.FromSeconds(frame / 60.0), fail, () => cleanup++, _ => reports++);
        Equal(1, calls, "1800-frame retry suppression");
        Equal(1, cleanup, "one cleanup per failure");
        Equal(1, reports, "one report per failure");
        gate.Run(TimeSpan.FromSeconds(30), fail, () => cleanup++, _ => reports++);
        Equal(2, calls, "retry exactly at deadline");
        gate.Run(TimeSpan.FromSeconds(59.999), fail, () => cleanup++, _ => reports++);
        Equal(2, calls, "new failure advances deadline");
        gate.Run(TimeSpan.FromSeconds(60), () => calls++, () => cleanup++, _ => reports++);
        gate.Run(TimeSpan.FromSeconds(60.01), () => calls++, () => cleanup++, _ => reports++);
        Equal(4, calls, "normal updates resume after recovery");
        Equal(2, cleanup, "no cleanup on recovered frames");
        Equal(2, reports, "no reports on recovered frames");
    }

    private static void CheckCleanupAndLoggerFailures() {
        var gate = new CallbackRecovery();
        int calls = 0, cleanup = 0, reports = 0;
        Action update = () => { calls++; throw new Exception("update"); };
        Action stop = () => { cleanup++; throw new Exception("cleanup"); };
        Action<Exception> log = _ => { reports++; throw new Exception("logger"); };
        gate.Run(TimeSpan.Zero, update, stop, log);
        gate.Run(TimeSpan.FromSeconds(1), update, stop, log);
        Equal(1, calls, "cleanup/logger failures preserve cooldown");
        Equal(1, cleanup, "cleanup still runs after logger failure");
        Equal(2, reports, "both update and cleanup errors reported");
        gate.Run(TimeSpan.FromSeconds(30), update, stop, log);
        Equal(2, calls, "secondary failures do not prevent retry");
    }

    private static void CheckReset() {
        var gate = new CallbackRecovery();
        int calls = 0;
        gate.Run(TimeSpan.FromSeconds(100), () => { throw new Exception("update"); }, () => { }, _ => { });
        gate.Reset();
        gate.Run(TimeSpan.Zero, () => calls++, () => { }, _ => { });
        Equal(1, calls, "reset immediately permits updates");
    }
}
