using System.Runtime.InteropServices;
using System.Text;

namespace BotArena.WasmGuest;

internal static class Program
{
    [WasmImportLinkage]
    [DllImport("botarena")]
    private static extern unsafe int next_observation(byte* buffer, int capacity);

    [WasmImportLinkage]
    [DllImport("botarena")]
    private static extern unsafe void post_decision(byte* buffer, int length);

    private static unsafe int Main()
    {
        var buffer = new byte[128 * 1024];
        GuestSession? session = null;
        while (true)
        {
            int length;
            fixed (byte* pointer = buffer)
                length = next_observation(pointer, buffer.Length);
            if (length <= 0)
                return 0;

            string line = Encoding.UTF8.GetString(buffer, 0, length);
            string reply;
            try
            {
                if (line.StartsWith("I ", StringComparison.Ordinal))
                {
                    session = GuestSession.Start(line);
                    reply = "R " + GuestProtocol.ProtocolVersion;
                }
                else if (session is not null)
                {
                    reply = session.HandleTick(line);
                }
                else
                {
                    reply = GuestProtocol.FormatFault("Tick received before init.");
                }
            }
            catch (Exception ex)
            {
                // A bot exception must never kill the guest loop: report it as a fault
                // and keep serving ticks (the engine applies the fault rules).
                reply = GuestProtocol.FormatFault($"{ex.GetType().Name}: {ex.Message}");
            }

            var replyBytes = Encoding.UTF8.GetBytes(reply);
            fixed (byte* pointer = replyBytes)
                post_decision(pointer, replyBytes.Length);
        }
    }
}
