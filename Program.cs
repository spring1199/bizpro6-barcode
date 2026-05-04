using BarTenderClone.Helpers;
using BarTenderClone.Models;
using BarTenderClone.Services;
using Newtonsoft.Json;

namespace TemplateParityProbe;

internal static class Program
{
    private static int Main()
    {
        var rawRfid = "0000000000000000B61F05C9";
        var displayRfid = "B61F05C9";

        var item = new ResourceItem
        {
            ParsedDocument = new ResourceDocument
            {
                Product = new ProductDto
                {
                    Name = "Probe Product",
                    ItemCode = "SKU-1",
                    PriceRaw = 125000
                },
                ProductRfid = new ProductRfidDto
                {
                    Rfid = rawRfid
                }
            }
        };

        var element = new LabelElement
        {
            Type = ElementType.Barcode,
            FieldName = "RFID",
            Content = "{RFID}",
            X = -8,
            Y = 12,
            Width = 226.77,
            Height = 35,
            IsCentered = true
        };

        AssertEqual(displayRfid, LabelFieldValueResolver.ResolveVisualValue(element, item), "visual RFID");
        AssertEqual(rawRfid, LabelFieldValueResolver.ResolveRawValue(element, item), "raw RFID");

        var zpl = new ZplGeneratorService().GenerateZplWithRfid(
            new[] { element },
            item,
            new LabelTemplate { Width = 226.77, Height = 151.18 },
            new RfidConfiguration
            {
                EnableRfidEncoding = true,
                DataFormat = RfidDataFormat.Hexadecimal
            },
            new PrinterConfiguration
            {
                Dpi = 203,
                EnableUtf8 = true
            });

        AssertContains(zpl, $"^FD{displayRfid}^FS", "visual barcode uses stripped RFID");
        AssertContains(zpl, $"^FD{rawRfid}^FS", "RFID encoder uses raw RFID");
        AssertDoesNotContain(zpl, "^FO-", "negative coordinates are clamped before ZPL output");
        AssertEqual("0", LabelFieldValueResolver.StripLeadingZerosForDisplay("0000"), "all-zero RFID display");
        AssertEqual(
            "{RFID}",
            LabelFieldValueResolver.ResolveVisualValue("RFID", new ResourceItem(), "{RFID}"),
            "missing RFID keeps template placeholder");

        var dto = new LabelTemplateDto
        {
            Name = "Roundtrip",
            Elements =
            {
                new LabelElementDto
                {
                    Type = ElementType.Barcode,
                    FieldName = "RFID",
                    IsCentered = true
                }
            }
        };

        var roundTrip = JsonConvert.DeserializeObject<LabelTemplateDto>(
            JsonConvert.SerializeObject(dto))!;
        AssertEqual(true, roundTrip.Elements[0].IsCentered, "template IsCentered roundtrip");

        Console.WriteLine("Template parity probe passed.");
        return 0;
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'.");
    }

    private static void AssertContains(string text, string expected, string name)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"{name}: expected generated ZPL to contain '{expected}'.");
    }

    private static void AssertDoesNotContain(string text, string unexpected, string name)
    {
        if (text.Contains(unexpected, StringComparison.Ordinal))
            throw new InvalidOperationException($"{name}: generated ZPL contained '{unexpected}'.");
    }
}
