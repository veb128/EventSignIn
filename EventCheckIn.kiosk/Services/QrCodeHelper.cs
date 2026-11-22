using QRCoder;
using ZXing;
using ZXing.Common;
#if ANDROID
using Android.Graphics;
#endif

namespace EventCheckIn.Services;

/// <summary>
/// Generates and parses QR codes for checked-in attendees.
/// Payload format: "EVENTCHECKIN:{32-char hex check-in Id}" (or a raw 32-char hex Id).
/// </summary>
public static class QrCodeHelper
{
    public const string Prefix = "EVENTCHECKIN:";

    public static string EncodePayload(string checkInId) => $"{Prefix}{checkInId}";

    public static byte[] GeneratePng(string checkInId, int pixelsPerModule = 12)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(EncodePayload(checkInId), QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }

    public static string? TryExtractCheckInId(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var s = rawText.Trim();
        if (s.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            s = s[Prefix.Length..].Trim();
        return s.Length == 32 && s.All(Uri.IsHexDigit) ? s.ToLowerInvariant() : null;
    }

    /// <summary>Decodes a QR code from a captured JPEG/PNG image byte array.</summary>
    public static string? TryDecodeQrFromImage(byte[] imageBytes)
    {
#if ANDROID
        try
        {
            using var bmp = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length);
            if (bmp is null) return null;

            // Downscale large camera photos for fast QR decoding.
            int maxDim = 1024;
            Bitmap target = bmp;
            if (bmp.Width > maxDim || bmp.Height > maxDim)
            {
                float scale = Math.Min((float)maxDim / bmp.Width, (float)maxDim / bmp.Height);
                int w = Math.Max(1, (int)(bmp.Width * scale));
                int h = Math.Max(1, (int)(bmp.Height * scale));
                target = Bitmap.CreateScaledBitmap(bmp, w, h, true)!;
            }

            try
            {
                int width = target.Width;
                int height = target.Height;
                int[] pixels = new int[width * height];
                target.GetPixels(pixels, 0, width, 0, 0, width, height);

                byte[] rgb = new byte[width * height * 3];
                for (int i = 0, j = 0; i < pixels.Length; i++)
                {
                    int p = pixels[i];
                    rgb[j++] = (byte)((p >> 16) & 0xFF);
                    rgb[j++] = (byte)((p >> 8) & 0xFF);
                    rgb[j++] = (byte)(p & 0xFF);
                }

                var source = new RGBLuminanceSource(rgb, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
                var reader = new BarcodeReaderGeneric
                {
                    AutoRotate = true,
                    Options = new DecodingOptions
                    {
                        TryHarder = true,
                        TryInverted = true,
                        PossibleFormats = [BarcodeFormat.QR_CODE]
                    }
                };
                var result = reader.Decode(source);
                return result?.Text;
            }
            finally
            {
                if (!ReferenceEquals(target, bmp)) target.Dispose();
            }
        }
        catch { return null; }
#else
        return null;
#endif
    }
}
