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

        if (args.All(arg => arg != "rev-parse"))
        {
            Console.Write(gitOut);
            Environment.Exit(0);
        }

        // msys2 git では一部コマンド出力のパス形式が unix 形式 のため、
        // cygpath を使って windows 形式のパスに変換する
        var cygpathProc = new SubProc("cygpath");
        var gitOutLines = gitOut.Split('\n');
        try
        {
            var resultLines = (string[])gitOutLines.Clone();
            var targets = gitOutLines
                .Select((line, i) => (trimmed: line.TrimEnd('\r'), i))
                .Where(t => t.trimmed.Length > 0)
                .ToArray();

            if (targets.Length > 0)
            {
                // 行ごとに cygpath プロセスを起動すると N 回起動になり著しく遅いため、1 回の起動にまとめて渡す
                cygpathProc.RawArgumentList = ["-w", .. targets.Select(t => t.trimmed)];
                if (cygpathProc.Exec(out var cygpathOut) != 0)
                {
                    throw new Exception();
                }

                var convertedLines = cygpathOut.TrimEnd('\r', '\n').Split('\n');
                if (convertedLines.Length != targets.Length)
                {
                    throw new Exception();
                }

                for (int i = 0; i < targets.Length; i++)
                {
                    resultLines[targets[i].i] = convertedLines[i].TrimEnd('\r');
                }
            }

            Console.Write(string.Join("\n", resultLines));
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
            psi.ArgumentList.Clear();
            foreach (var arg in value)
            {
                // 中括弧はエスケープ必要
                psi.ArgumentList.Add(arg.Replace(@"{", @"\{").Replace(@"}", @"\}"));
            }
            psi.Arguments = string.Empty;
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

    public string[] RawArgumentList
    {
        set
        {
            psi.ArgumentList.Clear();
            foreach (var arg in value)
            {
                psi.ArgumentList.Add(arg);
            }
            psi.Arguments = string.Empty;
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
        psi.EnvironmentVariables["HOME"] = MSYS2_home;
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

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            stdout = stdoutTask.GetAwaiter().GetResult();
            stderr = stderrTask.GetAwaiter().GetResult();

            // git & cygpath コマンドはハングしないという希望的観測により
            // タイムアウトを設定せず終了を待つ
            proc.WaitForExit();

            // if (File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt"))
            // {
            //     // コマンドラインと実行結果のファイル出力
            //     var args = string.IsNullOrEmpty(psi.Arguments) ? string.Join(" ", psi.ArgumentList) : psi.Arguments;
            //     File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"command > {psi.FileName} {args}\n\n");
            //     File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"stdout  > {new string(stdout)}\n\n");
            //     File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}/log.txt", $"stderr  > {new string(stderr)}\n\n");
            // }

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
