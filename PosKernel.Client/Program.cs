namespace PosKernel.Client;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("POS Kernel Client Library v0.5.0");
        Console.WriteLine("This is a library project - use it as a reference in your applications.");
        Console.WriteLine();
        Console.WriteLine("Example usage:");
        Console.WriteLine("  var client = new PosKernelClient();");
        Console.WriteLine("  await client.ConnectAsync();");
        Console.WriteLine("  var sessionId = await client.CreateSessionAsync(\"terminal1\", \"operator1\");");
        Console.WriteLine("  // ... perform transactions ...");
        Console.WriteLine("  await client.CloseSessionAsync(sessionId);");
    }
}
