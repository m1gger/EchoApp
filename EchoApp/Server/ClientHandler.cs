using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace EchoApp.Server;

/// <summary>
/// Обрабатывает одно TCP-подключение на стороне сервера.
/// Читает входящие сообщения и передаёт их в <see cref="EchoServer.Broadcast"/>.
/// Исходящие сообщения помещаются в ограниченный канал: если клиент читает слишком
/// медленно и канал переполняется, старые сообщения автоматически вытесняются.
/// </summary>
public sealed class ClientHandler
{
    private const int OutboundQueueCapacity = 100;

    private readonly TcpClient _tcpClient;
    private readonly EchoServer _server;

    // Ограниченный канал: при переполнении вытесняет самое старое сообщение.
    private readonly Channel<string> _outboundQueue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(OutboundQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ClientHandler(TcpClient tcpClient, EchoServer server)
    {
        _tcpClient = tcpClient;
        _server    = server;
    }

    /// <summary>
    /// Добавляет сообщение в очередь исходящих данных клиента.
    /// Если очередь заполнена, самое старое сообщение вытесняется автоматически.
    /// </summary>
    public void EnqueueMessage(string message)
    {
        _outboundQueue.Writer.TryWrite(message);
    }

    /// <summary>
    /// Запускает циклы чтения и записи параллельно и ожидает завершения обоих
    /// при отключении клиента.
    /// </summary>
    public async Task HandleAsync()
    {
        var stream = _tcpClient.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        Task writeLoop = WriteLoopAsync(writer);

        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                // Добавляем символ новой строки, чтобы сообщение было самоделимым.
                string message = line + "\n";
                _server.Broadcast(message);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Штатное завершение подключения — дополнительных действий не требуется.
        }
        finally
        {
            _server.RemoveClient(this);
            _outboundQueue.Writer.Complete();
            _tcpClient.Close();
        }

        await writeLoop;
    }

    /// <summary>
    /// Опустошает очередь исходящих сообщений и записывает каждое из них в поток клиента.
    /// </summary>
    private async Task WriteLoopAsync(StreamWriter writer)
    {
        try
        {
            await foreach (string message in _outboundQueue.Reader.ReadAllAsync())
            {
                await writer.WriteAsync(message);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Клиент отключился во время записи; очистка выполняется в цикле чтения.
        }
    }
}
