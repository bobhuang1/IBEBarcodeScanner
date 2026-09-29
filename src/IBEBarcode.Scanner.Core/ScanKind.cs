namespace IBEBarcode.Scanner.Core;

/// <summary>
/// What a scanned payload actually is, once interpreted. Deliberately close to the
/// <c>BarcodeScanning.BarcodeTypes</c> enum so the platform engines map onto it cleanly.
/// </summary>
public enum ScanKind
{
    Text,
    Url,
    Email,
    Phone,
    Sms,
    Wifi,
    Contact,
    GeographicCoordinates,
    CalendarEvent,
    Gs1,
    Product,
}
