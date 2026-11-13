using FluentAssertions;
using VanAn.UI.Platform.Realtime;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Task AI-P11 — ChatKnowledgePairing: "Đề xuất AI" chỉ bật khi có tin khách
/// (sender khác current user) phía trước tin owner trong cùng thread.
/// </summary>
public sealed class ChatKnowledgePairingTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static RealtimeChatMessage Msg(Guid sender, string content = "x")
        => new() { Id = Guid.NewGuid(), SenderId = sender, Content = content, SentAt = DateTime.UtcNow };

    [Fact]
    public void OwnerMessage_WithPrecedingCustomerMessage_ReturnsQuestionIndex()
    {
        var msgs = new List<RealtimeChatMessage>
        {
            Msg(Customer, "ship bao lâu?"),
            Msg(Owner, "2h trong nội thành"),
        };

        var idx = ChatKnowledgePairing.FindQuestionIndex(msgs, ownerIndex: 1, Owner);

        idx.Should().Be(0);
    }

    [Fact]
    public void MultipleCustomerMessages_PicksNearestPreceding()
    {
        var msgs = new List<RealtimeChatMessage>
        {
            Msg(Customer, "câu 1"),
            Msg(Owner, "đáp 1"),
            Msg(Customer, "câu hỏi thật"),
            Msg(Customer, "à quên, ý là câu này"),
            Msg(Owner, "đáp cuối"),
        };

        var idx = ChatKnowledgePairing.FindQuestionIndex(msgs, ownerIndex: 4, Owner);

        idx.Should().Be(3);
    }

    [Fact]
    public void OwnerMessage_WithoutPrecedingOther_ReturnsNull()
    {
        var msgs = new List<RealtimeChatMessage>
        {
            Msg(Owner, "chào shop"),
            Msg(Owner, "có món chay không?"),
        };

        ChatKnowledgePairing.FindQuestionIndex(msgs, ownerIndex: 1, Owner).Should().BeNull();
    }

    [Fact]
    public void FirstMessageOwner_ReturnsNull()
    {
        var msgs = new List<RealtimeChatMessage> { Msg(Owner, "alo") };

        ChatKnowledgePairing.FindQuestionIndex(msgs, ownerIndex: 0, Owner).Should().BeNull();
    }

    [Fact]
    public void SkipsInterveningOwnerMessages_FindsCustomerBeforeThem()
    {
        var msgs = new List<RealtimeChatMessage>
        {
            Msg(Customer, "giá bao nhiêu?"),
            Msg(Owner, "30k"),
            Msg(Owner, "có ship không?"), // owner tự hỏi tiếp — vẫn pair về câu khách gốc
        };

        var idx = ChatKnowledgePairing.FindQuestionIndex(msgs, ownerIndex: 2, Owner);

        idx.Should().Be(0);
    }
}
