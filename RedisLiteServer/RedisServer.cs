using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RedisLiteServer;

public class RedisServer(string ip = "127.0.0.1", int port = 6379, string filePath = "data.bin")
{
    private readonly string _ip = ip;
    private readonly int _port = port;
    private readonly string persistenceFilePath = filePath;
    private readonly CommandProcessor _commandProcessor = new(filePath);
    private readonly object commandProcessorLock = new();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        TcpListener? server = null;

        try
        {
            IPAddress localAddr = IPAddress.Parse(_ip);
            server = new TcpListener(localAddr, _port);
            server.Start();

            Console.WriteLine($"Redis Lite server listening on port {_port}");

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await server.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                client.NoDelay = true;
                Console.WriteLine("Client connected.");

                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting server: {ex.Message}");
        }
        finally
        {
            server?.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        if (bytesRead == 0) break;

                        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        string response;
                        lock (commandProcessorLock)
                        {
                            response = _commandProcessor.ProcessCommand(request);
                        }

                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                        await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Client disconnected.");
        }
    }

    public void LoadDatabaseState()
    {
        if (File.Exists(persistenceFilePath))
        {
            _commandProcessor.ProcessLoadCommand(persistenceFilePath);
        }
    }
}
