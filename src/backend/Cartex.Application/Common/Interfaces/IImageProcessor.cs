namespace Cartex.Application.Common.Interfaces;

public record ProcessedImage(byte[] Display, byte[] Thumb, string ContentType, string Extension);

public interface IImageProcessor
{
    ProcessedImage? Process(Stream original);
}
