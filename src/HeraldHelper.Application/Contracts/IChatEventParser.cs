using HeraldHelper.Application.Models;

namespace HeraldHelper.Application.Contracts;

public interface IChatEventParser
{
    ChatParseResult Parse(string ocrText);
}
