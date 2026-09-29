namespace Anvil;

public sealed record AnvilMailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);
