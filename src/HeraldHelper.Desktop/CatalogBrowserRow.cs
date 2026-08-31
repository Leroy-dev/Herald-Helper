using System.ComponentModel;
using System.Windows.Media;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal sealed class CatalogBrowserRow
{
    private static readonly IconImageLoader IconLoader = new();
    private ImageSource? _iconImage;
    private bool _iconLoaded;
    public required string EntryKey { get; init; }
    public required string Name { get; init; }
    public required string EntryType { get; init; }
    public required string ClassName { get; init; }
    public required string Category { get; init; }
    public required string Summary { get; init; }
    public required string Details { get; init; }
    public required string SearchText { get; init; }
    public required string CastTimeDisplay { get; init; }
    public IconSpriteRef? Icon { get; init; }
    public ImageSource? IconImage
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconImage = IconLoader.Load(Icon);
                _iconLoaded = true;
            }
            return _iconImage;
        }
    }
}
