using System.Diagnostics;
using System.Text;

const string MSYS2_bin = @"c:\msys64\usr\bin";

Console.OutputEncoding = Encoding.GetEncoding("utf-8");

static bool execSubProc(ProcessStartInfo psi, out string stdout)
{
    psi.UseShellExecute = false;
    psi.RedirectStandardOutput = true;
    psi.RedirectStandardError = true;
    psi.CreateNoWindow = true;
    stdout = "";
    var stderr = "";

    try
    {
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            return false;
        }

        stdout = proc.StandardOutput.ReadToEnd();
        stderr = proc.StandardError.ReadToEnd();

        if (!proc.WaitForExit(10 * 1000))
        {
            return false;
        }
    }
    catch
    {
        return false;
    }
    finally
    {
        if (!string.IsNullOrEmpty(stderr))
        {
            Console.Error.Write(stderr);
        }
    }

    return true;
}

string gitOut = "";
if (args.Any(arg => arg == "stash"))
{
    var gitProcInfo = new ProcessStartInfo(Path.Combine(MSYS2_bin, "bash"))
    {
        Arguments = $"--login -c 'git {string.Join(" ", args)}'"
    };
    gitProcInfo.EnvironmentVariables.Add("MSYSTEM", "MSYS");
    gitProcInfo.EnvironmentVariables.Add("CHERE_INVOKING", "1");

    if (!execSubProc(gitProcInfo, out gitOut))
    {
        Environment.Exit(1);
    }
}
else
{
    var gitProcInfo = new ProcessStartInfo(Path.Combine(MSYS2_bin, "git"));
    foreach (var arg in args)
    {
        gitProcInfo.ArgumentList.Add(arg);
    }

    if (!execSubProc(gitProcInfo, out gitOut))
    {
        Environment.Exit(1);
    }
}

if (args.All(arg => arg != "rev-parse" && arg != "ls-files"))
{
    Console.Write(gitOut);
    Environment.Exit(0);
}

var cygpathProcInfo = new ProcessStartInfo(Path.Combine(MSYS2_bin, "cygpath"));
var gitOutElems = gitOut.Split(" ", StringSplitOptions.RemoveEmptyEntries);
try
{
    var gitOutFixed = string.Join(" ", gitOutElems.Select(elem =>
    {
        cygpathProcInfo.Arguments = $"-w {elem}";
        return execSubProc(cygpathProcInfo, out var cygpathOut) ? cygpathOut : throw new Exception();
    }));

    Console.Write(gitOutFixed);
}
catch
{
    Console.Write(gitOut);
}

Environment.Exit(0);
