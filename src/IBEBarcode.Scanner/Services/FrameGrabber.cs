using IBEBarcode.Scanner.Core.Imaging;

namespace IBEBarcode.Scanner.Services;

/// <summary>
/// Converts a captured camera frame or a chosen photo into the grayscale pixels the fallback decoders
/// need. Only the pixel extraction is platform-specific; everything downstream is shared.
/// <para>
/// Frames are downscaled before conversion: the decoders measure relative bar widths and heights, so a
/// 1600px-wide working image is plenty and keeps the work off the UI thread's critical path.
/// </para>
/// </summary>
public static partial class FrameGrabber
{
    /// <summary>Longest edge of the working image handed to the decoders.</summary>
    public const int MaxWorkingWidth = 1600;

    /// <summary>Converts a frame captured from the live camera preview.</summary>
    public static partial GrayImage? FromPlatformImage(Microsoft.Maui.Graphics.Platform.PlatformImage image);

    /// <summary>
    /// Encodes a captured frame as JPEG, which is how the platform engine's image scanner is handed the
    /// picture a scan press takes. Null if the frame cannot be encoded.
    /// </summary>
    public static partial byte[]? EncodeJpeg(Microsoft.Maui.Graphics.Platform.PlatformImage image);

    /// <summary>Decodes a chosen or captured photo file.</summary>
    public static partial Task<GrayImage?> FromPhotoAsync(FileResult file);

    /// <summary>Nearest-neighbour downscale to the working size, shared by the platform implementations.</summary>
    private static GrayImage Downscale(GrayImage image) => image.DownscaleToWidth(MaxWorkingWidth);
}
