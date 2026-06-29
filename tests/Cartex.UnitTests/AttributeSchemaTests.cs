using Cartex.Application.Common.Catalog;
using Cartex.Domain.Common.Exceptions;
using Xunit;

namespace Cartex.UnitTests;

public class AttributeSchemaTests
{
    private const string Schema = """
        [
          {"key":"hajm","label":"Hajm","type":"select","required":true,"options":["0.5L","1L"]},
          {"key":"organik","label":"Organik","type":"bool","required":false},
          {"key":"og'irlik","label":"Og'irlik","type":"number","required":false}
        ]
        """;

    [Fact]
    public void NoSchema_DoesNotThrow() => AttributeSchema.Validate(null, """{"x":"y"}""");

    [Fact]
    public void ValidValues_Pass() =>
        AttributeSchema.Validate(Schema, """{"hajm":"1L","organik":true,"og'irlik":1.5}""");

    [Fact]
    public void MissingRequired_Throws() =>
        Assert.Throws<BusinessRuleException>(() => AttributeSchema.Validate(Schema, """{"organik":true}"""));

    [Fact]
    public void SelectOutOfOptions_Throws() =>
        Assert.Throws<BusinessRuleException>(() => AttributeSchema.Validate(Schema, """{"hajm":"2L"}"""));

    [Fact]
    public void WrongType_Throws() =>
        Assert.Throws<BusinessRuleException>(() => AttributeSchema.Validate(Schema, """{"hajm":"1L","og'irlik":"katta"}"""));

    [Fact]
    public void Parse_ReturnsFields() =>
        Assert.Equal(3, AttributeSchema.Parse(Schema).Count);
}
