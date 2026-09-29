using Android.Graphics;
using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Services;

/// <summary> Android pixel extraction for the fallback decoders. </summary>
public static partial class FrameGrabber
{
    /// <summary>High enough that re-encoding costs the engine nothing measurable, small enough to stay cheap.</summary>
    private const int JpegQuality = 95;

    public static partial GrayImage? FromPlatformImage(Microsoft.Maui.Graphics.Platform.PlatformImage image)
    {
        try
        {
            if (image.PlatformRepresentation as Bitmap is not { } bitmap)
                return null;

            return FromBitmap(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static partial byte[]? EncodeJpeg(Microsoft.Maui.Graphics.Platform.PlatformImage image)
    {
        try
        {
            if (image.PlatformRepresentation as Bitmap is not { } bitmap)
                return null;

            using var buffer = new MemoryStream();

            return bitmap.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, buffer) ? buffer.ToArray() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static async partial Task<GrayImage?> FromPhotoAsync(FileResult file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var bitmap = await BitmapFactory.DecodeStreamAsync(stream);
            return bitmap is null ? null : FromBitmap(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static GrayImage? FromBitmap(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        if (width <= 0 || height <= 0)
            return null;

        // Shrink first: decoding a 12MP frame is wasted work when the decoders only need bar geometry.
        if (width > MaxWorkingWidth)
        {
            var scale = (double)MaxWorkingWidth / width;
            width = MaxWorkingWidth;
            height = Math.Max(1, (int)(bitmap.Height * scale));
        }

        var pixels = new int[width * height];
        using var scaled = Bitmap.CreateScaledBitmap(bitmap, width, height, filter: true);

        scaled.GetPixels(pixels, 0, width, 0, 0, width, height);
        return GrayImage.FromArgb32(pixels, width, height);
    }
}
