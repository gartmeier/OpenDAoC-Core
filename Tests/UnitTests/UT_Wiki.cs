using System.Collections.Generic;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Wiki: a realm's zones with their Mobs and NPCs, and its merchants with their wares, as a Human would read them on a fan site (ADR 0006).
    /// </summary>
    [TestFixture]
    public class UT_Wiki
    {
        private static readonly WikiZone LOUGH_DERG = new(202, 200, "Lough Derg",
        [
            new WikiNpc("mudman", "", 3, true, 296300, 642400, 4848),
            new WikiNpc("mudman", "", 2, true, 296900, 643100, 4850),
            new WikiNpc("wild boar", "", 1, true, 294000, 640000, 4900),
            new WikiNpc("Allistar", "Blademaster Trainer", 50, false, 295500, 641500, 4848),
            new WikiNpc("Eira", "Blades", 20, false, 294899, 641231, 4848,
            [
                new WikiWare(0, "short sword", 1, 16, 26, "SlashingWeapon", 150),
                new WikiWare(1, "falcata", 2, 18, 28, "SlashingWeapon", 225),
                new WikiWare(30, "rawhide jerkin", 2, 4, 0, "Leather", 120),
            ]),
        ]);

        private static readonly WikiZone SHANNON_ESTUARY = new(203, 200, "Shannon Estuary",
        [
            new WikiNpc("river sprite", "", 8, true, 300000, 650000, 4000),
            new WikiNpc("river sprite", "", 6, true, 301000, 651000, 4000),
        ]);

        private static IReadOnlyDictionary<string, string> Pages() => Wiki.Pages("Hibernia", [SHANNON_ESTUARY, LOUGH_DERG]);

        [Test]
        public void ZonePage_ShouldListMobsByName_LowestLevelFirst_ThenOtherNpcs()
        {
            Assert.That(Pages()["zones/lough-derg.md"], Is.EqualTo(
                """
                # Lough Derg

                Zone 202 in region 200, in Hibernia. Positions are global, as `/gloc` gives them.

                ## Mobs

                | Mob | Levels | How many | Spawn points |
                |---|---|---|---|
                | wild boar | 1 | 1 | X:294000 Y:640000 Z:4900 |
                | mudman | 2–3 | 2 | X:296300 Y:642400 Z:4848; X:296900 Y:643100 Z:4850 |

                ## NPCs

                | NPC | Role | Level | Position |
                |---|---|---|---|
                | Allistar | Blademaster Trainer | 50 | X:295500 Y:641500 Z:4848 |
                | Eira | Blades, [merchant](../merchants/eira-lough-derg.md) | 20 | X:294899 Y:641231 Z:4848 |

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void Index_ShouldListZonesLowestMobsFirst_WithTheirMerchants()
        {
            Assert.That(Pages()["index.md"], Is.EqualTo(
                """
                # Hibernia

                The realm's zones, those with the lowest Mobs first. Each zone's page lists its Mobs and other NPCs, and each merchant's page what it sells.

                | Zone | Mob levels | Merchants |
                |---|---|---|
                | [Lough Derg](zones/lough-derg.md) | 1–3 | [Eira](merchants/eira-lough-derg.md) |
                | [Shannon Estuary](zones/shannon-estuary.md) | 6–8 |  |

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void MerchantPage_ShouldListWaresByPage_WithTheIndexBuyTakes()
        {
            Assert.That(Pages()["merchants/eira-lough-derg.md"], Is.EqualTo(
                """
                # Eira

                Blades, a merchant in [Lough Derg](../zones/lough-derg.md), at X:294899 Y:641231 Z:4848. The index is what `buy` takes.

                ## Page 1

                | Index | Item | Level | DPS/AF | Speed | Object type | Price (copper) |
                |---|---|---|---|---|---|---|
                | 0 | short sword | 1 | 16 | 26 | SlashingWeapon | 150 |
                | 1 | falcata | 2 | 18 | 28 | SlashingWeapon | 225 |

                ## Page 2

                | Index | Item | Level | DPS/AF | Speed | Object type | Price (copper) |
                |---|---|---|---|---|---|---|
                | 30 | rawhide jerkin | 2 | 4 | 0 | Leather | 120 |

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void ZonePage_ShouldListAtMostFiveSpawnPointsOfAMob_SpreadAcrossThem()
        {
            List<WikiNpc> crabs = [];

            for (int i = 0; i < 12; i++)
                crabs.Add(new WikiNpc("sand crab", "", 1, true, 290000 + i * 100, 640000, 4700));

            string page = Wiki.Pages("Hibernia", [new WikiZone(202, 200, "Shannon Estuary", crabs)])["zones/shannon-estuary.md"];

            Assert.That(page, Does.Contain("| sand crab | 1 | 12 | X:290000 Y:640000 Z:4700; X:290200 Y:640000 Z:4700; X:290500 Y:640000 Z:4700; X:290800 Y:640000 Z:4700; X:291100 Y:640000 Z:4700; and 7 more |"));
        }

        [Test]
        public void Pages_ShouldTellApartZonesAndMerchantsOfTheSameName_AndLeaveOutZonesWithNoNpcs()
        {
            WikiZone first = new(10, 30, "Arbor Glen", [new WikiNpc("Twm", "Poison", 20, false, 1, 2, 3, []), new WikiNpc("Twm", "Arrows", 20, false, 4, 5, 6, [])]);
            WikiZone second = new(11, 31, "Arbor Glen", [new WikiNpc("rat", "", 1, true, 7, 8, 9)]);
            WikiZone empty = new(12, 32, "Dummy Zone", []);

            IReadOnlyDictionary<string, string> pages = Wiki.Pages("Hibernia", [first, second, empty]);

            Assert.That(pages.Keys, Is.EquivalentTo(new[] { "index.md", "zones/arbor-glen-10.md", "zones/arbor-glen-11.md", "merchants/twm-arbor-glen-10.md", "merchants/twm-2-arbor-glen-10.md" }));
            Assert.That(pages["merchants/twm-2-arbor-glen-10.md"], Does.Contain("Arrows, a merchant"));
        }
    }
}
