using HookBridge.Application.DTOs.Common;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class PaginationDtosTests
{
    [Fact]
    public void Defaults_AreApplied()
    {
        var request = new PagedRequestDto();

        Assert.Equal(1, request.NormalizedPageNumber);
        Assert.Equal(50, request.NormalizedPageSize);
        Assert.Equal("desc", request.NormalizedSortDirection);
    }

    [Fact]
    public void PageSize_IsCappedAt500()
    {
        var request = new PagedRequestDto { PageSize = 1000 };

        Assert.Equal(500, request.NormalizedPageSize);
    }

    [Fact]
    public void Response_ComputesPagingFlags()
    {
        var response = PagedResponseDto<int>.Create([1, 2], 2, 2, 5);

        Assert.Equal(5, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.True(response.HasPreviousPage);
        Assert.True(response.HasNextPage);
    }

    [Fact]
    public void InvalidSortDirection_DefaultsToDesc()
    {
        var request = new PagedRequestDto { SortDirection = "invalid" };

        Assert.Equal("desc", request.NormalizedSortDirection);
    }
}
