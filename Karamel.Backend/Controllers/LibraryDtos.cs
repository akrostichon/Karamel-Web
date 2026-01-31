using System;
using System.Collections.Generic;

namespace Karamel.Backend.Controllers
{
    public record SongListItemDto(Guid Id, Guid SessionId, string Artist, string Title, string? MetadataJson, DateTime AddedAt);
    public record SongUploadDto(Guid Id, string Artist, string Title, string? MetadataJson);
    public record PagedResult<T>(IEnumerable<T> Items, int Page, int PageSize, long TotalCount);
}
