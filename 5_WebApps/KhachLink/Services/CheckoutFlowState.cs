namespace VanAn.KhachLink.Services;

/// <summary>
/// Scoped state machine for the multi-step checkout flow.
/// Tracks current step, customer info, and payment selection.
/// </summary>
public class CheckoutFlowState
{
    public CheckoutStep CurrentStep { get; private set; } = CheckoutStep.Cart;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? PaymentMethod { get; set; }

    public event Action? OnStateChanged;

    public void GoTo(CheckoutStep step)
    {
        CurrentStep = step;
        OnStateChanged?.Invoke();
    }

    public void Reset()
    {
        CurrentStep = CheckoutStep.Cart;
        CustomerName = null;
        CustomerPhone = null;
        CustomerAddress = null;
        PaymentMethod = null;
        OnStateChanged?.Invoke();
    }
}

public enum CheckoutStep
{
    Cart,
    CustomerInfo,
    Payment,
    Confirmation
}
