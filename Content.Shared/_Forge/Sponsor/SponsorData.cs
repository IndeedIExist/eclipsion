
namespace Content.Shared._Forge.Sponsors;

public sealed class SponsorData
{
    public static readonly Dictionary<string, SponsorLevel> RolesMap = new()
    {
        { "1356590198628155526", SponsorLevel.Level1 }, // Ensign
        { "1460211939312537630", SponsorLevel.Level2 }, // Private
        { "1460212133290836009", SponsorLevel.Level3 }, // Sergeant
        { "1460212173086130339", SponsorLevel.Level4 }, // Major
        { "1460212230539710639", SponsorLevel.Level5 }, // Admiral
        { "1460212505778454588", SponsorLevel.Level6 }, // Reserve1
        { "1460212525663785145", SponsorLevel.Level7 }, // Reserve2
        { "1460212547239280650", SponsorLevel.Level8 } // Reserve3
    };

    public static readonly Dictionary<SponsorLevel, string> SponsorColor = new()
    {
        { SponsorLevel.Level1, "#6bb9f0" },
        { SponsorLevel.Level2, "#FACC15" },
        { SponsorLevel.Level3, "#FB923C" },
        { SponsorLevel.Level4, "#F75656" },
        { SponsorLevel.Level5, "#FFFFFF" }
    };

    public static readonly Dictionary<SponsorLevel, string> SponsorGhost = new()
    {
        { SponsorLevel.Level2, "MobObserverLevel1" },
		{ SponsorLevel.Level3, "MobObserverLevel2" },
        { SponsorLevel.Level4, "MobObserverLevel3" },
        { SponsorLevel.Level5, "MobObserverLevel4" }
    };

    public static SponsorLevel ParseRoles(List<string> roles)
    {
        var highestRole = SponsorLevel.None;
        foreach (var role in roles)
        {
            if (RolesMap.ContainsKey(role))
                if ((byte) RolesMap[role] > (byte) highestRole)
                    highestRole = RolesMap[role];
        }

        return highestRole;
    }
}

public enum SponsorLevel : byte
{
    None = 0,
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5,
    Level6 = 6,
    Level7 = 7,
    Level8 = 8
}
