using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DOL.AI.Brain;
using DOL.Database;

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
    /// The Wiki: game data a Human would look up on a fan site, written out for every Agent (ADR 0006).
    /// It's static: where NPCs spawn, not where they are now.
    /// </summary>
    public static class Wiki
    {
        // How many of a Mob's spawn points a zone page lists.
        private const int SPAWN_POINTS_LISTED = 5;

        /// <summary>Every page of the realm's Wiki in markdown, by its path relative to the Wiki's folder.</summary>
        public static IReadOnlyDictionary<string, string> Pages(string realm, IEnumerable<WikiZone> zones)
        {
            Dictionary<string, string> pages = [];
            List<WikiZone> list = zones
                .Where(static zone => zone.Npcs.Count > 0)
                .OrderBy(static zone => zone.Npcs.Where(static npc => npc.Mob).Select(static npc => (int) npc.Level).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(static zone => zone.Name, StringComparer.Ordinal)
                .ThenBy(static zone => zone.Id)
                .ToList();
            Dictionary<object, string> paths = Paths(list);
            pages["index.md"] = Index(realm, list, paths);

            foreach (WikiZone zone in list)
            {
                pages[paths[zone]] = ZonePage(realm, zone, paths);

                foreach (WikiNpc merchant in Merchants(zone))
                    pages[paths[merchant]] = MerchantPage(merchant, zone, paths);
            }

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

        /// <summary>Writes the pages into `folder`, replacing the Wiki there.</summary>
        public static void Write(string folder, IReadOnlyDictionary<string, string> pages)
        {
            foreach (string old in new[] { "zones", "merchants" }.Select(sub => Path.Combine(folder, sub)).Where(Directory.Exists))
                Directory.Delete(old, true);

            foreach ((string path, string page) in pages)
            {
                string file = Path.Combine(folder, path);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, page);
            }
        }

        private static string Index(string realm, List<WikiZone> zones, Dictionary<object, string> paths)
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

            return page.ToString();
        }

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
