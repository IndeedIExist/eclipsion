using System;
using System.Collections.Generic;
using Content.Client.UserInterface.Controls;
using Content.Shared._Crescent.Factions;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Crescent.Factions;

/// <summary>
/// Lists the basic-kit items a faction equipment vendor offers, each with a Take button. The one-per-player
/// limit is enforced server-side, so the window simply relays the request and lets the server accept or refuse.
/// </summary>
public sealed class FactionEquipmentVendorWindow : FancyWindow
{
    private readonly BoxContainer _list;

    /// <summary>Raised with the item prototype id when the player presses a Take button.</summary>
    public event Action<string>? OnTake;

    public FactionEquipmentVendorWindow()
    {
        Title = Loc.GetString("faction-vendor-window-title");
        MinSize = new(420, 380);
        SetSize = new(460, 440);

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new(8),
        };
        scroll.AddChild(_list);
        ContentsContainer.AddChild(scroll);
    }

    public void SetItems(List<FactionVendorItem> items)
    {
        _list.RemoveAllChildren();

        if (items.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("faction-vendor-empty"), Margin = new(4) });
            return;
        }

        foreach (var item in items)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new(0, 0, 0, 6),
            };
            row.AddChild(new Label
            {
                Text = item.Name,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            });

            var take = new Button
            {
                Text = Loc.GetString("faction-vendor-take-button"),
                MinWidth = 100,
            };
            var itemId = item.ItemId;
            take.OnPressed += _ => OnTake?.Invoke(itemId);
            row.AddChild(take);

            _list.AddChild(row);
        }
    }
}
