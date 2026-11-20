namespace VanAn.KhachLink.Services.ProfileContext;

/// <summary>
/// One prioritized "next best action" card on the customer profile page.
/// </summary>
public sealed class ProfileContextCard
{
    /// <summary>Stable rule identifier (e.g. "active-order") — used in tests + analytics.</summary>
    public required string Id { get; init; }

    /// <summary>Sort priority — lower value renders first.</summary>
    public required int Priority { get; init; }

    /// <summary>Bootstrap icon class (e.g. "bi bi-bag").</summary>
    public required string Icon { get; init; }

    /// <summary>Card headline.</summary>
    public required string Title { get; init; }

    /// <summary>Explainable one-liner — why this card is shown.</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Navigation target. "/path" → Blazor NavigateTo; "#anchor" → same-page anchor
    /// (rendered as plain &lt;a href&gt; so the browser scrolls natively).
    /// </summary>
    public required string Href { get; init; }

    /// <summary>CTA button label.</summary>
    public required string CtaLabel { get; init; }
}
