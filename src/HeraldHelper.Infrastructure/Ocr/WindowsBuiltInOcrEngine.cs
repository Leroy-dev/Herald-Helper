using Windows.Graphics.Imaging;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.Storage;

namespace HeraldHelper.Infrastructure.Ocr;

public sealed class WindowsBuiltInOcrEngine : IOcrEngine
{
    public string Name => "WindowsBuiltIn";

    public async Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(cancellationToken);
        using var stream = await file.OpenReadAsync().AsTask(cancellationToken);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        var bitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken);

        // DAoC's UI vocabulary is English even on German Windows installations.
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("Windows OCR engine is unavailable on this machine.");

        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        return result.Text ?? string.Empty;
    }
}
