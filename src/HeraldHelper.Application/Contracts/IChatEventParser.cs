using HeraldHelper.Application.Models;

namespace HeraldHelper.Application.Contracts;

public interface IChatEventParser
{
    /// <param name="fallbackTargetName">Target assumed when the text carries
    /// no usable "you target" mention — needed for sources that emit only new
    /// lines per poll (the target line scrolled out of the frame).</param>
    ChatParseResult Parse(string ocrText, string? fallbackTargetName = null);
}
