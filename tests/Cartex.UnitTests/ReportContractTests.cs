using System.Text.Json;
using System.Text.Json.Serialization;
using Cartex.Shared.Models.Reports;
using Xunit;

namespace Cartex.UnitTests;

/// Hisobot javobini desktop va mobil ilova bir xil qoidalar bilan o'qiydi. Shartnomaga yangi
/// maydon qo'shilganda eski klient ham, yangi klient ham buzilmasligi shart — shuning uchun
/// o'qish ikkala tomonga tekshiriladi.
public class ReportContractTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string WithoutHourly = """
        {
          "revenue": 64670, "profit": 20770, "salesCount": 4,
          "averageSale": 16167.5, "maxSale": 53350,
          "topProducts": [], "daily": [{ "date": "2026-08-21T00:00:00", "revenue": 64670, "profit": 20770, "count": 4 }]
        }
        """;

    private const string WithHourly = """
        {
          "revenue": 64670, "profit": 20770, "salesCount": 4,
          "averageSale": 16167.5, "maxSale": 53350,
          "topProducts": [], "daily": [{ "date": "2026-08-21T00:00:00", "revenue": 64670, "profit": 20770, "count": 4 }],
          "hourly": [
            { "hour": 14, "revenue": 4000, "profit": 1200, "count": 1 },
            { "hour": 15, "revenue": 57670, "profit": 18570, "count": 2 },
            { "hour": 20, "revenue": 3000, "profit": 1000, "count": 1 }
          ]
        }
        """;

    [Fact]
    public void Soatlik_maydon_yoq_javob_ham_oqiladi()
    {
        // Eski server yoki keshdagi javob: yangi maydon yo'q — klient baribir ishlashi kerak.
        var report = JsonSerializer.Deserialize<SalesReportDto>(WithoutHourly, Options);

        Assert.NotNull(report);
        Assert.Equal(64670m, report.Revenue);
        Assert.Single(report.Daily);
        Assert.True(report.Hourly is null || report.Hourly.Count == 0);
    }

    [Fact]
    public void Soatlik_qator_toliq_oqiladi()
    {
        var report = JsonSerializer.Deserialize<SalesReportDto>(WithHourly, Options);

        Assert.NotNull(report);
        Assert.Equal(3, report.Hourly.Count);
        Assert.Equal(15, report.Hourly[1].Hour);
        Assert.Equal(57670m, report.Hourly[1].Revenue);
    }

    [Fact]
    public void Soatlik_yigindi_kunlik_qiymatga_teng()
    {
        // HIS-07 invarianti klient tomonida ham tekshiriladi: ekranda ikki xil son chiqmasin.
        var report = JsonSerializer.Deserialize<SalesReportDto>(WithHourly, Options)!;

        Assert.Equal(report.Daily[0].Revenue, report.Hourly.Sum(h => h.Revenue));
        Assert.Equal(report.Revenue, report.Hourly.Sum(h => h.Revenue));
    }
}
