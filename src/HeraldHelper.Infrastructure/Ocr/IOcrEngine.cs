namespace HeraldHelper.Infrastructure.Ocr;

public interface IOcrEngine
{
    string Name { get; }
    Task<string> ReadTextAsync(string imagePath, CancellationToken cancellationToken);
}
