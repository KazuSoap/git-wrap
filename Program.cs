using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

const string MSYS2_bin = @"c:\msys64\usr\bin";

Console.OutputEncoding = Encoding.GetEncoding("utf-8");

static int execSubProc(ProcessStartInfo psi, out string stdout)
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
            return 1;
        }

        stdout = proc.StandardOutput.ReadToEnd();
        stderr = proc.StandardError.ReadToEnd();

        if (!proc.WaitForExit(10 * 1000))
        {
            proc.Kill();
            return 1;
        }
        return proc.ExitCode;
    }
    catch
    {
        return 1;
    }
    finally
    {
        if (!string.IsNullOrEmpty(stderr))
        {
            Console.Error.Write(stderr);
        }
    }
}

string gitOut = "";
ProcessStartInfo gitProcInfo = new();
if (args.Any(arg => arg == "stash"))
{
    gitProcInfo.FileName = Path.Combine(MSYS2_bin, "bash");
    gitProcInfo.Arguments = $"--login -c 'git {string.Join(" ", args)}'";
    gitProcInfo.EnvironmentVariables.Add("MSYSTEM", "MSYS");
    gitProcInfo.EnvironmentVariables.Add("CHERE_INVOKING", "1");
}
else
{
    gitProcInfo = new ProcessStartInfo(Path.Combine(MSYS2_bin, "git"));
    if (args.Any(arg => arg == "log"))
    {
        foreach (var arg in args)
        {
            gitProcInfo.ArgumentList.Add(Regex.Replace(arg, @"{(.*)}", @"\{$1\}"));
        }
    }
    else
    {
        foreach (var arg in args)
        {
            gitProcInfo.ArgumentList.Add(arg);
        }
    }
}
int exitCode = execSubProc(gitProcInfo, out gitOut);
if (exitCode != 0)
{
    Environment.Exit(exitCode);
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
        return execSubProc(cygpathProcInfo, out var cygpathOut) == 0 ? cygpathOut : throw new Exception();
    }));

    Console.Write(gitOutFixed);
}
catch
{
    Console.Write(gitOut);
}

Environment.Exit(0);
