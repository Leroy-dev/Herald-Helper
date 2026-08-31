namespace HeraldHelper.Domain.Models;

public sealed record IconSpriteRef(
    string SpriteSheet,
    int X,
    int Y,
    int Width,
    int Height,
    int BorderIndex = 0,
    int SpellBadgeIndex = 0,
    int UpLeftCornerIndex = 0,
    int UpCornerIndex = 0,
    int UpRightCornerIndex = 0,
    int RightCornerIndex = 0,
    int DownRightCornerIndex = 0,
    int DownCornerIndex = 0,
    int LeftCornerIndex = 0);
