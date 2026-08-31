using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Desktop;

internal sealed class IconImageLoader
{
    private const int BaseIconTileSize = 32;
    private const int CornerTileSize = 10;
    private const int SpellBadgeWidth = 20;
    private const int SpellBadgeHeight = 12;
    private readonly Dictionary<string, BitmapImage> _sheetCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _spriteRoot;

    public ImageSource? Load(IconSpriteRef? icon)
    {
        if (icon is null)
        {
            return null;
        }

        try
        {
            var spritePath = ResolveSpritePath(icon.SpriteSheet);
            if (spritePath is null || icon.Width <= 0 || icon.Height <= 0)
            {
                return null;
            }

            if (!File.Exists(spritePath))
            {
                return null;
            }

            var sheet = LoadSheet(spritePath);
            var baseRect = new Int32Rect(icon.X * icon.Width, icon.Y * icon.Height, icon.Width, icon.Height);
            if (!IsValidRect(sheet, baseRect))
            {
                return null;
            }

            var baseIcon = new CroppedBitmap(sheet, baseRect);
            baseIcon.Freeze();

            var group = new DrawingGroup();
            group.Children.Add(new ImageDrawing(baseIcon, new Rect(0, 0, icon.Width, icon.Height)));

            if (icon.SpriteSheet.StartsWith("blackthorn/", StringComparison.OrdinalIgnoreCase))
            {
                return baseIcon;
            }

            var spriteRoot = ResolveSpriteRoot();
            if (spriteRoot is null)
            {
                return baseIcon;
            }

            var borderSheetPath = Path.Combine(spriteRoot, "icon_borders.png");
            if (File.Exists(borderSheetPath))
            {
                AddTile(
                    group,
                    LoadSheet(borderSheetPath),
                    icon.BorderIndex,
                    BaseIconTileSize,
                    BaseIconTileSize,
                    new Rect(0, 0, icon.Width, icon.Height));
            }

            var badgeSheetPath = Path.Combine(spriteRoot, "icon_spells.png");
            if (File.Exists(badgeSheetPath))
            {
                AddTile(
                    group,
                    LoadSheet(badgeSheetPath),
                    icon.SpellBadgeIndex,
                    SpellBadgeWidth,
                    SpellBadgeHeight,
                    new Rect(12, 20, SpellBadgeWidth, SpellBadgeHeight));
            }

            var cornerSheetPath = Path.Combine(spriteRoot, "icon_corners.png");
            if (File.Exists(cornerSheetPath))
            {
                var cornerSheet = LoadSheet(cornerSheetPath);
                AddTile(group, cornerSheet, icon.UpLeftCornerIndex, CornerTileSize, CornerTileSize, new Rect(0, 0, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.UpCornerIndex, CornerTileSize, CornerTileSize, new Rect(11, 0, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.UpRightCornerIndex, CornerTileSize, CornerTileSize, new Rect(22, 0, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.RightCornerIndex, CornerTileSize, CornerTileSize, new Rect(22, 11, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.DownRightCornerIndex, CornerTileSize, CornerTileSize, new Rect(22, 22, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.DownCornerIndex, CornerTileSize, CornerTileSize, new Rect(11, 22, CornerTileSize, CornerTileSize));
                AddTile(group, cornerSheet, icon.LeftCornerIndex, CornerTileSize, CornerTileSize, new Rect(0, 11, CornerTileSize, CornerTileSize));
            }

            if (group.Children.Count == 1)
            {
                return baseIcon;
            }

            group.Freeze();

            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private BitmapImage LoadSheet(string path)
    {
        if (_sheetCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var sheet = new BitmapImage();
        sheet.BeginInit();
        sheet.CacheOption = BitmapCacheOption.OnLoad;
        sheet.UriSource = new Uri(path, UriKind.Absolute);
        sheet.EndInit();
        sheet.Freeze();
        _sheetCache[path] = sheet;
        return sheet;
    }

    private string? ResolveSpriteRoot()
    {
        if (!string.IsNullOrWhiteSpace(_spriteRoot))
        {
            return _spriteRoot;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "eden-charplan", "assets", "sprites");
            if (Directory.Exists(candidate))
            {
                _spriteRoot = candidate;
                return _spriteRoot;
            }

            current = current.Parent;
        }

        return null;
    }

    private string? ResolveSpritePath(string spriteSheet)
    {
        if (!spriteSheet.StartsWith("blackthorn/", StringComparison.OrdinalIgnoreCase))
        {
            var root = ResolveSpriteRoot();
            return root is null ? null : Path.Combine(root, spriteSheet);
        }

        var relativePath = spriteSheet["blackthorn/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "blackthorn-charplan", "assets", "icons", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }
        return null;
    }

    private static bool IsValidRect(BitmapSource source, Int32Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || rect.X < 0 || rect.Y < 0)
        {
            return false;
        }

        return rect.X + rect.Width <= source.PixelWidth &&
               rect.Y + rect.Height <= source.PixelHeight;
    }

    private static void AddTile(
        DrawingGroup group,
        BitmapSource sheet,
        int oneBasedIndex,
        int tileWidth,
        int tileHeight,
        Rect destination)
    {
        if (oneBasedIndex <= 0)
        {
            return;
        }

        var zeroBasedIndex = oneBasedIndex - 1;
        var columns = sheet.PixelWidth / tileWidth;
        if (columns <= 0)
        {
            return;
        }

        var tileRect = new Int32Rect(
            (zeroBasedIndex % columns) * tileWidth,
            (zeroBasedIndex / columns) * tileHeight,
            tileWidth,
            tileHeight);
        if (!IsValidRect(sheet, tileRect))
        {
            return;
        }

        var tile = new CroppedBitmap(sheet, tileRect);
        tile.Freeze();
        group.Children.Add(new ImageDrawing(tile, destination));
    }
}
