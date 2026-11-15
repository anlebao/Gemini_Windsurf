using VanAn.Atrs.Domain;
using Xunit;

namespace VanAn.Atrs.Tests.Domain;

public class RiskPolicyEngineTests
{
    private readonly RiskPolicyEngine _engine = new();

    private static RiskContext Ctx(Action<RiskContextBuilder> configure)
    {
        var b = new RiskContextBuilder();
        configure(b);
        return b.Build();
    }

    private sealed class RiskContextBuilder
    {
        private readonly Guid _subject = Guid.NewGuid();
        private string _action = "device.register";
        private bool _newDevice;
        private bool _lost;
        private bool _authFailures;
        private bool _recentRecovery;
        private bool _credentialChanged;
        private bool _highValue;

        public RiskContextBuilder Action(string a) { _action = a; return this; }
        public RiskContextBuilder NewDevice() { _newDevice = true; return this; }
        public RiskContextBuilder Lost() { _lost = true; return this; }
        public RiskContextBuilder AuthFailures() { _authFailures = true; return this; }
        public RiskContextBuilder RecentRecovery() { _recentRecovery = true; return this; }
        public RiskContextBuilder CredentialChanged() { _credentialChanged = true; return this; }
        public RiskContextBuilder HighValue() { _highValue = true; return this; }

        public RiskContext Build() => new(
            _subject, _action, _newDevice, _lost, _authFailures, _recentRecovery, _credentialChanged, _highValue);
    }

    [Fact]
    public void Routine_ReturnsAllow_WithReasonCodeAndPolicyVersion()
    {
        var d = _engine.Evaluate(Ctx(b => { }));
        Assert.Equal(RiskDecisionKind.ALLOW, d.Decision);
        Assert.Contains("routine", d.ReasonCodes);
        Assert.Equal(RiskPolicyEngine.PolicyVersion, d.PolicyVersion);
    }

    [Fact]
    public void NewDevice_RequiresStepUp()
    {
        var d = _engine.Evaluate(Ctx(b => b.NewDevice()));
        Assert.Equal(RiskDecisionKind.ALLOW_WITH_STEP_UP, d.Decision);
        Assert.Contains("new-device", d.ReasonCodes);
    }

    [Fact]
    public void HighValueRecovery_RequiresStepUp()
    {
        var d = _engine.Evaluate(Ctx(b => b.Action("recovery.submit").HighValue()));
        Assert.Equal(RiskDecisionKind.ALLOW_WITH_STEP_UP, d.Decision);
        Assert.Contains("high-value-recovery", d.ReasonCodes);
    }

    [Fact]
    public void RecentRecoveryPlusHighValue_ManualReview()
    {
        var d = _engine.Evaluate(Ctx(b => b.RecentRecovery().HighValue()));
        Assert.Equal(RiskDecisionKind.REQUIRE_MANUAL_REVIEW, d.Decision);
        Assert.Contains("recent-recovery", d.ReasonCodes);
    }

    [Fact]
    public void SuspectedTakeover_Restricts()
    {
        var d = _engine.Evaluate(Ctx(b => b.Lost().AuthFailures().CredentialChanged().HighValue()));
        Assert.Equal(RiskDecisionKind.TEMPORARY_RESTRICT, d.Decision);
        Assert.Contains("suspected-takeover", d.ReasonCodes);
    }

    [Fact]
    public void TakeoverSignals_Override_NewDeviceRule()
    {
        // R4 phải ưu tiên hơn R1 — device mới + takeover signals → TEMPORARY_RESTRICT.
        var d = _engine.Evaluate(Ctx(b => b.NewDevice().Lost().AuthFailures().CredentialChanged().HighValue()));
        Assert.Equal(RiskDecisionKind.TEMPORARY_RESTRICT, d.Decision);
    }

    [Fact]
    public void NullContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Evaluate(null!));
    }
}