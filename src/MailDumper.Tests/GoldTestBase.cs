using System.Text.RegularExpressions;
using Shouldly;

namespace MailDumper.Tests;

public class GoldTestBase
{
    private readonly Regex _dateRegex = new("Date:.+", RegexOptions.Compiled);

    protected string MaskDate(string text)
    {
        return _dateRegex.Replace(text, "DATE: $$DATE$$");
    }

    protected static void ExecuteWithGold(string goldFileName, Action<StreamWriter> dumper)
    {
        var goldFile = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "..", "..", "..", "TestData", $"{goldFileName}.gold"));
        var tempFile = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), 
            "..", "..", "..", "TestData", $"{goldFileName}.temp"));
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        var ms = new MemoryStream();
        using var wrt = new StreamWriter(ms);
        dumper(wrt);
        wrt.Flush();
        ms.Seek(0, SeekOrigin.Begin);
        using var rdr = new StreamReader(ms);
        var temp = rdr.ReadToEnd();
        wrt.Close();
        rdr.Close();
        ms.Close();
        if (File.Exists(goldFile))
        {
            var gold = File.ReadAllText(goldFile);
            if (temp == gold) return;
            File.WriteAllText(tempFile, temp);
            temp.ShouldBe(gold);
        }
        else
        {
            File.WriteAllText(tempFile, temp);
            throw new ShouldAssertException($"Gold file: '{goldFile}' not exists");
        }
    }
}