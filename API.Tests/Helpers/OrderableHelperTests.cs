using System.Collections.Generic;
using System.Linq;
using API.Entities;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class OrderableHelperTests
{
    [Fact]
    public void ReorderItems_ItemExists_SuccessfullyReorders()
    {
        // Arrange
        var items = new List<AppUserSideNavStream>
        {
            new AppUserSideNavStream { Id = 1, Order = 0, Name = "A" },
            new AppUserSideNavStream { Id = 2, Order = 1, Name = "A" },
            new AppUserSideNavStream { Id = 3, Order = 2, Name = "A" },
        };

        // Act
        OrderableHelper.ReorderItems(items, 2, 0);

        // Assert
        Assert.Equal(2, items[0].Id);  // Item 2 should be at position 0
        Assert.Equal(1, items[1].Id);  // Item 1 should be at position 1
        Assert.Equal(3, items[2].Id);  // Item 3 should remain at position 2
    }

    [Fact]
    public void ReorderItems_ItemNotFound_NoChange()
    {
        // Arrange
        var items = new List<AppUserSideNavStream>
        {
            new AppUserSideNavStream { Id = 1, Order = 0, Name = "A" },
            new AppUserSideNavStream { Id = 2, Order = 1, Name = "A" },
        };

        // Act
        OrderableHelper.ReorderItems(items, 3, 0);  // Item with Id 3 doesn't exist

        // Assert
        Assert.Equal(1, items[0].Id);  // Item 1 should remain at position 0
        Assert.Equal(2, items[1].Id);  // Item 2 should remain at position 1
    }

    [Fact]
    public void ReorderItems_InvalidPosition_NoChange()
    {
        // Arrange
        var items = new List<AppUserSideNavStream>
        {
            new AppUserSideNavStream { Id = 1, Order = 0, Name = "A" },
            new AppUserSideNavStream { Id = 2, Order = 1, Name = "A" },
        };

        // Act
        OrderableHelper.ReorderItems(items, 2, 3);  // Position 3 is out of range

        // Assert
        Assert.Equal(1, items[0].Id);  // Item 1 should remain at position 0
        Assert.Equal(2, items[1].Id);  // Item 2 should remain at position 1
    }

    [Fact]
    public void ReorderItems_EmptyList_NoChange()
    {
        // Arrange
        var items = new List<AppUserSideNavStream>();

        // Act
        OrderableHelper.ReorderItems(items, 2, 1);  // List is empty

        // Assert
        Assert.Empty(items);  // The list should remain empty
    }

    [Fact]
    public void ReorderItems_DoubleMove()
    {
        // Arrange
        var items = new List<AppUserSideNavStream>
        {
            new AppUserSideNavStream { Id = 1, Order = 0, Name = "0" },
            new AppUserSideNavStream { Id = 2, Order = 1, Name = "1" },
            new AppUserSideNavStream { Id = 3, Order = 2, Name = "2" },
            new AppUserSideNavStream { Id = 4, Order = 3, Name = "3" },
            new AppUserSideNavStream { Id = 5, Order = 4, Name = "4" },
            new AppUserSideNavStream { Id = 6, Order = 5, Name = "5" },
        };

        // Move 4 -> 1
        OrderableHelper.ReorderItems(items, 5, 1);

        // Assert
        Assert.Equal(1, items[0].Id);
        Assert.Equal(0, items[0].Order);
        Assert.Equal(5, items[1].Id);
        Assert.Equal(1, items[1].Order);
        Assert.Equal(2, items[2].Id);
        Assert.Equal(2, items[2].Order);

        // Ensure the items are in the correct order
        Assert.Equal("041235", string.Join("", items.Select(s => s.Name)));

        OrderableHelper.ReorderItems(items, items[4].Id, 1); // 3 -> 1

        Assert.Equal("034125", string.Join("", items.Select(s => s.Name)));
    }
}
