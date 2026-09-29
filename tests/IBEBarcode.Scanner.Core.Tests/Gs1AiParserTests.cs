namespace IBEBarcode.Scanner.Core.Tests;

public class Gs1AiParserTests
{
    [Fact]
    public void Parse_FixedLengthAisWithoutSeparators_SplitsOnDeclaredLengths()
    {
        var elements = Gs1AiParser.Parse("010950110153000317251231");

        Assert.Equal(2, elements.Count);
        Assert.Equal(new Gs1Element("01", "GTIN", "09501101530003"), elements[0]);
        Assert.Equal(new Gs1Element("17", "Expiration date", "251231"), elements[1]);
    }

    [Fact]
    public void Parse_VariableLengthAi_TakesTheRestOfTheSegment()
    {
        var elements = Gs1AiParser.Parse($"0109501101530003\u001D10LOT123");

        Assert.Equal(2, elements.Count);
        Assert.Equal("LOT123", elements[1].Value);
        Assert.Equal("Batch / lot number", elements[1].Description);
    }

    [Fact]
    public void Parse_VariableThenFixedAiAfterSeparator_ContinuesParsing()
    {
        var elements = Gs1AiParser.Parse($"0109501101530003\u001D10LOT123\u001D17251231");

        Assert.Equal(3, elements.Count);
        Assert.Equal("LOT123", elements[1].Value);
        Assert.Equal("251231", elements[2].Value);
    }

    [Fact]
    public void Parse_LeadingGroupSeparator_IsIgnored()
    {
        var elements = Gs1AiParser.Parse("\u001D0109501101530003");

        Assert.Single(elements);
        Assert.Equal("01", elements[0].Ai);
    }

    [Fact]
    public void Parse_SeriesAndSerialNumber_ResolveCorrectly()
    {
        var elements = Gs1AiParser.Parse("010950110153000321SN-0001");

        Assert.Equal(2, elements.Count);
        Assert.Equal("09501101530003", elements[0].Value);
        Assert.Equal("SN-0001", elements[1].Value);
    }

    [Fact]
    public void Parse_SsccAi00_Takes18Digits()
    {
        var elements = Gs1AiParser.Parse("00123456789012345675");

        Assert.Single(elements);
        Assert.Equal("00", elements[0].Ai);
        Assert.Equal("123456789012345675", elements[0].Value);
    }

    [Fact]
    public void Parse_BracketedNotation_IsReadable()
    {
        var elements = Gs1AiParser.Parse("(01)09501101530003(17)251231(10)LOT-42");

        Assert.Equal(3, elements.Count);
        Assert.Equal("LOT-42", elements[2].Value);
        Assert.Equal("(01)09501101530003", elements[0].ToBracketedString());
    }

    [Fact]
    public void Parse_InternalUseAi_IsRecognized()
    {
        var elements = Gs1AiParser.Parse("9900INTERNAL-DATA");

        Assert.Single(elements);
        Assert.Equal("99", elements[0].Ai);
        Assert.Equal("Internal company use", elements[0].Description);
        Assert.Equal("00INTERNAL-DATA", elements[0].Value);
    }

    [Fact]
    public void Parse_UnknownAi_KeepsTheDataInsteadOfDroppingIt()
    {
        var elements = Gs1AiParser.Parse("5999XYZ");

        Assert.Single(elements);
        Assert.Equal("5999", elements[0].Ai);
        Assert.Null(elements[0].Description);
        Assert.Equal("XYZ", elements[0].Value);
    }

    [Fact]
    public void Parse_EmptyOrNonGs1Text_ReturnsEmpty()
    {
        Assert.Empty(Gs1AiParser.Parse(null));
        Assert.Empty(Gs1AiParser.Parse("   "));
        Assert.Empty(Gs1AiParser.Parse("not a gs1 string"));
    }

    [Theory]
    [InlineData("010950110153000317251231", true)]
    [InlineData("(01)09501101530003", true)]
    [InlineData("0109501101530003", true)]
    [InlineData("00123456789012345675", true)]
    [InlineData("hello world", false)]
    [InlineData("5555555555", false)]
    // "1234567890" starts with AI 12 (due date, fixed 6 digits), which the loose heuristic accepts.
    [InlineData("1234567890", true)]
    public void LooksLikeGs1_ClassifiesRepresentativeInputs(string text, bool expected)
    {
        Assert.Equal(expected, Gs1AiParser.LooksLikeGs1(text));
    }
}
