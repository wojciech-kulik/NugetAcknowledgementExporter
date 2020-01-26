using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ShellHelper
{
    public static string Bash(this string cmd)
    {
        var escapedArgs = cmd.Replace("\"", "\\\"");
        var fileName = "/bin/bash";
        var arguments = $"-c \"{escapedArgs}\"";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        { 
            fileName = "cmd.exe";
            arguments = $"/C \"{escapedArgs}\"";
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return result;
    }
}