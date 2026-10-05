namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// Business conflict (HTTP 409) — double-booking / staff conflict (SRS §12, AC-C04).
/// Message phải hướng dẫn hành động cho customer (§6.5), không hiển thị mã kỹ thuật.
/// </summary>
public class BookingConflictException : Exception
{
    public BookingConflictException(string message) : base(message) { }
    public BookingConflictException(string message, Exception innerException) : base(message, innerException) { }
}
