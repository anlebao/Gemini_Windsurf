namespace VanAn.Atrs.Contracts;

/// <summary>Notification item (ATRS-13) — user inbox, KHÔNG chứa secret.</summary>
public sealed record AccountNotificationDto(
    Guid Id,
    string EventType,
    string Title,
    string Body,
    string? Link,
    DateTime CreatedAt,
    bool IsRead);

/// <summary>Page notifications — unread-first, paged.</summary>
public sealed record NotificationPageDto(
    IReadOnlyList<AccountNotificationDto> Items,
    int Total,
    int Unread,
    int Skip,
    int Take);