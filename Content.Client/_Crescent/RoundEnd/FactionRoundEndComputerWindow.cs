using System;
using Content.Client.UserInterface.Controls;
using Content.Shared._Crescent.RoundEnd;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Crescent.RoundEnd;

/// <summary>
/// Lists a faction round-end computer's missions, each with its required items, live delivery progress and a
/// turn-in button (live only when the requirements are met). The one-and-done limit is enforced server-side, so
/// the window just relays the turn-in request.
/// </summary>
public sealed class FactionRoundEndComputerWindow : FancyWindow
{
    private readonly BoxContainer _list;

    /// <summary>Raised with the mission id when the player presses its turn-in button.</summary>
    public event Action<string>? OnSubmit;

    public FactionRoundEndComputerWindow()
    {
        Title = Loc.GetString("faction-roundend-window-title");
        MinSize = new(460, 420);
        SetSize = new(500, 520);

        // Horizontal scrolling off so the multi-paragraph briefings wrap to the window instead of running off it.
        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new(6),
        };
        scroll.AddChild(_list);
        ContentsContainer.AddChild(scroll);
    }

    public void SetState(FactionRoundEndConsoleState state)
    {
        _list.RemoveAllChildren();

        if (state.Missions.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("faction-roundend-no-missions"), Margin = new(4) });
            return;
        }

        foreach (var mission in state.Missions)
            _list.AddChild(BuildMission(mission));
    }

    private Control BuildMission(FactionMissionView mission)
    {
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new(8),
        };

        box.AddChild(new Label
        {
            Text = mission.Name,
            StyleClasses = { "LabelHeading" },
        });

        if (!string.IsNullOrWhiteSpace(mission.Description))
        {
            // RichTextLabel (not Label) so the briefing wraps across lines.
            var description = new RichTextLabel
            {
                HorizontalExpand = true,
                Margin = new(0, 2, 0, 6),
            };
            description.SetMessage(mission.Description, defaultColor: Color.DarkGray);
            box.AddChild(description);
        }

        foreach (var item in mission.Items)
        {
            var done = item.Delivered >= item.Required;
            box.AddChild(new Label
            {
                Text = $"    {item.Name}   {item.Delivered}/{item.Required}",
                FontColorOverride = done ? Color.LightGreen : Color.White,
            });
        }

        if (mission.Completed)
        {
            box.AddChild(new Label
            {
                Text = Loc.GetString("faction-roundend-status-completed"),
                FontColorOverride = Color.LightGreen,
                Margin = new(0, 6, 0, 0),
            });
        }
        else
        {
            var button = new Button
            {
                Text = Loc.GetString("faction-roundend-turn-in"),
                Disabled = !mission.Ready,
                HorizontalAlignment = HAlignment.Right,
                Margin = new(0, 6, 0, 0),
            };
            var id = mission.MissionId;
            button.OnPressed += _ => OnSubmit?.Invoke(id);
            box.AddChild(button);
        }

        var panel = new PanelContainer { Margin = new(0, 0, 0, 8) };
        panel.AddChild(box);
        return panel;
    }
}
