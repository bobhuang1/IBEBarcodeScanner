namespace IBEBarcode.Scanner.Core;

/// <summary>
/// Parses GS1 Application Identifier element strings, in both the compact form (what a GS1-128 barcode
/// carries, with ASCII 29 / FNC1 separators) and the bracketed form <c>(01)09501101530003</c> (what
/// IBEBarcodeGenerator's GS1-128 encoder accepts and prints as human-readable text).
/// </summary>
public static class Gs1AiParser
{
    /// <summary>The FNC1 / group separator character GS1 uses to terminate variable-length elements.</summary>
    public const char GroupSeparator = '\u001D';

    private sealed record AiInfo(int FixedLength, string Description);

    // FixedLength > 0 means the element has that exact length and needs no separator after it.
    // FixedLength == 0 means variable length, terminated by the group separator.
    private static readonly Dictionary<string, AiInfo> KnownAis = new(StringComparer.Ordinal)
    {
        ["00"] = new(18, "SSCC"),
        ["01"] = new(14, "GTIN"),
        ["02"] = new(14, "GTIN of contained trade items"),
        ["10"] = new(0, "Batch / lot number"),
        ["11"] = new(6, "Production date"),
        ["12"] = new(6, "Due date"),
        ["13"] = new(6, "Packaging date"),
        ["15"] = new(6, "Best-before date"),
        ["16"] = new(6, "Sell-by date"),
        ["17"] = new(6, "Expiration date"),
        ["20"] = new(2, "Internal product variant"),
        ["21"] = new(0, "Serial number"),
        ["22"] = new(0, "Consumer product variant"),
        ["30"] = new(0, "Variable count of items"),
        ["37"] = new(0, "Count of trade items"),
        ["235"] = new(0, "Third party controlled, serialised extension of GTIN"),
        ["240"] = new(0, "Additional product identification"),
        ["241"] = new(0, "Customer part number"),
        ["242"] = new(0, "Made-to-order variation number"),
        ["243"] = new(0, "Packaging component number"),
        ["250"] = new(0, "Secondary serial number"),
        ["251"] = new(0, "Reference to source entity"),
        ["253"] = new(0, "Global document type identifier"),
        ["254"] = new(0, "GLN extension component"),
        ["255"] = new(0, "Global coupon number"),
        ["400"] = new(0, "Order number"),
        ["401"] = new(0, "Global identification number for consignment"),
        ["402"] = new(17, "Global shipment identification number"),
        ["403"] = new(0, "Routing code"),
        ["410"] = new(13, "GLN of ship-to location"),
        ["411"] = new(13, "GLN of invoicing party"),
        ["412"] = new(13, "GLN of purchase order"),
        ["413"] = new(13, "GLN of ship-for location"),
        ["414"] = new(13, "GLN of physical location"),
        ["415"] = new(13, "GLN of invoicing party reference"),
        ["416"] = new(13, "GLN of production location"),
        ["417"] = new(13, "GLN of party responsible"),
        ["420"] = new(0, "Ship-to postal code"),
        ["421"] = new(0, "Ship-to postal code with ISO country code"),
        ["422"] = new(3, "Country of origin"),
        ["423"] = new(0, "Country of initial processing"),
        ["424"] = new(3, "Country of processing"),
        ["425"] = new(3, "Country of disassembly"),
        ["426"] = new(3, "Country of full process chain"),
        ["427"] = new(0, "Subdivision of country of origin"),
        ["4300"] = new(35, "Ship-to company name"),
        ["4301"] = new(35, "Ship-to name"),
        ["4302"] = new(35, "Ship-to address line 1"),
        ["4303"] = new(35, "Ship-to address line 2"),
        ["4304"] = new(35, "Ship-to suburb"),
        ["4305"] = new(35, "Ship-to locality"),
        ["4306"] = new(35, "Ship-to region"),
        ["4307"] = new(2, "Ship-to country code"),
        ["4308"] = new(35, "Ship-to telephone number"),
        ["7001"] = new(13, "NATO stock number"),
        ["7002"] = new(0, "Meat cut classification"),
        ["7003"] = new(10, "Expiration date and time"),
        ["7004"] = new(0, "Active potency"),
        ["7005"] = new(0, "Catch area"),
        ["7006"] = new(6, "First freeze date"),
        ["7007"] = new(0, "Harvest date"),
        ["7008"] = new(0, "Species for fishery purposes"),
        ["7009"] = new(0, "Fishing gear"),
        ["7010"] = new(2, "Production method"),
        ["7020"] = new(0, "Refurbishment lot ID"),
        ["7021"] = new(0, "Functional status"),
        ["7022"] = new(0, "Revision status"),
        ["7023"] = new(0, "Global individual asset identifier of an assembly"),
        ["7030"] = new(0, "Processing method"),
        ["7040"] = new(4, "UIC+ extension"),
        ["710"] = new(20, "National healthcare reimbursement number"),
        ["711"] = new(20, "National healthcare reimbursement number"),
        ["712"] = new(20, "National healthcare reimbursement number"),
        ["713"] = new(20, "National healthcare reimbursement number"),
        ["714"] = new(20, "National healthcare reimbursement number"),
        ["715"] = new(20, "National healthcare reimbursement number"),
        ["7230"] = new(0, "Certification reference"),
        ["7240"] = new(0, "Protocol ID"),
        ["8001"] = new(14, "Roll products"),
        ["8002"] = new(0, "Cellular mobile telephone identifier"),
        ["8003"] = new(0, "Global returnable asset identifier"),
        ["8004"] = new(0, "Global individual asset identifier"),
        ["8005"] = new(6, "Price per unit of measure"),
        ["8006"] = new(18, "Identification of the components of a trade item"),
        ["8007"] = new(0, "International bank account number"),
        ["8008"] = new(0, "Date and time of production"),
        ["8009"] = new(0, "Optically readable sensor indicator"),
        ["8010"] = new(0, "Component / part identifier"),
        ["8011"] = new(0, "Component / part identifier serial number"),
        ["8012"] = new(0, "Software version"),
        ["8013"] = new(0, "Global model number"),
        ["8017"] = new(0, "Global service relation number - provider"),
        ["8018"] = new(0, "Global service relation number - recipient"),
        ["8019"] = new(0, "Service relation instance number"),
        ["8020"] = new(0, "Payment slip reference number"),
        ["8026"] = new(0, "Identification of pieces of a trade item"),
        ["8100"] = new(6, "Coupon code - extended"),
        ["8101"] = new(10, "Coupon code - price per unit"),
        ["8102"] = new(2, "Coupon code - extension digit"),
        ["8110"] = new(0, "Coupon code - promotional offer"),
        ["8111"] = new(4, "Coupon code - loyalty points"),
        ["8112"] = new(0, "Coupon code - promotional offer"),
        ["8200"] = new(0, "Product URL"),
        ["90"] = new(0, "Internal company use"),
        ["91"] = new(0, "Internal company use"),
        ["92"] = new(0, "Internal company use"),
        ["93"] = new(0, "Internal company use"),
        ["94"] = new(0, "Internal company use"),
        ["95"] = new(0, "Internal company use"),
        ["96"] = new(0, "Internal company use"),
        ["97"] = new(0, "Internal company use"),
        ["98"] = new(0, "Internal company use"),
        ["99"] = new(0, "Internal company use"),
    };

    /// <summary>Parses either notation. Returns an empty list when nothing GS1-like is present.</summary>
    public static IReadOnlyList<Gs1Element> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var value = text.Trim().TrimStart(GroupSeparator);

        if (value.Contains('(') && value.Contains(')'))
            return ParseBracketed(value);

        // Every element string starts with a numeric Application Identifier, so free text is not parsed
        // into a single bogus element.
        if (value.Length == 0 || !char.IsAsciiDigit(value[0]))
            return [];

        return ParseCompact(value);
    }

    /// <summary>
    /// Loose check used by <see cref="PayloadClassifier"/> for symbologies that may carry GS1 data.
    /// True for bracketed notation, for content with group separators, or for a leading known AI whose
    /// fixed length is satisfied.
    /// </summary>
    public static bool LooksLikeGs1(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var value = text.Trim();

        if (value.Contains(GroupSeparator, StringComparison.Ordinal) && value.Length > 3)
            return true;

        if (value.Length > 4 && value[0] == '(' && char.IsDigit(value[1]) && value.Contains(')'))
            return true;

        return StartsWithKnownFixedAi(value);
    }

    private static bool StartsWithKnownFixedAi(string value)
    {
        foreach (var length in (int[])[4, 3, 2])
        {
            if (value.Length < length + 1)
                continue;

            var ai = value[..length];
            if (!KnownAis.TryGetValue(ai, out var info))
                continue;

            // Variable-length AIs need a separator to be unambiguous, so only fixed-length ones count.
            if (info.FixedLength > 0 && value.Length - length >= info.FixedLength)
                return true;
        }

        return false;
    }

    private static IReadOnlyList<Gs1Element> ParseCompact(string value)
    {
        var elements = new List<Gs1Element>();
        var segments = value.Split(GroupSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var position = 0;

            while (position < segment.Length)
            {
                if (!TryMatchAi(segment, position, out var ai, out var info))
                {
                    // Unknown AI: consume the remainder of the element rather than dropping data.
                    var unknownLength = Math.Min(4, segment.Length - position);
                    elements.Add(new Gs1Element(segment.Substring(position, unknownLength), null, segment[(position + unknownLength)..]));
                    break;
                }

                position += ai.Length;

                if (info!.FixedLength > 0)
                {
                    var take = Math.Min(info.FixedLength, segment.Length - position);
                    elements.Add(new Gs1Element(ai, info.Description, segment.Substring(position, take)));
                    position += take;
                }
                else
                {
                    // Variable-length element: runs to the end of this separator-delimited segment.
                    elements.Add(new Gs1Element(ai, info.Description, segment[position..]));
                    position = segment.Length;
                }
            }
        }

        return elements;
    }

    private static IReadOnlyList<Gs1Element> ParseBracketed(string value)
    {
        var elements = new List<Gs1Element>();
        var position = 0;

        while (position < value.Length)
        {
            if (value[position] != '(')
            {
                position++;
                continue;
            }

            var close = value.IndexOf(')', position);
            if (close < 0)
                break;

            var ai = value[(position + 1)..close];
            if (ai.Length is < 2 or > 4 || !ai.All(char.IsAsciiDigit))
            {
                position = close + 1;
                continue;
            }

            var next = value.IndexOf('(', close + 1);
            var end = next < 0 ? value.Length : next;
            var elementValue = value[(close + 1)..end].Trim(GroupSeparator, ' ');
            elements.Add(new Gs1Element(ai, KnownAis.GetValueOrDefault(ai)?.Description, elementValue));

            position = end;
        }

        return elements;
    }

    private static bool TryMatchAi(string value, int position, out string ai, out AiInfo? info)
    {
        foreach (var length in (int[])[4, 3, 2])
        {
            if (position + length > value.Length)
                continue;

            var candidate = value.Substring(position, length);
            if (!candidate.All(char.IsAsciiDigit))
                continue;

            if (KnownAis.TryGetValue(candidate, out var found))
            {
                ai = candidate;
                info = found;
                return true;
            }
        }

        ai = string.Empty;
        info = null;
        return false;
    }
}
