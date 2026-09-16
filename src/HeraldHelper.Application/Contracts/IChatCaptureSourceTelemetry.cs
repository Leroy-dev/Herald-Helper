namespace HeraldHelper.Application.Contracts;

/// <summary>Reports which underlying capture source produced the most recent
/// chat-region text ("memory", "bt-relay", "chat.log", "OCR"). Wrappers that
/// defer to an inner service propagate the inner source's value.</summary>
public interface IChatCaptureSourceTelemetry
{
    string LastChatSource { get; }
}
