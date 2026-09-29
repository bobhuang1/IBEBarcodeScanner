using System.Runtime.InteropServices;
using CoreGraphics;
using Foundation;
using IBEBarcode.Scanner.Core.Imaging;
using UIKit;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// iOS pixel extraction for the fallback decoders. The frame is redrawn into an 8-bit grayscale
/// CoreGraphics bitmap context, which both converts the colour space and does the downscale in one step.
/// </summary>
public static partial class FrameGrabber
{
    public static partial GrayImage? FromPlatformImage(Microsoft.Maui.Graphics.Platform.PlatformImage image)
    {
        try
        {
            if (image.PlatformRepresentation as UIImage is not { } uiImage)
                return null;

            return FromUiImage(uiImage);
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
            if (image.PlatformRepresentation as UIImage is not { } uiImage)
                return null;

            using var data = uiImage.AsJPEG(0.95f);

            return data?.ToArray();
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
            using var data = NSData.FromStream(stream);
            if (data is null)
                return null;

            using var uiImage = UIImage.LoadFromData(data);
            return uiImage is null ? null : FromUiImage(uiImage);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static GrayImage? FromUiImage(UIImage uiImage)
    {
        using var cgImage = uiImage.CGImage;
        if (cgImage is null)
            return null;

        var width = (int)cgImage.Width;
        var height = (int)cgImage.Height;

        if (width <= 0 || height <= 0)
            return null;

        if (width > MaxWorkingWidth)
        {
            var scale = (double)MaxWorkingWidth / width;
            width = MaxWorkingWidth;
            height = Math.Max(1, (int)(height * scale));
        }

        var buffer = Marshal.AllocHGlobal(width * height);

        try
        {
            using var colorSpace = CGColorSpace.CreateDeviceGray();
            using var context = new CGBitmapContext(
                buffer,
                width,
                height,
                8,
                width,
                colorSpace,
                CGImageAlphaInfo.None);

            context.DrawImage(new CGRect(0, 0, width, height), cgImage);

            var gray = new byte[width * height];
            Marshal.Copy(buffer, gray, 0, gray.Length);
            return new GrayImage(width, height, gray);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
