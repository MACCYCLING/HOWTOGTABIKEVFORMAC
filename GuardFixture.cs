using System;
using System.ComponentModel;
public static class GuardFixture {
    static bool disposed;
    public static void ScreenShot(int mode) {
        try {
            if (mode == 1) throw new Win32Exception(123);
            if (mode == 2) throw new InvalidOperationException("unrelated failure");
        } finally { disposed = true; }
    }
    public static int Main() {
        disposed = false; ScreenShot(0);
        if (!disposed) throw new Exception("Success path skipped cleanup.");
        disposed = false; ScreenShot(1);
        if (!disposed) throw new Exception("Screenshot failure skipped cleanup.");
        disposed = false;
        try { ScreenShot(2); throw new Exception("Unrelated error was swallowed."); }
        catch (InvalidOperationException) {
            if (!disposed) throw new Exception("Unrelated error skipped cleanup.");
        }
        Console.WriteLine("PASS: successful capture preserved; Win32 failure contained; unrelated errors propagate; finally cleanup preserved.");
        return 0;
    }
}
