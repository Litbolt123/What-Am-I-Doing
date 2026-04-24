using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace WhatAmIDoing.Services;

/// <summary>
/// Best-effort text extraction from a JPEG screenshot using the OCR engine that ships with
/// Windows 10/11 (<see cref="OcrEngine"/>). No third-party model files; falls back to no text
/// when the user's installed OCR languages can't recognize the image.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public static class ScreenOcrService
{
    private static OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    public static string? Recognize(byte[] jpegBytes)
    {
        if (_engine is null)
            return null;
        if (jpegBytes is null || jpegBytes.Length == 0)
            return null;

        try
        {
            return RecognizeAsync(jpegBytes).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> RecognizeAsync(byte[] jpegBytes)
    {
        if (_engine is null)
            return null;

        using var ms = new MemoryStream(jpegBytes);
        using var ras = new InMemoryRandomAccessStream();
        using (var dataWriter = new DataWriter(ras))
        {
            dataWriter.WriteBytes(jpegBytes);
            await dataWriter.StoreAsync();
            await dataWriter.FlushAsync();
            dataWriter.DetachStream();
        }

        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        var result = await _engine.RecognizeAsync(softwareBitmap);
        var text = result?.Text;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Squash repeated whitespace (multi-line OCR text packs better in DB and reports).
        return System.Text.RegularExpressions.Regex
            .Replace(text, @"\s+", " ")
            .Trim();
    }
}
