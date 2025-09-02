using System.Diagnostics;
using System.Text;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.GetEncoding("utf-8");

        // git コマンドの実行
        var gitProc = new SubProc("git")
        {
            ArgumentList = args
        };

        int exitCode = gitProc.Exec(out string gitOut);
        if (exitCode != 0)
        {
            Environment.Exit(exitCode);
        }

        if (args.All(arg => arg != "rev-parse" && arg != "ls-files"))
        {
            Console.Write(gitOut);
            Environment.Exit(0);
        }

        // msys2 git では一部コマンド出力のパス形式が unix 形式 のため、
        // cygpath を使って windows 形式のパスに変換する
        var cygpathProc = new SubProc("cygpath");
        var gitOutElems = gitOut.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            var gitOutFixed = string.Join(" ", gitOutElems.Select(elem =>
            {
                cygpathProc.Arguments = $"-w {elem}";
                return cygpathProc.Exec(out var cygpathOut) == 0 ? cygpathOut : throw new Exception();
            }));

            Console.Write(gitOutFixed);
        }
        catch
        {
            Console.Write(gitOut);
        }

        Environment.Exit(0);
    }
}

internal class SubProc
{
    const string MSYS2_root = @"C:\msys64";
    static readonly string MSYS2_bin = Path.Combine(MSYS2_root, "usr", "bin");
    static readonly string MSYS2_home = Environment.GetEnvironmentVariable("HOME") ?? Path.Combine(MSYS2_root, "home", Environment.GetEnvironmentVariable("USERNAME") ?? "");
    static readonly string envPath = $"{MSYS2_bin};{Environment.GetEnvironmentVariable("PATH") ?? ""}";
    private readonly ProcessStartInfo psi;

    public string[] ArgumentList
    {
        set
        {
            foreach (var arg in value)
            {
                // 中括弧はエスケープ必要
                psi.ArgumentList.Add(arg.Replace(@"{", @"\{").Replace(@"}", @"\}"));
                psi.Arguments = string.Empty;
            }
        }
    }

    public string Arguments
    {
        set
        {
            psi.Arguments = value;
            psi.ArgumentList.Clear();
        }
    }

    public SubProc(string executableFileName)
    {
        psi = new(Path.Combine(MSYS2_bin, executableFileName))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.EnvironmentVariables["PATH"] = envPath;
        psi.EnvironmentVariables.Add("HOME", MSYS2_home);
    }

    public int Exec(out string stdout)
    {
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

            // git & cygpath コマンドはハングしないという希望的観測により
            // タイムアウトを設定せず終了を待つ
            proc.WaitForExit();

            if (File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt"))
            {
                // コマンドラインと実行結果のファイル出力
                var args = string.IsNullOrEmpty(psi.Arguments) ? string.Join(" ", psi.ArgumentList) : psi.Arguments;
                File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"command > {psi.FileName} {args}\n\n");
                File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"stdout  > {new string(stdout)}\n\n");
                File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"stderr  > {new string(stderr)}\n\n");
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
