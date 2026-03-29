using System.Net.Sockets;
using System.Text;

namespace EchoApp.Client;

/// <summary>
/// TCP-клиент эхо-сервера. Читает строки из стандартного ввода и отправляет их на сервер.
/// Все сообщения, полученные от сервера, выводятся в консоль и дописываются в лог-файл.
/// </summary>
public sealed class EchoClient
{
    private readonly string _host;
    private readonly int    _port;
    private readonly string _logFile;

    public EchoClient(string host, int port, string logFile)
    {
        _host    = host;
        _port    = port;
        _logFile = logFile;
    }

    /// <summary>
    /// Устанавливает TCP-соединение с сервером и запускает циклы чтения и записи.
    /// Возвращает управление после закрытия стандартного ввода (Ctrl+Z / Ctrl+D).
    /// </summary>
    public async Task ConnectAsync()
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_host, _port);
        Console.WriteLine($"[Client] Подключено к {_host}:{_port}. Введите сообщение и нажмите Enter.");
        Console.WriteLine($"[Client] Входящие сообщения записываются в: {Path.GetFullPath(_logFile)}");

        var stream = tcpClient.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Токен отмены для остановки цикла чтения при завершении цикла записи.
        using var cts = new CancellationTokenSource();
        Task readTask = ReadLoopAsync(reader, cts.Token);

        await WriteLoopAsync(writer);

        // Стандартный ввод закрыт — останавливаем цикл чтения.
        cts.Cancel();
        await readTask;
    }

    /// <summary>
    /// Читает строки из стандартного ввода и отправляет каждую на сервер.
    /// Завершается при получении EOF (Ctrl+Z в Windows, Ctrl+D в Linux).
    /// </summary>
    private static async Task WriteLoopAsync(StreamWriter writer)
    {
        try
        {
            while (true)
            {
                // Console.ReadLine блокирует поток; выполняем в пуле потоков.
                string? input = await Task.Run(Console.ReadLine);
                if (input is null)
                    break;

                await writer.WriteLineAsync(input);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            Console.Error.WriteLine($"[Client] Соединение разорвано: {ex.Message}");
        }
    }

    /// <summary>
    /// Читает сообщения от сервера, выводит их в консоль и дописывает в лог-файл.
    /// Завершается при отмене токена или разрыве соединения.
    /// </summary>
    private async Task ReadLoopAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        await using var logWriter = new StreamWriter(_logFile, append: true, Encoding.UTF8)
        {
            AutoFlush = true
        };

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                string message = line + "\n";
                Console.Write($"[Echo] {message}");
                await logWriter.WriteAsync(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Штатное завершение по отмене токена.
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            Console.Error.WriteLine($"[Client] Сервер отключился: {ex.Message}");
        }
    }
}
