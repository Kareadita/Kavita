using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.SignalR;
using Kavita.Models.DTOs.SignalR;
using Kavita.Services.SignalR;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Xunit;

namespace Kavita.Services.Tests.SignalR;

public class EventHubTests
{
    private readonly IHubContext<MessageHub> _hubContext = Substitute.For<IHubContext<MessageHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _all = Substitute.For<IClientProxy>();
    private readonly IClientProxy _filtered = Substitute.For<IClientProxy>();
    private readonly IPresenceTracker _presenceTracker = Substitute.For<IPresenceTracker>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly EventHub _eventHub;

    public EventHubTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.All.Returns(_all);
        _clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_filtered);
        _presenceTracker.GetOnlineAdminIds().Returns([1]);
        _presenceTracker.GetOnlineUserIds().Returns([2]);
        _unitOfWork.UserRepository.HasAccessToLibrary(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        _eventHub = new EventHub(_hubContext, _presenceTracker, _unitOfWork);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task LibraryModified_GoesToEveryClient_EvenWithLibraryId(string action)
    {
        await _eventHub.SendMessageAsync(MessageFactory.LibraryModified, MessageFactory.LibraryModifiedEvent(7, action), false);

        await _all.Received(1).SendCoreAsync(MessageFactory.LibraryModified, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await _filtered.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }

    [Fact]
    public async Task SeriesAdded_IsStillFilteredByAccess()
    {
        await _eventHub.SendMessageAsync(MessageFactory.SeriesAdded, MessageFactory.SeriesAddedEvent(3, "One Piece", 7), false);

        await _filtered.Received(1).SendCoreAsync(MessageFactory.SeriesAdded, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await _all.DidNotReceiveWithAnyArgs().SendCoreAsync(default!, default!, default);
    }
}
