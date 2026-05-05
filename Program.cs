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
        var rotatedText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "Rotated",
            X = 20,
            Y = 55,
            Width = 120,
            Height = 30,
            FontSize = 12,
            RotationDegrees = 90
        };
        var rotatedQr = new LabelElement
        {
            Type = ElementType.QRCode,
            Content = "QR",
            X = 60,
            Y = 70,
            Width = 40,
            Height = 40,
            RotationDegrees = 270
        };
        var imageElement = new LabelElement
        {
            Type = ElementType.Image,
            X = 110,
            Y = 80,
            Width = 32,
            Height = 32,
            RotationDegrees = 180,
            ImageDataBase64 = CreateProbeImageBase64(),
            ImageMimeType = "image/png",
            ImageFileName = "probe.png"
        };

        AssertEqual(displayRfid, LabelFieldValueResolver.ResolveVisualValue(element, item), "visual RFID");
        AssertEqual(rawRfid, LabelFieldValueResolver.ResolveRawValue(element, item), "raw RFID");

        var zpl = new ZplGeneratorService().GenerateZplWithRfid(
            new[] { element, rotatedText, rotatedQr, imageElement },
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
        AssertContains(zpl, "^A0R", "text rotation uses ZPL native 90-degree orientation");
        AssertContains(zpl, "^BQB", "QR rotation uses ZPL native 270-degree orientation");
        AssertContains(zpl, "^GFA", "image element emits inline ZPL graphic data");
        AssertEqual("0", LabelFieldValueResolver.StripLeadingZerosForDisplay("0000"), "all-zero RFID display");
        AssertEqual(
            "{RFID}",
            LabelFieldValueResolver.ResolveVisualValue("RFID", new ResourceItem(), "{RFID}"),
            "missing RFID keeps template placeholder");

        var priceFallbackItem = new ResourceItem
        {
            ParsedDocument = new ResourceDocument
            {
                Product = new ProductDto
                {
                    CostRaw = 0,
                    DiscountPriceRaw = 121000,
                    PriceRaw = 99000,
                    CurrencyRaw = "88,000 MNT"
                }
            }
        };

        AssertEqual(121000m, priceFallbackItem.Price, "display price prefers discountPrice over zero cost");
        AssertEqual(0m, priceFallbackItem.Cost, "cost remains separate from display price");

        var formattedPriceItem = new ResourceItem
        {
            ParsedDocument = new ResourceDocument
            {
                Product = new ProductDto
                {
                    CostRaw = 0,
                    PriceRaw = "121,000 MNT"
                }
            }
        };

        AssertEqual(121000m, formattedPriceItem.Price, "display price parses formatted price strings");

        var dto = new LabelTemplateDto
        {
            Name = "Roundtrip",
            Elements =
            {
                new LabelElementDto
                {
                    Type = ElementType.Barcode,
                    FieldName = "RFID",
                    IsCentered = true,
                    RotationDegrees = 90,
                    ImageDataBase64 = imageElement.ImageDataBase64,
                    ImageMimeType = imageElement.ImageMimeType,
                    ImageFileName = imageElement.ImageFileName
                }
            }
        };

        var roundTrip = JsonConvert.DeserializeObject<LabelTemplateDto>(
            JsonConvert.SerializeObject(dto))!;
        AssertEqual(true, roundTrip.Elements[0].IsCentered, "template IsCentered roundtrip");
        AssertEqual(90, roundTrip.Elements[0].RotationDegrees, "template RotationDegrees roundtrip");
        AssertEqual(imageElement.ImageDataBase64, roundTrip.Elements[0].ImageDataBase64, "template embedded image roundtrip");

        Console.WriteLine("Template parity probe passed.");
        return 0;
    }

    private static string CreateProbeImageBase64()
    {
        using var bitmap = new System.Drawing.Bitmap(2, 2);
        bitmap.SetPixel(0, 0, System.Drawing.Color.Black);
        bitmap.SetPixel(1, 0, System.Drawing.Color.White);
        bitmap.SetPixel(0, 1, System.Drawing.Color.White);
        bitmap.SetPixel(1, 1, System.Drawing.Color.Black);

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
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
