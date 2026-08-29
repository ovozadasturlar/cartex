namespace Cartex.Application.Common.Interfaces;

using Cartex.Application.Common.Models;
using Cartex.Shared.Models.Common;

public interface IPagingMetadataWriter
{
    void Write(PagedListMetadata metadata);
}
