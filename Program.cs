using BarTenderClone.Helpers;
using BarTenderClone.Models;
using BarTenderClone.Services;
using Newtonsoft.Json;

namespace TemplateParityProbe;

internal static class Program
{
    [STAThread]
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

        var legacyConfig = new PrinterConfiguration
        {
            Dpi = 203,
            EnableUtf8 = true,
            RenderMode = PrintRenderMode.LegacyNativeZpl
        };

        var zpl = new ZplGeneratorService().GenerateZplWithRfid(
            new[] { element, rotatedText, rotatedQr, imageElement },
            item,
            new LabelTemplate { Width = 226.77, Height = 151.18 },
            new RfidConfiguration
            {
                EnableRfidEncoding = true,
                DataFormat = RfidDataFormat.Hexadecimal
            },
            legacyConfig);

        AssertContains(zpl, $"^FD{displayRfid}^FS", "visual barcode uses stripped RFID");
        AssertContains(zpl, $"^FD{rawRfid}^FS", "RFID encoder uses raw RFID");
        AssertDoesNotContain(zpl, "^FO-", "negative coordinates are clamped before ZPL output");
        AssertContains(zpl, "^A0R", "text rotation uses ZPL native 90-degree orientation");
        AssertContains(zpl, "^BQB", "QR rotation uses ZPL native 270-degree orientation");
        AssertContains(zpl, "^GFA", "image element emits inline ZPL graphic data");
        var rasterZpl = new ZplGeneratorService().GenerateZplWithRfid(
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

        AssertContains(rasterZpl, "^GFA", "default WYSIWYG raster mode emits full-label graphic");
        AssertDoesNotContain(rasterZpl, "^BC", "default WYSIWYG raster mode does not emit native barcode visual command");
        AssertContains(rasterZpl, $"^FD{rawRfid}^FS", "RFID encoder remains native in raster mode");
        AssertContains(rasterZpl, "^PW", "raster mode emits print width");
        AssertContains(rasterZpl, "^LL", "raster mode emits label length");
        AssertContains(
            LabelImageHelper.GenerateZplGraphic(CreateRectangularProbeImageBase64(), 16, 8, 90),
            "^GFA,16,16,2,",
            "rotated image remains fitted to requested element width");
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

        var geometryText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "CCW21-8012b-0001/0520/052",
            X = 18,
            Y = 24,
            Width = 150,
            Height = 0,
            FontSize = 9,
            RotationDegrees = 90
        };
        var local = DesignerInteractionHelper.GetLocalSize(geometryText);
        var visualBounds = DesignerInteractionHelper.GetVisualBounds(geometryText);
        AssertAlmost(local.Height, visualBounds.Width, "90-degree visual width swaps from local height");
        AssertAlmost(local.Width, visualBounds.Height, "90-degree visual height swaps from local width");
        AssertAlmost(geometryText.X + local.Width / 2, visualBounds.Left + visualBounds.Width / 2, "rotation preserves center X");
        AssertAlmost(geometryText.Y + local.Height / 2, visualBounds.Top + visualBounds.Height / 2, "rotation preserves center Y");

        var explicitShortText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "MNT 395,000",
            X = 20,
            Y = 20,
            Width = 24,
            Height = 16,
            FontSize = 12,
            IsBold = true,
            RotationDegrees = 90
        };
        var explicitShortLocal = DesignerInteractionHelper.GetLocalSize(explicitShortText);
        AssertTrue(
            explicitShortLocal.Height > explicitShortText.Height,
            "explicit-height rotated text expands to measured content height");
        AssertTrue(
            explicitShortLocal.Width > explicitShortText.Width,
            "explicit-width rotated text expands to measured word width");

        var narrowCyrillicText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "Богино ханцуйтай хар цамц",
            X = 20,
            Y = 20,
            Width = 24,
            Height = 24,
            FontSize = 6,
            RotationDegrees = 90
        };
        var narrowCyrillicLocal = DesignerInteractionHelper.GetLocalSize(narrowCyrillicText);
        AssertTrue(
            narrowCyrillicLocal.Width > narrowCyrillicText.Width,
            "narrow rotated Cyrillic text expands to longest word width");
        AssertTrue(
            narrowCyrillicLocal.Height > narrowCyrillicText.Height,
            "narrow rotated Cyrillic text expands to wrapped measured height");

        var topLeftAfterRotation = DesignerInteractionHelper.GetChromePoint(
            geometryText.Width,
            geometryText.Height,
            geometryText.RotationDegrees,
            geometryText.Type,
            geometryText.FontSize,
            geometryText.Content,
            ResizeHandleDirection.TopLeft);
        AssertAlmost(local.Height, topLeftAfterRotation.X, "90-degree top-left handle maps to right edge of visual bounds");
        AssertAlmost(0, topLeftAfterRotation.Y, "90-degree top-left handle maps to top edge of visual bounds");

        var visualBefore = DesignerInteractionHelper.GetVisualBounds(geometryText);
        geometryText.RotationDegrees = 270;
        var visualAfter = DesignerInteractionHelper.GetVisualBounds(geometryText);
        AssertAlmost(visualBefore.Left + visualBefore.Width / 2, visualAfter.Left + visualAfter.Width / 2, "rotation change preserves visual center X");
        AssertAlmost(visualBefore.Top + visualBefore.Height / 2, visualAfter.Top + visualAfter.Height / 2, "rotation change preserves visual center Y");

        var cornerScaledText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "Scale me",
            X = 10,
            Y = 10,
            Width = 100,
            Height = 30,
            FontSize = 10
        };
        DesignerInteractionHelper.ResizeElementFromSnapshot(
            cornerScaledText,
            new LabelTemplate { Width = 300, Height = 200 },
            ResizeHandleDirection.BottomRight,
            cornerScaledText.X,
            cornerScaledText.Y,
            cornerScaledText.Width,
            cornerScaledText.Height,
            cornerScaledText.FontSize,
            cornerScaledText.RotationDegrees,
            new System.Windows.Vector(50, 15),
            1.0);
        AssertAlmost(15, cornerScaledText.FontSize, "text corner resize scales font size");

        var sideResizedText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "Wrap me",
            X = 10,
            Y = 10,
            Width = 100,
            Height = 30,
            FontSize = 10
        };
        DesignerInteractionHelper.ResizeElementFromSnapshot(
            sideResizedText,
            new LabelTemplate { Width = 300, Height = 200 },
            ResizeHandleDirection.Right,
            sideResizedText.X,
            sideResizedText.Y,
            sideResizedText.Width,
            sideResizedText.Height,
            sideResizedText.FontSize,
            sideResizedText.RotationDegrees,
            new System.Windows.Vector(50, 0),
            1.0);
        AssertAlmost(10, sideResizedText.FontSize, "text side resize keeps font size");

        var rasterProbe = LabelRasterRenderService.RenderToZplGraphic(
            new[]
            {
                geometryText,
                new LabelElement
                {
                    Type = ElementType.Barcode,
                    Content = "B61F05C9",
                    X = 20,
                    Y = 70,
                    Width = 120,
                    Height = 32,
                    RotationDegrees = 270
                }
            },
            item,
            new LabelTemplate { Width = 226.77, Height = 151.18 },
            new PrinterConfiguration { Dpi = 203, EnableUtf8 = true });
        AssertContains(rasterProbe.GraphicField, "^GFA", "rotated raster probe emits a graphic field");
        AssertTrue(HasNonWhiteGraphicData(rasterProbe.GraphicField), "rotated raster probe contains non-white pixels");

        var parityTemplate = new LabelTemplate { Width = 226.77, Height = 151.18 };
        var parityElements = new[] { geometryText, rotatedQr, imageElement };
        var parityConfig = new PrinterConfiguration { Dpi = 203, EnableUtf8 = true };
        var designerBitmap = LabelRenderEngine.RenderDesignerBitmap(parityElements, item, parityTemplate, parityConfig);
        var printBitmap = LabelRenderEngine.RenderPrintBitmap(parityElements, item, parityTemplate, parityConfig);
        AssertEqual(designerBitmap.WidthDots, printBitmap.WidthDots, "designer and print render widths match without calibration");
        AssertEqual(designerBitmap.HeightDots, printBitmap.HeightDots, "designer and print render heights match without calibration");
        AssertTrue(BitmapPixelsEqual(designerBitmap.Bitmap, printBitmap.Bitmap), "designer and print render pixels match without calibration");

        var calibratedPrint = LabelRenderEngine.RenderPrintBitmap(
            parityElements,
            item,
            parityTemplate,
            new PrinterConfiguration
            {
                Dpi = 203,
                EnableUtf8 = true,
                CalibrationOffsetXmm = 1.5,
                CalibrationOffsetYmm = -0.5,
                CalibrationScaleX = 0.98,
                CalibrationScaleY = 1.02
            });
        AssertEqual(printBitmap.WidthDots, calibratedPrint.WidthDots, "calibration keeps commanded label width stable");
        AssertEqual(printBitmap.HeightDots, calibratedPrint.HeightDots, "calibration keeps commanded label height stable");
        AssertContains(calibratedPrint.DiagnosticSummary, "Offset=1.5,-0.5mm", "calibration diagnostic includes offsets");
        AssertContains(calibratedPrint.DiagnosticSummary, "Scale=0.98,1.02", "calibration diagnostic includes scale");

        var rotatedPrint = LabelRenderEngine.RenderPrintBitmap(
            parityElements,
            item,
            parityTemplate,
            new PrinterConfiguration
            {
                Dpi = 203,
                EnableUtf8 = true,
                PrintRotationDegrees = 90
            });
        AssertEqual(printBitmap.HeightDots, rotatedPrint.WidthDots, "whole-label 90 rotation swaps raster width");
        AssertEqual(printBitmap.WidthDots, rotatedPrint.HeightDots, "whole-label 90 rotation swaps raster height");

        var calibrationProbe = LabelRasterRenderService.RenderCalibrationToZplGraphic(
            parityTemplate,
            new PrinterConfiguration { Dpi = 203, EnableUtf8 = true });
        AssertContains(calibrationProbe.GraphicField, "^GFA", "calibration label emits a graphic field");
        AssertTrue(HasNonWhiteGraphicData(calibrationProbe.GraphicField), "calibration label contains non-white pixels");

        Console.WriteLine("Template parity probe passed.");
        return 0;
    }

    private static string CreateProbeImageBase64()
    {
        return CreateProbeImageBase64(2, 2);
    }

    private static string CreateRectangularProbeImageBase64()
    {
        return CreateProbeImageBase64(4, 2);
    }

    private static string CreateProbeImageBase64(int width, int height)
    {
        using var bitmap = new System.Drawing.Bitmap(width, height);
        bitmap.SetPixel(0, 0, System.Drawing.Color.Black);
        bitmap.SetPixel(width - 1, 0, System.Drawing.Color.White);
        bitmap.SetPixel(0, height - 1, System.Drawing.Color.White);
        bitmap.SetPixel(width - 1, height - 1, System.Drawing.Color.Black);

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'.");
    }

    private static void AssertAlmost(double expected, double actual, string name, double tolerance = 0.75)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{name}: expected '{expected:N2}', got '{actual:N2}'.");
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"{name}: expected true.");
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

    private static bool HasNonWhiteGraphicData(string graphicField)
    {
        var lastComma = graphicField.LastIndexOf(',');
        if (lastComma < 0 || lastComma == graphicField.Length - 1)
            return false;

        return graphicField[(lastComma + 1)..].Any(c => c != '0');
    }

    private static bool BitmapPixelsEqual(
        System.Windows.Media.Imaging.BitmapSource expected,
        System.Windows.Media.Imaging.BitmapSource actual)
    {
        if (expected.PixelWidth != actual.PixelWidth || expected.PixelHeight != actual.PixelHeight)
            return false;

        var stride = expected.PixelWidth * 4;
        var expectedPixels = new byte[stride * expected.PixelHeight];
        var actualPixels = new byte[stride * actual.PixelHeight];
        expected.CopyPixels(expectedPixels, stride, 0);
        actual.CopyPixels(actualPixels, stride, 0);
        return expectedPixels.SequenceEqual(actualPixels);
    }
}
