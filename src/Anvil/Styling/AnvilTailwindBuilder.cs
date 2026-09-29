using System.Diagnostics;

namespace Anvil;

public sealed class AnvilTailwindBuilder
{
    public async Task BuildAsync(
        string projectDirectory,
        AnvilTailwindOptions options,
        bool minify = false,
        CancellationToken cancellationToken = default)
    {
        var input = Path.GetFullPath(Path.Combine(projectDirectory, options.Input));
        var output = Path.GetFullPath(Path.Combine(projectDirectory, options.Output));
        if (!File.Exists(input))
            throw new FileNotFoundException("Tailwind input file was not found.", input);

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var startInfo = new ProcessStartInfo("tailwindcss")
        {
            WorkingDirectory = projectDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(input);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(output);
        foreach (var content in options.ContentGlobs)
        {
            startInfo.ArgumentList.Add("--content");
            startInfo.ArgumentList.Add(Path.Combine(projectDirectory, content));
        }
        if (minify) startInfo.ArgumentList.Add("--minify");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the tailwindcss executable.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(await process.StandardError.ReadToEndAsync(cancellationToken));
    }
}
