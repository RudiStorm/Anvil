using Microsoft.AspNetCore.Http;

namespace Anvil;

public sealed record AnvilMultipartFile(
    string Name,
    string FileName,
    string ContentType,
    long Length,
    IFormFile File)
{
    public Stream OpenReadStream() => File.OpenReadStream();
}
