using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common.Exceptions;
using Xunit;

namespace Cartex.UnitTests;

public class QueryExtensionsTests
{
    private enum Status { Active, Inactive }

    private class Item
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int Qty { get; set; }
        public DateTime CreatedAt { get; set; }
        public Status Status { get; set; }
        public string? PasswordHash { get; set; }
    }

    private static readonly List<Item> Data =
    [
        new() { Id = 1, Name = "Apple juice", Qty = 5, CreatedAt = new DateTime(2025, 12, 31), Status = Status.Active, PasswordHash = "secret" },
        new() { Id = 2, Name = "Apricot jam", Qty = 10, CreatedAt = new DateTime(2026, 6, 15, 10, 0, 0), Status = Status.Active },
        new() { Id = 3, Name = "Banana", Qty = 15, CreatedAt = new DateTime(2026, 6, 20), Status = Status.Inactive },
        new() { Id = 4, Name = "Cherry", Qty = 20, CreatedAt = new DateTime(2026, 7, 1), Status = Status.Inactive },
    ];

    private static FilteringRequest Req(Dictionary<string, List<string>>? filters = null, string? search = null, double? tz = null)
        => new() { Filters = filters, Search = search, TimeZone = tz, SortBy = "Id" };

    private static Dictionary<string, List<string>> F(string key, params string[] vals)
        => new() { [key] = [.. vals] };

    private static List<Item> Run(FilteringRequest req) => Data.AsQueryable().AsFilterable(req).ToList();

    [Fact]
    public void Equality_Numeric()
    {
        var r = Run(Req(F("Qty", "10")));
        Assert.Equal([2], r.Select(x => x.Id));
    }

    [Fact]
    public void GreaterThan_Numeric()
    {
        var r = Run(Req(F("Qty", ">10")));
        Assert.Equal([3, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void Equals_Prefix_Numeric()
    {
        var r = Run(Req(F("Qty", "equals:15")));
        Assert.Equal([3], r.Select(x => x.Id));
    }

    [Fact]
    public void Not_Operator()
    {
        var r = Run(Req(F("Qty", "!10")));
        Assert.Equal([1, 3, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void In_List_Numeric()
    {
        var r = Run(Req(F("Qty", "in:5,15")));
        Assert.Equal([1, 3], r.Select(x => x.Id));
    }

    [Fact]
    public void In_List_Enum()
    {
        var r = Run(Req(F("Status", "in:Inactive")));
        Assert.Equal([3, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void Or_Logic_PipeToken()
    {
        var r = Run(Req(F("Qty", "5", "|", "20")));
        Assert.Equal([1, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void Contains_String_CaseInsensitive()
    {
        var r = Run(Req(F("Name", "contains:AP")));
        Assert.Equal([1, 2], r.Select(x => x.Id));
    }

    [Fact]
    public void Starts_String()
    {
        var r = Run(Req(F("Name", "starts:Apri")));
        Assert.Equal([2], r.Select(x => x.Id));
    }

    [Fact]
    public void Date_Day_Range()
    {
        var r = Run(Req(F("CreatedAt", "2026-06-15")));
        Assert.Equal([2], r.Select(x => x.Id));
    }

    [Fact]
    public void Date_Month_Range()
    {
        var r = Run(Req(F("CreatedAt", "2026-06")));
        Assert.Equal([2, 3], r.Select(x => x.Id));
    }

    [Fact]
    public void Date_Year_Range()
    {
        var r = Run(Req(F("CreatedAt", "2026")));
        Assert.Equal([2, 3, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void Date_GreaterThan()
    {
        var r = Run(Req(F("CreatedAt", ">2026-06-15")));
        Assert.Equal([3, 4], r.Select(x => x.Id));
    }

    [Fact]
    public void SensitiveProperty_Filter_Ignored()
    {
        var r = Run(Req(F("PasswordHash", "secret")));
        Assert.Equal(4, r.Count);
    }

    [Fact]
    public void GlobalSearch_StringProps_SkipsSensitive()
    {
        var r = Run(Req(search: "secret"));
        Assert.Empty(r);
    }

    [Fact]
    public void GlobalSearch_MatchesName()
    {
        var r = Run(Req(search: "ap"));
        Assert.Equal([1, 2], r.Select(x => x.Id));
    }

    [Fact]
    public void Sort_Descending()
    {
        var req = new FilteringRequest { SortBy = "Qty", Descending = true };
        var r = Data.AsQueryable().AsFilterable(req).ToList();
        Assert.Equal([4, 3, 2, 1], r.Select(x => x.Id));
    }

    [Fact]
    public void BadDate_Throws_BusinessRule()
    {
        Assert.Throws<BusinessRuleException>(() => Run(Req(F("CreatedAt", "not-a-date"))));
    }

    [Fact]
    public void BadInListValue_Throws_BusinessRule()
    {
        Assert.Throws<BusinessRuleException>(() => Run(Req(F("Qty", "in:5,xx,15"))));
    }
}
