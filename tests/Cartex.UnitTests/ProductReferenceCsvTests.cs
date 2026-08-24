using Cartex.Infrastructure.ProductReference;
using Cartex.Application.Common.Settings;
using System.Net;
using System.Text;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ProductReferenceCsvTests
{
    [Fact]
    public void MAKAT_07_Csv_supports_quotes_commas_multiline_bom_and_empty_columns()
    {
        const string csv = "\uFEFFbarcode,name,unit,note\r\n1,\"Water, 1L\",dona,\"line one\r\nline two\"\r\n2,Tea,,\"\"";

        var rows = CsvRecordReader.Parse(new StringReader(csv));

        Assert.Equal(3, rows.Count);
        Assert.Equal("barcode", rows[0][0]);
        Assert.Equal("Water, 1L", rows[1][1]);
        Assert.Equal("line one\r\nline two", rows[1][3]);
        Assert.Equal(string.Empty, rows[2][2]);
        Assert.Equal(string.Empty, rows[2][3]);
    }

    [Fact]
    public async Task MAKAT_07_Custom_column_headers_are_mapped()
    {
        const string csv = "EAN,Tovar,Birlik,Guruh,Brend,Qadoq,Narx\r\n478001,Choy,dona,Ichimlik,Cartex,12,25000";
        var source = new GoogleSheetsProductReferenceSource(new StubHttpClientFactory(csv));
        var config = new ProductReferenceSourceConfig(
            "sheet-id", "Products", "EAN", "Tovar", "Birlik", "Guruh", "Brend", "Qadoq", "Narx");

        var rows = await source.FetchAsync(config, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("478001", row.Barcode);
        Assert.Equal("Choy", row.Name);
        Assert.Equal("dona", row.UnitHint);
        Assert.Equal("Ichimlik", row.CategoryHint);
        Assert.Equal("Cartex", row.ManufacturerHint);
        Assert.Equal(12, row.PackQty);
        Assert.Equal(25000, row.SuggestedPrice);
    }

    private sealed class StubHttpClientFactory(string response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(response));
    }

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "text/csv")
            });
    }
}
