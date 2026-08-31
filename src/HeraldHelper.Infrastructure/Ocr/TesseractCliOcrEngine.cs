using System.Diagnostics;
using System.Text;

namespace HeraldHelper.Infrastructure.Ocr;

public sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string _tesseractExe;
    public string Name => "Tesseract";

    public TesseractCliOcrEngine(string? tesseractExe = null)
    {
        _tesseractExe = ResolveExecutable(tesseractExe);
    }

    public async Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
    {
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

    private static string ResolveExecutable(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return requestedPath;
        }

        var knownPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
        if (File.Exists(knownPath))
        {
            return knownPath;
        }

        return "tesseract.exe";
    }
}
