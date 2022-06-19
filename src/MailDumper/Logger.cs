namespace MailDumper;

public class Logger
{
    public void Info(string message)
    {
        Console.WriteLine(message);
    }

    public void Error(string message)
    {
        Console.Error.WriteLine(message);
    }
}