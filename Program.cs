using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

internal class Program
{
    const string MSYS2_root = @"C:\msys64";
    static readonly string MSYS2_bin = Path.Combine(MSYS2_root, "usr", "bin");
    static readonly string MSYS2_home = Environment.GetEnvironmentVariable("HOME") ?? Path.Combine(MSYS2_root, "home", Environment.GetEnvironmentVariable("USERNAME") ?? "");
    static readonly string envPath = $"{MSYS2_bin};{Environment.GetEnvironmentVariable("PATH") ?? ""}";

    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.GetEncoding("utf-8");

        var gitProcInfo = new ProcessStartInfo(Path.Combine(MSYS2_bin, "git"));
        foreach (var arg in args)
        {
            // 中括弧はエスケープ必要
            gitProcInfo.ArgumentList.Add(arg.Replace(@"{", @"\{").Replace(@"}", @"\}"));
        }

        int exitCode = ExecSubProc(gitProcInfo, out string gitOut);
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
                return ExecSubProc(cygpathProcInfo, out var cygpathOut) == 0 ? cygpathOut : throw new Exception();
            }));

            Console.Write(gitOutFixed);
        }
        catch
        {
            Console.Write(gitOut);
        }

        Environment.Exit(0);
    }

    static int ExecSubProc(ProcessStartInfo psi, out string stdout)
    {
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        psi.EnvironmentVariables["PATH"] = envPath;
        psi.EnvironmentVariables.Add("HOME", MSYS2_home);
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
        catch (Exception ex)
        {
            Console.Error.Write(ex.ToString());
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
}
