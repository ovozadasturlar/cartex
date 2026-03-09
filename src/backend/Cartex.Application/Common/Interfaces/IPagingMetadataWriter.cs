namespace Cartex.Application.Common.Interfaces;

using Cartex.Application.Common.Models;

public interface IPagingMetadataWriter
{
    void Write(PagedListMetadata metadata);
}
