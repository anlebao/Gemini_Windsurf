namespace VanAn.UI.Platform.Realtime;

/// <summary>
/// AI-P11 — pure pairing logic for the "Đề xuất vào tri thức AI" button:
/// an owner-side message is suggestible only when a customer (different sender)
/// message precedes it in the same thread. Kept pure/static for unit tests.
/// </summary>
public static class ChatKnowledgePairing
{
    /// <summary>
    /// Index of the nearest message before <paramref name="ownerIndex"/> sent by
    /// someone other than <paramref name="currentUserId"/> — i.e. the customer
    /// question the owner answered. Null when there is none (button disabled).
    /// v1 limitation: "shop side" = current user only; other staff senders are
    /// treated as the customer (ShopInbox is Owner-only — acceptable).
    /// </summary>
    public static int? FindQuestionIndex(
        IReadOnlyList<RealtimeChatMessage> messages, int ownerIndex, Guid currentUserId)
    {
        for (var i = ownerIndex - 1; i >= 0; i--)
            if (messages[i].SenderId != currentUserId)
                return i;
        return null;
    }
}
