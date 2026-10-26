using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Styles;

namespace DOL.GS.Agents
{
    /// <summary>A zone of the realm, with every NPC that spawns in it and the bind areas its Characters can use.</summary>
    public sealed record WikiZone(ushort Id, ushort RegionId, string Name, IReadOnlyList<WikiNpc> Npcs, IReadOnlyList<WikiBindArea> BindAreas = null);

    /// <summary>Where the server lets a Character bind: anywhere within the radius of the point. Bind areas that overlap are one Bindstone.</summary>
    public sealed record WikiBindArea(int X, int Y, int Z, int Radius);

    /// <summary>
    /// One spawned NPC, where it spawns. A Mob is one a Character can fight. A merchant has wares, and anyone else has none.
    /// The role is its guild name, as the client shows it under the name.
    /// </summary>
    public sealed record WikiNpc(string Name, string Role, byte Level, bool Mob, int X, int Y, int Z, IReadOnlyList<WikiWare> Wares = null);

    /// <summary>One item a merchant sells, at the index the Mind's `buy` takes: its page times 30 plus its place on the page.</summary>
    public sealed record WikiWare(int Index, string Name, int Level, int DpsAf, int Speed, string ObjectType, long Price);

    /// <summary>
    /// A class of the realm, with the Specializations it trains that bring Spells or Styles. A class Characters start as is its own base class.
    /// `SpecPointsMultiplier` sets how many Specialization points it gets each level past 5: ten times as many as the level's number.
    /// </summary>
    public sealed record WikiClass(string Name, string BaseName, int SpecPointsMultiplier, IReadOnlyList<WikiSpecialization> Specializations);

    /// <summary>One Specialization, by its database name as `skills` and `/train` name it, from the Character level the class gets it, with its Spell lines, baseline first, and its Styles.</summary>
    public sealed record WikiSpecialization(string Name, int Level, IReadOnlyList<WikiSpellLine> SpellLines, IReadOnlyList<WikiStyle> Styles);

    /// <summary>A baseline Spell line gives its Spells at the Character's level, and any other at the Specialization's.</summary>
    public sealed record WikiSpellLine(string Name, bool Baseline, IReadOnlyList<WikiSpell> Spells);

    /// <summary>One Spell, with what its tooltip shows, or nothing when the server has no handler for it.</summary>
    public sealed record WikiSpell(string Name, int Level, SpellDetails Details);

    /// <summary>One Style, at the Specialization level it needs, with what its tooltip shows and what each Spell it adds does.</summary>
    public sealed record WikiStyle(string Name, int Level, StyleDetails Details, IReadOnlyList<string> Adds);

    /// <summary>
    /// The Wiki: game data a Human would look up on a fan site, written out for every Agent (ADR 0006).
    /// It's static: where NPCs spawn, not where they are now.
    /// </summary>
    public static class Wiki
    {
        // How many of a Mob's spawn points a zone page lists.
        private const int SPAWN_POINTS_LISTED = 5;

        /// <summary>Every page of the realm's Wiki in markdown, by its path relative to the Wiki's folder.</summary>
        public static IReadOnlyDictionary<string, string> Pages(string realm, IEnumerable<WikiZone> zones, IEnumerable<WikiClass> classes = null)
        {
            List<WikiClass> classList = (classes ?? []).OrderBy(static characterClass => characterClass.Name, StringComparer.Ordinal).ToList();
            Dictionary<string, string> pages = [];
            List<WikiZone> list = zones
                .Where(static zone => zone.Npcs.Count > 0)
                .OrderBy(static zone => zone.Npcs.Where(static npc => npc.Mob).Select(static npc => (int) npc.Level).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(static zone => zone.Name, StringComparer.Ordinal)
                .ThenBy(static zone => zone.Id)
                .ToList();
            Dictionary<object, string> paths = Paths(list);
            pages["index.md"] = Index(realm, list, paths, classList);

            foreach (WikiZone zone in list)
            {
                pages[paths[zone]] = ZonePage(realm, zone, paths);

                foreach (WikiNpc merchant in Merchants(zone))
                    pages[paths[merchant]] = MerchantPage(merchant, zone, paths);
            }

            foreach (WikiClass characterClass in classList)
                pages[$"classes/{Slug(characterClass.Name)}.md"] = ClassPage(realm, characterClass, classList);

            return pages;
        }

        // Each zone's and merchant's page, by the zone or merchant. Zones of the same name are told apart by their ID, and merchants of the same name in a zone by a number.
        private static Dictionary<object, string> Paths(List<WikiZone> zones)
        {
            Dictionary<object, string> paths = new(ReferenceEqualityComparer.Instance);
            HashSet<string> shared = zones.GroupBy(static zone => zone.Name).Where(static same => same.Count() > 1).Select(static same => same.Key).ToHashSet();

            foreach (WikiZone zone in zones)
            {
                string zoneSlug = shared.Contains(zone.Name) ? $"{Slug(zone.Name)}-{zone.Id}" : Slug(zone.Name);
                paths[zone] = $"zones/{zoneSlug}.md";

                foreach (IGrouping<string, WikiNpc> same in Merchants(zone).GroupBy(static merchant => merchant.Name))
                {
                    int n = 0;

                    foreach (WikiNpc merchant in same)
                        paths[merchant] = $"merchants/{Slug(merchant.Name)}{(++n == 1 ? "" : $"-{n}")}-{zoneSlug}.md";
                }
            }

            return paths;
        }

        /// <summary>
        /// The realm's zones as the server has loaded them, each with the NPCs that spawn in it.
        /// Pets and NPCs a client can't target or name are left out, as a Human wouldn't see them. A Mob is an NPC of no realm that isn't peaceful, and not a merchant.
        /// A dungeon region with no realm NPCs in it belongs to no realm, so it's left out.
        /// Bind areas are read from the database rows the regions load theirs from, and only those of no realm or the realm's own, as only those let its Characters bind.
        /// </summary>
        public static List<WikiZone> FromWorld(eRealm realm)
        {
            List<WikiZone> zones = [];
            ILookup<int, DbBindPoint> bindAreas = GameServer.Database.SelectAllObjects<DbBindPoint>()
                .Where(area => area.Realm == 0 || area.Realm == (int) realm)
                .ToLookup(static area => area.Region);

            foreach (Region region in WorldMgr.GetAllRegions())
            {
                // The database may leave a zone's realm unset. Such a zone belongs to the realm most of its region's realm NPCs, such as guards, merchants and trainers, belong to.
                eRealm regionRealm = WorldMgr.GetNPCsFromRegion(region.ID)
                    .Where(static npc => npc.Realm is eRealm.Albion or eRealm.Midgard or eRealm.Hibernia)
                    .GroupBy(static npc => npc.Realm)
                    .OrderByDescending(static npcs => npcs.Count())
                    .Select(static npcs => npcs.Key)
                    .FirstOrDefault(eRealm.None);
                List<Zone> realmZones = region.Zones.Where(zone => zone.Realm == realm || (zone.Realm == eRealm.None && regionRealm == realm)).ToList();

                if (realmZones.Count == 0)
                    continue;

                Dictionary<Zone, List<WikiNpc>> npcs = realmZones.ToDictionary(static zone => zone, static _ => new List<WikiNpc>());

                foreach (GameNPC npc in WorldMgr.GetNPCsFromRegion(region.ID))
                {
                    if (npc.Brain is IControlledBrain || (npc.Flags & (GameNPC.eFlags.CANTTARGET | GameNPC.eFlags.DONTSHOWNAME)) != 0 || string.IsNullOrEmpty(npc.Name))
                        continue;

                    Point3D spawn = npc.SpawnPoint;

                    if (region.GetZone(spawn.X, spawn.Y) is not Zone zone || !npcs.TryGetValue(zone, out List<WikiNpc> list))
                        continue;

                    List<WikiWare> wares = npc is GameMerchant { TradeItems: MerchantTradeItems tradeItems } ? Wares(tradeItems) : null;
                    bool mob = wares == null && npc.Realm == eRealm.None && (npc.Flags & GameNPC.eFlags.PEACE) == 0;
                    list.Add(new WikiNpc(npc.Name, npc.GuildName ?? "", npc.Level, mob, spawn.X, spawn.Y, spawn.Z, wares));
                }

                Dictionary<Zone, List<WikiBindArea>> zoneBindAreas = realmZones.ToDictionary(static zone => zone, static _ => new List<WikiBindArea>());

                foreach (DbBindPoint area in bindAreas[region.ID])
                {
                    if (region.GetZone(area.X, area.Y) is Zone zone && zoneBindAreas.TryGetValue(zone, out List<WikiBindArea> list))
                        list.Add(new WikiBindArea(area.X, area.Y, area.Z, area.Radius));
                }

                foreach ((Zone zone, List<WikiNpc> list) in npcs)
                    zones.Add(new WikiZone(zone.ID, region.ID, zone.Description, list, zoneBindAreas[zone]));
            }

            return zones;
        }

        // As the merchant window lists them (see `PerceptProjection.MerchantWindow`).
        private static List<WikiWare> Wares(MerchantTradeItems tradeItems)
        {
            List<WikiWare> wares = [];

            for (int page = 0; page < MerchantTradeItems.MAX_PAGES_IN_TRADEWINDOWS; page++)
            {
                System.Collections.IDictionary itemsInPage = tradeItems.GetItemsInPage(page);

                if (itemsInPage == null)
                    continue;

                for (int place = 0; place < MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS; place++)
                {
                    if (itemsInPage[place] is DbItemTemplate item)
                        wares.Add(new WikiWare(page * MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS + place, item.Name, item.Level, item.DPS_AF, item.SPD_ABS, ((eObjectType) item.Object_Type).ToString(), item.Price));
                }
            }

            return wares;
        }

        /// <summary>
        /// The realm's classes as the server knows them, each with what its trainer would show its Specializations bring, up to the highest level. A class belongs to the realm of its races.
        /// Spell lines and Styles are picked as `Specialization` picks them for a Character of the class: those for the class itself if there are any, otherwise those for any class.
        /// A class Characters start as gets only baseline lines. Champion and Master Level lines are in no class's career, so they're left out, as are Specializations that bring only abilities.
        /// </summary>
        public static List<WikiClass> ClassesFromServer(eRealm realm)
        {
            List<WikiClass> classes = [];
            // Tooltips describe a Spell as someone casts it, but none of what the Wiki shows depends on who.
            GameNPC caster = new();

            foreach (eCharacterClass id in Enum.GetValues<eCharacterClass>())
            {
                if (id is eCharacterClass.Unknown || ScriptMgr.FindCharacterClass((int) id) is not ICharacterClass characterClass || characterClass.EligibleRaces.All(race => race.Realm != realm))
                    continue;

                List<(Specialization Spec, int Level, List<SpellLine> Lines, List<Style> Styles)> specs = SkillBase.GetSpecializationCareer(characterClass.ID)
                    .OrderBy(static career => career.Value)
                    .ThenBy(static career => career.Key.ID)
                    .Select(career => (career.Key, career.Value, SpellLines(career.Key, characterClass), Styles(career.Key, characterClass.ID)))
                    .Where(static spec => spec.Item3.Count > 0 || spec.Item4.Count > 0)
                    .ToList();

                if (specs.Count == 0)
                    continue;

                List<Style> allStyles = [.. specs.SelectMany(static spec => spec.Styles)];
                List<WikiSpecialization> wikiSpecs = specs.Select(spec => new WikiSpecialization(
                    spec.Spec.KeyName,
                    spec.Level,
                    [.. spec.Lines.Select(line => new WikiSpellLine(line.Name, line.IsBaseLine, [.. SkillBase.GetSpellList(line.KeyName)
                        .OrderBy(static spell => spell.Level)
                        .ThenBy(static spell => spell.ID)
                        .Select(spell => new WikiSpell(spell.Name, spell.Level, Details(caster, spell)))]))],
                    [.. spec.Styles.Select(style => new WikiStyle(style.Name, style.SpecLevelRequirement, PerceptProjection.Details(style, allStyles), Adds(style, characterClass.ID, caster)))]))
                    .ToList();
                classes.Add(new WikiClass(characterClass.Name, characterClass.BaseName, characterClass.SpecPointsMultiplier, wikiSpecs));
            }

            return classes;
        }

        // As `Specialization.GetSpellLinesForLiving` picks them: baseline lines first, then the others, which only a class that has advanced from its base class gets.
        private static List<SpellLine> SpellLines(Specialization spec, ICharacterClass characterClass)
        {
            IList<Tuple<SpellLine, int>> lines = SkillBase.GetSpecsSpellLines(spec.KeyName);
            List<SpellLine> picked = Pick(true);

            if (characterClass.HasAdvancedFromBaseClass())
                picked.AddRange(Pick(false));

            return picked;

            List<SpellLine> Pick(bool baseline)
            {
                List<Tuple<SpellLine, int>> ofKind = lines.Where(line => line.Item1.IsBaseLine == baseline).ToList();
                List<Tuple<SpellLine, int>> forClass = ofKind.Where(line => line.Item2 == characterClass.ID).ToList();
                return [.. (forClass.Count > 0 ? forClass : ofKind.Where(static line => line.Item2 == 0)).Select(static line => line.Item1).OrderBy(static line => line.ID)];
            }
        }

        // As `Specialization.GetStylesForLiving` picks them.
        private static List<Style> Styles(Specialization spec, int classId)
        {
            List<Style> styles = SkillBase.GetStyleList(spec.KeyName, classId);

            if (styles.Count == 0)
                styles = SkillBase.GetStyleList(spec.KeyName, 0);

            return [.. styles.OrderBy(static style => style.SpecLevelRequirement).ThenBy(static style => style.ID)];
        }

        // What each Spell a Style adds does, as `StyleProcessor` picks them for the class: its own if it has any, otherwise those for any class. A Style with random procs adds one of them.
        private static List<string> Adds(Style style, int classId, GameLiving caster)
        {
            List<StyleProcInfo> procs = style.RandomProc ? style.Procs : style.Procs.Where(proc => proc.ClassId == classId).ToList();

            if (procs.Count == 0)
                procs = style.Procs.Where(proc => proc.ClassId == 0 || proc.ClassId == style.ClassID).ToList();

            return [.. procs.Select(proc =>
            {
                string add = $"{proc.Spell.Name}: {Details(caster, proc.Spell)?.Description}".TrimEnd(' ', ':');

                if (style.RandomProc)
                    return $"{add} (one of these at random)";

                return proc.Chance < 100 ? $"{add} ({proc.Chance}% chance)" : add;
            })];
        }

        // What the Spell's tooltip shows. Some handlers describe a Spell only for a player casting it, and fail for anyone else; those Spells go without.
        private static SpellDetails Details(GameLiving caster, Spell spell)
        {
            try
            {
                return PerceptProjection.Details(caster, spell);
            }
            catch (Exception e) when (e is NullReferenceException or InvalidCastException)
            {
                return null;
            }
        }

        /// <summary>Writes the pages into `folder`, replacing the Wiki there.</summary>
        public static void Write(string folder, IReadOnlyDictionary<string, string> pages)
        {
            foreach (string old in new[] { "zones", "merchants", "classes" }.Select(sub => Path.Combine(folder, sub)).Where(Directory.Exists))
                Directory.Delete(old, true);

            foreach ((string path, string page) in pages)
            {
                string file = Path.Combine(folder, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, page);
            }
        }

        private static string Index(string realm, List<WikiZone> zones, Dictionary<object, string> paths, List<WikiClass> classes)
        {
            StringBuilder page = new();
            page.Append($"# {realm}\n\n");
            page.Append("The realm's zones, those with the lowest Mobs first. Each zone's page lists its Mobs, other NPCs and Bindstones, and each merchant's page what it sells.\n\n");
            page.Append("| Zone | Mob levels | Merchants |\n|---|---|---|\n");

            foreach (WikiZone zone in zones)
            {
                List<WikiNpc> mobs = zone.Npcs.Where(static npc => npc.Mob).ToList();
                string merchants = string.Join(", ", Merchants(zone).Select(merchant => $"[{merchant.Name}]({paths[merchant]})"));
                page.Append($"| [{zone.Name}]({paths[zone]}) | {(mobs.Count == 0 ? "none" : Levels(mobs))} | {merchants} |\n");
            }

            if (classes.Count == 0)
                return page.ToString();

            page.Append("\n## Classes\n\n");
            page.Append("Each class's page lists the Spells and Styles its Specializations bring.\n\n");
            page.Append("| Class | Starts as |\n|---|---|\n");

            foreach (WikiClass characterClass in classes)
            {
                string baseClass = characterClass.BaseName == characterClass.Name ? "" : ClassName(characterClass.BaseName, classes, "classes/");
                page.Append($"| {ClassName(characterClass.Name, classes, "classes/")} | {baseClass} |\n");
            }

            return page.ToString();
        }

        private static string ClassPage(string realm, WikiClass characterClass, List<WikiClass> classes)
        {
            StringBuilder page = new();
            page.Append($"# {characterClass.Name}\n\n");
            page.Append(characterClass.BaseName == characterClass.Name
                ? $"A class of {realm} that Characters start as. "
                : $"A class of {realm} that starts as a {ClassName(characterClass.BaseName, classes, "")}. ");
            page.Append("Each Specialization lists the Spells and Styles it brings. ");
            page.Append("A baseline Spell line gives its Spells as the Character levels, at the Character levels shown; any other gives them as the Specialization rises, at the Specialization levels shown.\n");

            foreach (WikiSpecialization spec in characterClass.Specializations)
            {
                page.Append($"\n## {spec.Name}\n");

                if (spec.Level > 1)
                    page.Append($"\nFrom level {spec.Level}.\n");

                foreach (WikiSpellLine line in spec.SpellLines.Where(static line => line.Spells.Count > 0))
                {
                    page.Append($"\n### {line.Name}{(line.Baseline ? " (baseline)" : "")}\n\n");
                    page.Append("| Level | Spell | Target | Cost | Casting time | Range | Radius | Duration | Recast | Magic type | What it does |\n|---|---|---|---|---|---|---|---|---|---|---|\n");

                    foreach (WikiSpell spell in line.Spells)
                    {
                        SpellDetails d = spell.Details;
                        string cost = d switch
                        {
                            { ConcentrationCost: int concentration } => $"{concentration} concentration",
                            { PowerCost: < 0 and int percent } => $"{-percent}% of max power",
                            { PowerCost: int power } => $"{power} power",
                            _ => ""
                        };
                        string casting = d == null ? "" : d.CastingSeconds == 0 ? "instant" : $"{Number(d.CastingSeconds)} s";
                        string duration = d?.DurationSeconds is float seconds ? $"{Number(seconds)} s" : "";
                        string recast = d?.RecastSeconds is float wait ? $"{Number(wait)} s" : "";
                        page.Append($"| {spell.Level} | {Cell(spell.Name)} | {d?.Target} | {cost} | {casting} | {d?.Range} | {d?.Radius} | {duration} | {recast} | {d?.MagicType} | {Cell(d?.Description)} |\n");
                    }
                }

                if (spec.Styles.Count == 0)
                    continue;

                page.Append("\n### Styles\n\n");
                page.Append("| Level | Style | Opening | Weapon | Endurance | To hit | Defence | Growth | Opens | Adds |\n|---|---|---|---|---|---|---|---|---|---|\n");

                foreach (WikiStyle style in spec.Styles)
                {
                    StyleDetails d = style.Details;
                    page.Append($"| {style.Level} | {Cell(style.Name)} | {Cell(Opening(d))} | {Cell(d.Weapon)} | {d.Fatigue} | {d.AttackMod} | {d.DefensiveMod} | {Number(d.OpeningDamage)} | {Cell(string.Join(", ", d.FollowupStyles))} | {Cell(string.Join("; ", style.Adds))} |\n");
                }
            }

            // `GamePlayer.GainExperience` keeps a Character of a class Characters start as at level 5 until a trainer makes it another.
            bool startClass = characterClass.BaseName == characterClass.Name;
            page.Append("\n## Specialization points\n\n");
            page.Append("Raising a Specialization from level n to n + 1 costs n + 1 points. ");
            page.Append(startClass
                ? "These are the points a Character of this class gets as it levels, up to level 5: it can't level past that until a trainer makes it one of the classes that start as it.\n\n"
                : "These are the points a Character of this class gets as it levels, and from level 40 also halfway through each level.\n\n");
            page.Append("| Level | Points | Total |\n|---|---|---|\n");
            int total = 0;

            foreach ((string level, int points) in SpecPoints(characterClass.SpecPointsMultiplier, startClass ? 5 : GamePlayer.MAX_LEVEL))
            {
                total += points;
                page.Append($"| {level} | {points} | {total} |\n");
            }

            return page.ToString();
        }

        // As `GamePlayer.VerifySpecPoints` counts them: none at level 1, the level's number up to 5, then the multiplier times the level over 10,
        // and halfway through each level from 40, which `GamePlayer.OnLevelSecondStage` gives, half that.
        private static IEnumerable<(string Level, int Points)> SpecPoints(int multiplier, int lastLevel)
        {
            for (int level = 1; level <= lastLevel; level++)
            {
                yield return ($"{level}", level == 1 ? 0 : level <= 5 ? level : multiplier * level / 10);

                if (level >= 40 && level < GamePlayer.MAX_LEVEL)
                    yield return ($"{level}.5", multiplier * level / 20);
            }
        }

        // A class's name, linked to its page when it has one. `folder` is the way from the page that links to the class pages.
        private static string ClassName(string name, List<WikiClass> classes, string folder)
        {
            return classes.Any(characterClass => characterClass.Name == name) ? $"[{name}]({folder}{Slug(name)}.md)" : name;
        }

        // What a Style needs before it can be used, in words.
        private static string Opening(StyleDetails details)
        {
            string opening = (details.Opening, details.OpeningResult) switch
            {
                (Style.eOpening.Positional, _) => $"from the target's {details.OpeningPosition.ToString().ToLowerInvariant()}",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Any) => "",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Style) => $"after {details.OpeningStyle}",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Hit) => "after you hit",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Miss) => "after you miss",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Fumble) => "after you fumble",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Parry) => "after the target parries",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Block) => "after the target blocks",
                (Style.eOpening.Offensive, Style.eAttackResultRequirement.Evade) => "after the target evades",
                (_, Style.eAttackResultRequirement.Any) => "after the target attacks you",
                (_, Style.eAttackResultRequirement.Hit) => "after the target hits you",
                (_, Style.eAttackResultRequirement.Miss) => "after the target misses you",
                (_, Style.eAttackResultRequirement.Fumble) => "after the target fumbles",
                (_, Style.eAttackResultRequirement.Style) => "after the target's style",
                (_, Style.eAttackResultRequirement.Parry) => "after you parry",
                (_, Style.eAttackResultRequirement.Block) => "after you block",
                (_, Style.eAttackResultRequirement.Evade) => "after you evade",
                _ => ""
            };

            if (!details.Hidden)
                return opening;

            return opening.Length == 0 ? "while hidden" : $"{opening}, while hidden";
        }

        // A table cell can't hold a line break or an unescaped pipe.
        private static string Cell(string text) => text?.ReplaceLineEndings(" ").Replace("|", "\\|");

        private static string Number(float number) => number.ToString(CultureInfo.InvariantCulture);

        private static string MerchantPage(WikiNpc merchant, WikiZone zone, Dictionary<object, string> paths)
        {
            StringBuilder page = new();
            page.Append($"# {merchant.Name}\n\n");
            string role = merchant.Role.Length == 0 ? "A merchant" : $"{merchant.Role}, a merchant";
            page.Append($"{role} in [{zone.Name}](../{paths[zone]}), at {At(merchant)}. The index is what `buy` takes.\n");

            foreach (IGrouping<int, WikiWare> wares in merchant.Wares.OrderBy(static ware => ware.Index).GroupBy(static ware => ware.Index / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS))
            {
                page.Append($"\n## Page {wares.Key + 1}\n\n");
                page.Append("| Index | Item | Level | DPS/AF | Speed | Object type | Price (copper) |\n|---|---|---|---|---|---|---|\n");

                foreach (WikiWare ware in wares)
                    page.Append($"| {ware.Index} | {ware.Name} | {ware.Level} | {ware.DpsAf} | {ware.Speed} | {ware.ObjectType} | {ware.Price} |\n");
            }

            return page.ToString();
        }

        private static IEnumerable<WikiNpc> Merchants(WikiZone zone) => zone.Npcs.Where(static npc => npc.Wares != null).OrderBy(static npc => npc.Name, StringComparer.Ordinal);

        private static string ZonePage(string realm, WikiZone zone, Dictionary<object, string> paths)
        {
            StringBuilder page = new();
            page.Append($"# {zone.Name}\n\n");
            page.Append($"Zone {zone.Id} in region {zone.RegionId}, in {realm}. Positions are global, as `/gloc` gives them.\n\n");
            page.Append("## Mobs\n\n");
            page.Append("| Mob | Levels | How many | Spawn points |\n|---|---|---|---|\n");

            foreach (IGrouping<string, WikiNpc> mobs in zone.Npcs.Where(static npc => npc.Mob).GroupBy(static npc => npc.Name).OrderBy(static mobs => mobs.Min(static mob => mob.Level)).ThenBy(static mobs => mobs.Key, StringComparer.Ordinal))
                page.Append($"| {mobs.Key} | {Levels(mobs)} | {mobs.Count()} | {SpawnPoints(mobs.ToList())} |\n");

            page.Append("\n## NPCs\n\n");
            page.Append("| NPC | Role | Level | Position |\n|---|---|---|---|\n");

            foreach (WikiNpc npc in zone.Npcs.Where(static npc => !npc.Mob).OrderBy(static npc => npc.Name, StringComparer.Ordinal))
            {
                string role = npc.Wares == null ? npc.Role : string.Join(", ", new[] { npc.Role, $"[merchant](../{paths[npc]})" }.Where(static part => part.Length > 0));
                page.Append($"| {npc.Name} | {role} | {npc.Level} | {At(npc)} |\n");
            }

            page.Append("\n## Bindstones\n\n");

            if (zone.BindAreas is not { Count: > 0 })
                page.Append("None in this zone.\n");
            else
            {
                foreach ((int x, int y, int z) in Bindstones(zone.BindAreas))
                    page.Append($"- {At(x, y, z)}\n");
            }

            return page.ToString();
        }

        // One per Bindstone, west to east. The database may have several bind areas for one; those that overlap, even through others, are one, at their average position.
        private static IEnumerable<(int X, int Y, int Z)> Bindstones(IEnumerable<WikiBindArea> areas)
        {
            List<List<WikiBindArea>> bindstones = [];

            foreach (WikiBindArea area in areas)
            {
                List<List<WikiBindArea>> overlapping = bindstones.Where(bindstone => bindstone.Any(other => Overlap(area, other))).ToList();
                List<WikiBindArea> merged = [area, .. overlapping.SelectMany(static bindstone => bindstone)];
                bindstones.RemoveAll(overlapping.Contains);
                bindstones.Add(merged);
            }

            return bindstones
                .Select(static bindstone => (X: (int) bindstone.Average(static area => (long) area.X), Y: (int) bindstone.Average(static area => (long) area.Y), Z: (int) bindstone.Average(static area => (long) area.Z)))
                .OrderBy(static bindstone => bindstone.X)
                .ThenBy(static bindstone => bindstone.Y);

            // A bind area is a sphere, as the server checks height too.
            static bool Overlap(WikiBindArea a, WikiBindArea b)
            {
                long dx = a.X - b.X;
                long dy = a.Y - b.Y;
                long dz = a.Z - b.Z;
                long reach = a.Radius + b.Radius;
                return dx * dx + dy * dy + dz * dz < reach * reach;
            }
        }

        private static string Levels(IEnumerable<WikiNpc> mobs)
        {
            int min = mobs.Min(static mob => mob.Level);
            int max = mobs.Max(static mob => mob.Level);
            return min == max ? $"{min}" : $"{min}–{max}";
        }

        // At most a few of them, spread across them from west to east.
        private static string SpawnPoints(List<WikiNpc> mobs)
        {
            List<WikiNpc> sorted = mobs.OrderBy(static mob => mob.X).ThenBy(static mob => mob.Y).ToList();

            if (sorted.Count <= SPAWN_POINTS_LISTED)
                return string.Join("; ", sorted.Select(At));

            IEnumerable<WikiNpc> spread = Enumerable.Range(0, SPAWN_POINTS_LISTED).Select(i => sorted[i * (sorted.Count - 1) / (SPAWN_POINTS_LISTED - 1)]);
            return $"{string.Join("; ", spread.Select(At))}; and {sorted.Count - SPAWN_POINTS_LISTED} more";
        }

        private static string At(WikiNpc npc) => At(npc.X, npc.Y, npc.Z);

        private static string At(int x, int y, int z) => $"X:{x} Y:{y} Z:{z}";

        // `Lough Derg` becomes `lough-derg`.
        private static string Slug(string name)
        {
            StringBuilder slug = new();

            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    slug.Append(c);
                else if (slug.Length > 0 && slug[^1] != '-')
                    slug.Append('-');
            }

            return slug.ToString().TrimEnd('-');
        }
    }
}
