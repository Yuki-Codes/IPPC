using IPPCLibrary;

class Program
{
    private static ServerClient? ServerClient;

    static void Main(string[] args)
    {
        try
        {
            Program p = new();
            Task.Run(() => p.Start()).Wait();
        }
        finally { ServerClient?.Dispose(); }
    }

    private async Task Start()
    {
        Console.WriteLine($"Starting client on port {ServerClient.Port}");

        ServerClient = new();
        ServerClient.LogMessage = (msg) => Console.WriteLine(msg);
        ServerClient.LogError = (e, msg) => Console.WriteLine(msg);

        bool success = await ServerClient.StartClient();
        if (!success)
        {
            Console.WriteLine("Failed to connect to server");
        }
        else
        {
            Console.WriteLine("Started client");

            var ret = await ServerClient.Invoke<(int, int)>("Brio.ApiVersion");
            Console.WriteLine(ret);
        }

        ServerClient.Dispose();
    }
}