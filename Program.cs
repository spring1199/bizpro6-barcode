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
        AssertAlmost(explicitShortText.Height, explicitShortLocal.Height, "explicit text height remains user-controlled");
        AssertAlmost(explicitShortText.Width, explicitShortLocal.Width, "explicit text width remains user-controlled");
        var explicitShortLayout = DesignerInteractionHelper.MeasureTextLayout(
            explicitShortLocal.Width,
            explicitShortLocal.Height,
            explicitShortText.FontSize,
            explicitShortText.Content,
            explicitShortText.IsBold,
            explicitShortText.IsCentered,
            explicitShortText.RotationDegrees);
        AssertTrue(explicitShortLayout.FontSize < explicitShortText.FontSize * LabelSizeHelper.FONT_SCALING_FACTOR, "narrow rotated text auto-fits below requested font size");
        AssertTrue(explicitShortLayout.Fits, "narrow rotated text fits inside explicit box");
        AssertTrue(!explicitShortLayout.WrapText, "90-degree text uses no-wrap fitting before rotation");
        AssertTrue(explicitShortLayout.MeasuredWidth <= explicitShortLayout.ContentWidth + 0.75, "90-degree text unwrapped width fits inside content box");

        var screenshotPriceText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "MNT 395,000",
            X = 45,
            Y = 30,
            Width = 36,
            Height = 58,
            FontSize = 12,
            IsBold = true,
            RotationDegrees = 90
        };
        var screenshotPriceLocal = DesignerInteractionHelper.GetLocalSize(screenshotPriceText);
        var screenshotPriceLayout = DesignerInteractionHelper.MeasureTextLayout(
            screenshotPriceLocal.Width,
            screenshotPriceLocal.Height,
            screenshotPriceText.FontSize,
            screenshotPriceText.Content,
            screenshotPriceText.IsBold,
            screenshotPriceText.IsCentered,
            screenshotPriceText.RotationDegrees);
        AssertTrue(screenshotPriceLayout.Fits, "screenshot-sized rotated price text fits inside explicit box");
        AssertTrue(!screenshotPriceLayout.WrapText, "screenshot-sized rotated price text stays unwrapped");

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
        AssertAlmost(narrowCyrillicText.Width, narrowCyrillicLocal.Width, "narrow Cyrillic text keeps explicit width");
        AssertAlmost(narrowCyrillicText.Height, narrowCyrillicLocal.Height, "narrow Cyrillic text keeps explicit height");
        var narrowCyrillicLayout = DesignerInteractionHelper.MeasureTextLayout(
            narrowCyrillicLocal.Width,
            narrowCyrillicLocal.Height,
            narrowCyrillicText.FontSize,
            narrowCyrillicText.Content,
            rotationDegrees: narrowCyrillicText.RotationDegrees);
        AssertTrue(narrowCyrillicLayout.Fits, "narrow rotated Cyrillic text auto-fits inside explicit box");
        AssertTrue(!narrowCyrillicLayout.WrapText, "90-degree Cyrillic text uses no-wrap fitting before rotation");

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
            1.0,
            203);
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
            1.0,
            203);
        AssertAlmost(10, sideResizedText.FontSize, "text side resize keeps font size");

        var driftText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "MNT 350,000",
            X = 40,
            Y = 30,
            Width = 18,
            Height = 12,
            FontSize = 12,
            RotationDegrees = 90
        };
        var driftVisualBefore = DesignerInteractionHelper.GetVisualBounds(driftText);
        var driftLocalBefore = DesignerInteractionHelper.GetLocalSize(driftText);
        DesignerInteractionHelper.CommitMeasuredLocalSize(driftText);
        var driftVisualAfter = DesignerInteractionHelper.GetVisualBounds(driftText);
        AssertAlmost(driftLocalBefore.Width, driftText.Width, "commit normalizes text width to model");
        AssertAlmost(driftLocalBefore.Height, driftText.Height, "commit normalizes text height to model");
        AssertAlmost(driftVisualBefore.Left + driftVisualBefore.Width / 2, driftVisualAfter.Left + driftVisualAfter.Width / 2, "commit keeps visual center X");
        AssertAlmost(driftVisualBefore.Top + driftVisualBefore.Height / 2, driftVisualAfter.Top + driftVisualAfter.Height / 2, "commit keeps visual center Y");

        var textOnlyRender = LabelRenderEngine.RenderDesignerBitmap(
            new[] { driftText },
            null,
            new LabelTemplate { Width = 220, Height = 180 },
            new PrinterConfiguration { Dpi = 96, EnableUtf8 = true });
        var textPixelBounds = GetNonWhitePixelBounds(textOnlyRender.Bitmap);
        AssertTrue(textPixelBounds.HasValue, "rotated text render produces non-white pixels");
        var textBounds = textPixelBounds.GetValueOrDefault();
        AssertTrue(textBounds.X > driftVisualAfter.Left, "rotated text pixels stay inside left visual edge");
        AssertTrue(textBounds.Y > driftVisualAfter.Top, "rotated text pixels stay inside top visual edge");
        AssertTrue(textBounds.X + textBounds.Width < driftVisualAfter.Right, "rotated text pixels stay inside right visual edge");
        AssertTrue(textBounds.Y + textBounds.Height < driftVisualAfter.Bottom, "rotated text pixels stay inside bottom visual edge");

        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            foreach (var handle in Enum.GetValues<ResizeHandleDirection>())
            {
                AssertResizeHandleKeepsOppositeAnchor(handle, rotation);
            }
        }

        AssertEqual(
            System.Windows.Input.Cursors.SizeWE,
            DesignerInteractionHelper.GetResizeCursor(ResizeHandleDirection.Top, 90),
            "90-degree top handle cursor maps to horizontal resize");
        AssertEqual(
            System.Windows.Input.Cursors.SizeNS,
            DesignerInteractionHelper.GetResizeCursor(ResizeHandleDirection.Right, 90),
            "90-degree right handle cursor maps to vertical resize");

        var edgeRotatedText = new LabelElement
        {
            Type = ElementType.Text,
            Content = "MNT 395,000",
            X = 4,
            Y = 4,
            Width = 120,
            Height = 24,
            FontSize = 12,
            RotationDegrees = 90
        };
        var edgeTemplate = new LabelTemplate { Width = 260, Height = 160 };
        DesignerInteractionHelper.ClampElementToTemplate(edgeRotatedText, edgeTemplate);
        var edgeBounds = DesignerInteractionHelper.GetVisualBounds(edgeRotatedText);
        AssertTrue(edgeBounds.Top >= DesignerInteractionHelper.SelectionChromePadding - 0.75, "rotated clamp keeps top handles inside label");
        AssertTrue(edgeBounds.Bottom <= edgeTemplate.Height - DesignerInteractionHelper.SelectionChromePadding + 0.75, "rotated clamp keeps bottom handles inside label");

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

    private static System.Windows.Int32Rect? GetNonWhitePixelBounds(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var left = bitmap.PixelWidth;
        var top = bitmap.PixelHeight;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                var offset = y * stride + x * 4;
                var blue = pixels[offset];
                var green = pixels[offset + 1];
                var red = pixels[offset + 2];
                var alpha = pixels[offset + 3];
                if (alpha == 0 || (red > 248 && green > 248 && blue > 248))
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
            return null;

        return new System.Windows.Int32Rect(left, top, right - left + 1, bottom - top + 1);
    }

    private static void AssertResizeHandleKeepsOppositeAnchor(ResizeHandleDirection handle, int rotation)
    {
        var element = new LabelElement
        {
            Type = ElementType.Barcode,
            Content = "B61F05C9",
            X = 100,
            Y = 70,
            Width = 80,
            Height = 30,
            RotationDegrees = rotation
        };
        var template = new LabelTemplate { Width = 400, Height = 260 };
        var beforeAnchor = GetOppositeAnchor(element, handle);
        var localDelta = new System.Windows.Vector(
            GetHandleX(handle) * 12,
            GetHandleY(handle) * 8);
        var screenDelta = DesignerInteractionHelper.RotateVector(localDelta, rotation);

        DesignerInteractionHelper.ResizeElementFromSnapshot(
            element,
            template,
            handle,
            element.X,
            element.Y,
            element.Width,
            element.Height,
            element.FontSize,
            element.RotationDegrees,
            screenDelta,
            1.0,
            203);

        var afterAnchor = GetOppositeAnchor(element, handle);
        AssertAlmost(beforeAnchor.X, afterAnchor.X, $"{rotation}/{handle} opposite anchor X");
        AssertAlmost(beforeAnchor.Y, afterAnchor.Y, $"{rotation}/{handle} opposite anchor Y");

        if (GetHandleX(handle) != 0)
            AssertTrue(element.Width > 80, $"{rotation}/{handle} width grows along local X");
        else
            AssertAlmost(80, element.Width, $"{rotation}/{handle} width stable for vertical edge handle");

        if (GetHandleY(handle) != 0)
            AssertTrue(element.Height > 30, $"{rotation}/{handle} height grows along local Y");
        else
            AssertAlmost(30, element.Height, $"{rotation}/{handle} height stable for horizontal edge handle");
    }

    private static System.Windows.Point GetOppositeAnchor(LabelElement element, ResizeHandleDirection handle)
    {
        var local = DesignerInteractionHelper.GetLocalSize(element);
        var center = new System.Windows.Point(element.X + local.Width / 2, element.Y + local.Height / 2);
        var oppositeLocal = new System.Windows.Vector(
            GetHandleX(handle) == 0 ? 0 : -GetHandleX(handle) * local.Width / 2,
            GetHandleY(handle) == 0 ? 0 : -GetHandleY(handle) * local.Height / 2);
        return center + DesignerInteractionHelper.RotateVector(oppositeLocal, element.RotationDegrees);
    }

    private static int GetHandleX(ResizeHandleDirection handle)
    {
        return handle switch
        {
            ResizeHandleDirection.TopLeft or ResizeHandleDirection.Left or ResizeHandleDirection.BottomLeft => -1,
            ResizeHandleDirection.TopRight or ResizeHandleDirection.Right or ResizeHandleDirection.BottomRight => 1,
            _ => 0
        };
    }

    private static int GetHandleY(ResizeHandleDirection handle)
    {
        return handle switch
        {
            ResizeHandleDirection.TopLeft or ResizeHandleDirection.Top or ResizeHandleDirection.TopRight => -1,
            ResizeHandleDirection.BottomLeft or ResizeHandleDirection.Bottom or ResizeHandleDirection.BottomRight => 1,
            _ => 0
        };
    }
}
