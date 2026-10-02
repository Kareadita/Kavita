using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.SignalR;
using Kavita.Common.EnvironmentInfo;
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
    private readonly IProgressThrottle _progressThrottle = Substitute.For<IProgressThrottle>();
    private readonly IActivityTracker _activityTracker = Substitute.For<IActivityTracker>();
    private readonly EventHub _eventHub;

    public EventHubTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.All.Returns(_all);
        _clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_filtered);
        _presenceTracker.GetOnlineAdminIds().Returns([1]);
        _presenceTracker.GetOnlineUserIds().Returns([2]);
        _unitOfWork.UserRepository.HasAccessToLibrary(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        _progressThrottle.SendAsync(Arg.Any<SignalRMessageDto>(), Arg.Any<Func<Task>>())
            .Returns(call => call.Arg<Func<Task>>()());

        _eventHub = new EventHub(_hubContext, _presenceTracker, _unitOfWork, _progressThrottle, _activityTracker);
    }

    [Fact]
    public async Task NotificationProgress_GoesThroughThrottle()
    {
        var message = MessageFactory.FileScanProgressEvent("M:/One Piece", 1, "Manga", ProgressEventType.Updated);

        await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, message);

        await _progressThrottle.Received(1).SendAsync(message, Arg.Any<Func<Task>>());
        await _filtered.Received(1).SendCoreAsync(MessageFactory.NotificationProgress, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsideJob_StampsBootAndJobId()
    {
        var message = MessageFactory.FileScanProgressEvent("M:/One Piece", 1, "Manga", ProgressEventType.Updated);

        JobCorrelation.CurrentJobId = "184";
        try
        {
            await _eventHub.SendMessageAsync(MessageFactory.NotificationProgress, message);
        }
        finally
        {
            JobCorrelation.CurrentJobId = null;
        }

        Assert.Matches(@"^[0-9a-f]{8}\.184$", message.CorrelationId);
        Assert.StartsWith(BuildInfo.BootId.ToString("N")[..8], message.CorrelationId);
    }

    [Fact]
    public async Task InsideJob_KeepsExistingCorrelationId()
    {
        var message = MessageFactory.DownloadProgressEvent("joe", "One Piece v01.zip", "Preparing", 0.5f, correlationId: "client-abc");

        JobCorrelation.CurrentJobId = "184";
        try
        {
            await _eventHub.SendMessageToAsync(MessageFactory.NotificationProgress, message, 1);
        }
        finally
        {
            JobCorrelation.CurrentJobId = null;
        }

        Assert.Equal("client-abc", message.CorrelationId);
    }

    [Fact]
    public async Task OutsideJob_LeavesCorrelationIdEmpty()
    {
        var message = MessageFactory.ErrorEvent("Comics scan aborted", "Empty root");

        await _eventHub.SendMessageAsync(MessageFactory.Error, message);

        Assert.Null(message.CorrelationId);
    }

    [Fact]
    public async Task OtherMethods_SkipThrottle()
    {
        await _eventHub.SendMessageAsync(MessageFactory.Error, MessageFactory.ErrorEvent("Comics scan aborted", "Empty root"));

        await _progressThrottle.DidNotReceiveWithAnyArgs().SendAsync(default!, default!);
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
