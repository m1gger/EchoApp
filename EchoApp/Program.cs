using EchoApp.Client;
using EchoApp.Server;

namespace EchoApp;

/// <summary>
/// Точка входа приложения. Запускает сервер или клиент в зависимости от аргументов командной строки.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "server":
            {
                int port = args.Length > 1 ? int.Parse(args[1]) : 5000;
                var server = new EchoServer(port);
                await server.StartAsync();
                break;
            }
            case "client":
            {
                string host    = args.Length > 1 ? args[1] : "127.0.0.1";
                int    port    = args.Length > 2 ? int.Parse(args[2]) : 5000;
                string logFile = args.Length > 3 ? args[3] : "client.log";
                var client = new EchoClient(host, port, logFile);
                await client.ConnectAsync();
                break;
            }
            default:
                Console.Error.WriteLine($"Неизвестный режим: {args[0]}");
                PrintUsage();
                break;
        }
    }

    /// <summary>
    /// Выводит справку по использованию программы.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("Echo Server/Client");
        Console.WriteLine();
        Console.WriteLine("Использование:");
        Console.WriteLine("  Режим сервера:  EchoApp server [port]");
        Console.WriteLine("  Режим клиента:  EchoApp client [host] [port] [logfile]");
        Console.WriteLine();
        Console.WriteLine("Значения по умолчанию:");
        Console.WriteLine("  port    = 5000");
        Console.WriteLine("  host    = 127.0.0.1");
        Console.WriteLine("  logfile = client.log");
        Console.WriteLine();
        Console.WriteLine("Примеры:");
        Console.WriteLine("  EchoApp server 5000");
        Console.WriteLine("  EchoApp client 127.0.0.1 5000 my.log");
    }
}
