using System.Diagnostics;

namespace Note163Backup;
internal class CommandHelper
{
    public static (string command, string arguments) GetCommand(string rawCommand)
    {
        rawCommand = rawCommand.Trim();
        int index = rawCommand.IndexOf(' ');
        // 要执行的命令
        string command = rawCommand[..index].Trim();
        // 要传递给命令的参数
        string arguments = rawCommand[index..].Trim();
        return (command, arguments);
    }

    public static Task ExecCommand(string rawCommand)
    {
        (string command, string arguments) = GetCommand(rawCommand);
        return ExecCommand(command, arguments);
    }

    public static async Task ExecCommand(string command, string arguments)
    {
        Console.WriteLine($"准备执行命令: {command}");

        #region 执行命令

        // 创建 ProcessStartInfo 对象
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command, // 在 Linux 上，系统会从 PATH 环境变量中查找 'curl'
            Arguments = arguments,
            RedirectStandardOutput = true, // 重定向标准输出
            RedirectStandardError = true,  // 重定向标准错误
            UseShellExecute = false,       // 必须为 false 才能重定向流
            CreateNoWindow = true,         // 在非 GUI 环境下通常设置为 true
        };

        // 创建并启动进程
        using (var process = new Process { StartInfo = processStartInfo })
        {
            try
            {
                process.Start();

                // 异步读取输出和错误流
                // 这比同步读取更好，可以避免死锁
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                // 等待进程执行完成
                await process.WaitForExitAsync();

                // 获取输出结果
                string output = await outputTask;
                string error = await errorTask;

                Console.WriteLine("-------------------- 命令执行完成 --------------------");

                // 检查退出码
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("命令成功执行。");
                    Console.WriteLine("\n--- 标准输出 (stdout) ---");
                    // 只打印部分输出，因为 HTML 内容可能很长
                    Console.WriteLine(output.Length > 500 ? output.Substring(0, 500) + "..." : output);
                }
                else
                {
                    Console.WriteLine($"命令执行失败，退出码: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("\n--- 标准错误 (stderr) ---");
                        Console.WriteLine(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行命令时发生异常: {ex.Message}");
                // 在这里可以处理一些情况，例如 'curl' 命令不存在
            }
        }

        #endregion
    }
}
