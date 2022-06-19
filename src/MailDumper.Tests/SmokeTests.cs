using System.Net;
using System.Net.Mail;
using Shouldly;

namespace MailDumper.Tests;

public class SmokeTests : GoldTestBase
{
    [Fact]
    public void SendSingleMessage()
    {
        ExecuteWithGold("SendSingleMessage", sw =>
        {
            var tempStoragePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(tempStoragePath);
                using var smtpServer = new SmtpServer(IPAddress.Parse("127.0.0.1"), tempStoragePath);
                smtpServer.Start();
                var smtpClient = new SmtpClient();
                smtpClient.Host = "127.0.0.1";
                smtpClient.Port = smtpServer.Port;
                smtpClient.Send(new MailMessage("admin@example.com", "me@example.com", "Hello", "Test"));
                var files = Directory.GetFiles(tempStoragePath);
                files.Length.ShouldBe(1);
                sw.Write(MaskDate(File.ReadAllText(files.Single())));
            }
            finally
            {
                Directory.Delete(tempStoragePath, true);
            }
        });
    }
}