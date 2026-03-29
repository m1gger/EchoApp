using System.Net;
using System.Net.Sockets;

namespace EchoApp.Server;

/// <summary>
/// TCP-сервер с поддержкой нескольких клиентов одновременно.
/// Каждое полученное сообщение рассылается всем подключённым клиентам.
/// Хранит историю последних <see cref="MaxHistorySize"/> сообщений для
/// новых или медленно читающих клиентов.
/// </summary>
public sealed class EchoServer
{
    private const int MaxHistorySize = 100;

    private readonly int _port;
    private readonly List<ClientHandler> _clients = [];
    private readonly Queue<string> _messageHistory = new();
    private readonly object _lock = new();

    public EchoServer(int port)
    {
        _port = port;
    }

    /// <summary>
    /// Запускает прослушивание входящих TCP-подключений. Работает до остановки процесса.
    /// </summary>
    public async Task StartAsync()
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[Server] Ожидание подключений на порту {_port}. Для остановки нажмите Ctrl+C.");

        while (true)
        {
            TcpClient tcpClient = await listener.AcceptTcpClientAsync();
            var handler = new ClientHandler(tcpClient, this);

            lock (_lock)
            {
                _clients.Add(handler);

                // Отправляем накопленную историю сообщений новому клиенту.
                foreach (string msg in _messageHistory)
                {
                    handler.EnqueueMessage(msg);
                }
            }

            Console.WriteLine($"[Server] Клиент подключился: {tcpClient.Client.RemoteEndPoint}");

            // Запускаем обработчик клиента асинхронно, не блокируя цикл приёма.
            _ = handler.HandleAsync();
        }
    }

    /// <summary>
    /// Рассылает <paramref name="message"/> всем подключённым клиентам
    /// и добавляет сообщение в историю.
    /// </summary>
    public void Broadcast(string message)
    {
        lock (_lock)
        {
            _messageHistory.Enqueue(message);
            if (_messageHistory.Count > MaxHistorySize)
                _messageHistory.Dequeue();

            foreach (ClientHandler client in _clients)
            {
                client.EnqueueMessage(message);
            }
        }
    }

    /// <summary>
    /// Удаляет отключившегося клиента из списка активных подключений.
    /// </summary>
    public void RemoveClient(ClientHandler handler)
    {
        lock (_lock)
        {
            _clients.Remove(handler);
        }
        Console.WriteLine("[Server] Клиент отключился.");
    }
}
