using System.Diagnostics;
using System.Text;

namespace HeraldHelper.Infrastructure.Ocr;

public sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string? _tesseractExe;
    public string Name => "Tesseract";

    public TesseractCliOcrEngine(string? tesseractExe = null)
    {
        _tesseractExe = ResolveExecutable(tesseractExe);
    }

    public async Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        if (_tesseractExe is null)
        {
            throw new InvalidOperationException(
                "Unable to start Tesseract OCR. Install Tesseract, put tesseract.exe on PATH, " +
                "or bundle tools\\tesseract\\tesseract.exe next to the app.");
        }

        var args = $"\"{imagePath}\" stdout --dpi 96";
        var psi = new ProcessStartInfo
        {
            FileName = _tesseractExe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Unable to start Tesseract OCR. Install Tesseract and ensure tesseract.exe is on PATH.",
                ex);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Tesseract failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    /// <summary>True when a tesseract.exe can be located (bundled, installed, or on PATH).</summary>
    public static bool IsAvailable(string? requestedPath = null) =>
        ResolveExecutable(requestedPath) is not null;

    private static string? ResolveExecutable(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return requestedPath;
        }

        // Release bundles may ship a slim Tesseract beside the app; its
        // tessdata resolves automatically relative to the exe.
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "tesseract", "tesseract.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        var knownPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
        if (File.Exists(knownPath))
        {
            return knownPath;
        }

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "tesseract.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
