using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace MailDumper;

public class SmtpServer : IDisposable
{
    private readonly string _storagePath;
    private readonly TcpListener _listener;
    private int _port;
    private bool _started;
    private readonly Logger _logger = new();
    private readonly Regex _ehloRegex = new("EHLO (.+)");
    private readonly Regex _mailFromRx = new("MAIL FROM:<(.+)>");
    private readonly Regex _rcptToRx = new("RCPT TO:<(.+)>");


    public SmtpServer(IPAddress ipAddress, string storagePath, int? port = null)
    {
        _storagePath = storagePath;
        _port = port ?? 0;
        _listener = new TcpListener(ipAddress, _port);
    }

    public int Port
    {
        get
        {
            if (_port == 0)
            {
                throw new InvalidOperationException("Server not started");
            }

            return _port;
        }
        private set => _port = value;
    }


    public void Start()
    {
        _listener.Start();
        if (_port == 0)
        {
            Port = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? 0;
        }

        new Thread(() =>
        {
            Thread.CurrentThread.Name = "Client receiver";
            ThreadPool.QueueUserWorkItem(ClientHandler, _listener.AcceptTcpClient());
        }).Start();

        _started = true;
    }

    private void ClientHandler(object? stateInfo)
    {
        var client = stateInfo as TcpClient;
        if (client?.Client.RemoteEndPoint is not IPEndPoint clientEndPoint)
        {
            _logger.Error("invalid client end point");
            return;
        }

        _logger.Info($"client {clientEndPoint.Address} connected");
        using var stream = client.GetStream();
        using var rdr = new StreamReader(stream);
        using var wrt = new StreamWriter(stream);
        void Close()
        {
            rdr.Close();
            wrt.Close();
            stream.Close();
        }
        wrt.Write("220 mail.dumper this is simple stub SMTP server\r\n");
        wrt.Flush();
        // EHLO
        var ehloStr = rdr.ReadLine();
        if (ehloStr == null)
        {
            _logger.Error("broken pipe");
            Close();
            return;
        }
        var m = _ehloRegex.Match(ehloStr);
        if (!m.Success)
        {
            _logger.Error($@"invalid 'EHLO' message format: '{ehloStr}'");
            wrt.Write("503 bad sequence of commands\r\n");
            wrt.Flush();
            Close();
            return;
        }

        var clientId = m.Groups[1].Value;
        _logger.Info($"receive EHLO from {clientId}");
        wrt.Write($"250 mail.dumper hello {clientId} [{clientEndPoint.Address}]\r\n");
        wrt.Flush();
        // MAIL FROM
        var mailFromStr = rdr.ReadLine() ?? "null";
        m = _mailFromRx.Match(mailFromStr);
        if (!m.Success)
        {
            _logger.Error($@"invalid 'MAIL FROM' message format: '{mailFromStr}'");
            wrt.Write("503 Bad sequence of commands\r\n");
            wrt.Flush();
            Close();
            return;
        }
        _logger.Info($"receive MAIL FROM:<{m.Groups[1]}>");
        wrt.Write($"250 {m.Groups[1]} sender accepted\r\n");
        wrt.Flush();
        // RCPT TO:
        var rcptToStr = rdr.ReadLine();
        if (rcptToStr == null)
        {
            _logger.Error("broken pipe");
            Close();
            return;
        }
        m = _rcptToRx.Match(rcptToStr);
        if (!m.Success)
        {
            _logger.Error($@"invalid 'RCPT TO' message format: '{rcptToStr}'");
            wrt.Write("503 bad sequence of commands\r\n");
            wrt.Flush();
            Close();
            return;
        }
        _logger.Info($"receive RCPT TO:<{m.Groups[1]}>");
        wrt.Write($"250 {m.Groups[1]} ok\r\n");
        wrt.Flush();
        // Waiting for DATA
        while (true)
        {
            var line = rdr.ReadLine()?.Trim();
            if (line == null)
            {
                _logger.Error("broken pipe");
                Close();
                return;
            }
            if (line == "DATA")
            {
               break;
            }

            m = _rcptToRx.Match(line);
            if (!m.Success)
            {
                _logger.Error($@"invalid 'RCPT TO' message format: '{rcptToStr}'");
                wrt.Write("503 bad sequence of commands\r\n");
                wrt.Flush();
                Close();
                return;
            }
            _logger.Info($"receive RCPT TO:<{m.Groups[1]}>");
            wrt.Write($"250 {m.Groups[1]} ok\r\n");
            wrt.Flush();
        }
        _logger.Info("receive DATA");
        wrt.Write("354 enter mail, end with '.' on a line by itself\r\n");
        wrt.Flush();
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }

        using var fs = File.CreateText(Path.Combine(_storagePath,
            $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture)}_{Guid.NewGuid()}.txt"));
        while (true)
        {
            var line = rdr.ReadLine();
            if (line == null)
            {
                _logger.Error("broken pipe");
                fs.Close();
                Close();
                return;
            }
            if (line == ".")
            {
                break;
            }
            fs.WriteLine(line);
        }
        fs.Close();
        wrt.Write("250 message accepted for delivery\r\n");
        wrt.Flush();
        var quitLine = rdr.ReadLine() ?? "null";
        if (quitLine != "QUIT")
        {
            _logger.Error($@"invalid command sequence: '{quitLine}'");
            wrt.Write("503 bad sequence of commands\r\n");
            wrt.Flush();
            Close();
            return;
        }
        wrt.Write("221 mail.dumper closing connection\r\n");
        wrt.Flush();
        Close();
    }

    public void Dispose()
    {
        if (_started)
        {
            _listener.Stop();
        }
    }
}