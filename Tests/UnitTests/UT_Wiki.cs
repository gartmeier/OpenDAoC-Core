using System.Collections.Generic;
using DOL.GS.Agents;
using DOL.GS.Styles;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Wiki: a realm's zones with their Mobs and NPCs, its merchants with their wares, and its classes with their Spells and Styles, as a Human would read them on a fan site (ADR 0006).
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
        ],
        [
            new WikiBindArea(295943, 642344, 4848, 750),
        ]);

        private static readonly WikiZone SHANNON_ESTUARY = new(203, 200, "Shannon Estuary",
        [
            new WikiNpc("river sprite", "", 8, true, 300000, 650000, 4000),
            new WikiNpc("river sprite", "", 6, true, 301000, 651000, 4000),
        ]);

        private static IReadOnlyDictionary<string, string> Pages() => Wiki.Pages("Hibernia", [SHANNON_ESTUARY, LOUGH_DERG]);

        [Test]
        public void ZonePage_ShouldListMobsByName_LowestLevelFirst_ThenOtherNpcs_ThenBindstones()
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

                ## Bindstones

                - X:295943 Y:642344 Z:4848

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void Index_ShouldListZonesLowestMobsFirst_WithTheirMerchants()
        {
            Assert.That(Pages()["index.md"], Is.EqualTo(
                """
                # Hibernia

                The realm's zones, those with the lowest Mobs first. Each zone's page lists its Mobs, other NPCs and Bindstones, and each merchant's page what it sells.

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
        public void ZonePage_ShouldSaySo_WhenTheZoneHasNoBindstone()
        {
            Assert.That(Pages()["zones/shannon-estuary.md"], Does.EndWith("## Bindstones\n\nNone in this zone.\n"));
        }

        [Test]
        public void ZonePage_ShouldMergeBindAreasThatOverlap_AndListBindstonesWestToEast()
        {
            WikiZone zone = new(202, 200, "Lough Derg", [new WikiNpc("rat", "", 1, true, 7, 8, 9)],
            [
                new WikiBindArea(339685, 467146, 5211, 750),
                new WikiBindArea(313218, 469162, 5200, 750),
                new WikiBindArea(339702, 467021, 5200, 750),
            ]);

            string page = Wiki.Pages("Hibernia", [zone])["zones/lough-derg.md"];

            Assert.That(page, Does.EndWith("- X:313218 Y:469162 Z:5200\n- X:339693 Y:467083 Z:5205\n"));
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

        private static readonly WikiClass MAGICIAN = new("Magician", "Magician",
        [
            new WikiSpecialization("Void", 1, [new WikiSpellLine("Way of the Eclipse", true, [])], []),
        ]);

        private static readonly WikiClass ELDRITCH = new("Eldritch", "Magician",
        [
            new WikiSpecialization("Void", 1,
            [
                new WikiSpellLine("Way of the Eclipse", true,
                [
                    new WikiSpell("Major Ethereal Concussion", 5, new SpellDetails(5, null, 6, "Enemy", 1500, 2.5f, "Cold", null, "Does 22 cold damage.")),
                ]),
                new WikiSpellLine("Void Mastery", false,
                [
                    new WikiSpell("Frost Wave", 2, new SpellDetails(2, 9f, -10, "Enemy", 1500, 3f, "Cold", 350, "Slows | chills.")),
                    new WikiSpell("Chilling Shield", 3, new SpellDetails(3, null, null, "Self", null, 0f, null, null, "Adds 5 armor.", 4)),
                    new WikiSpell("Odd Spell", 4, null),
                ]),
            ], []),
        ]);

        private static readonly WikiClass BLADEMASTER = new("Blademaster", "Guardian",
        [
            new WikiSpecialization("Blades", 1, [],
            [
                new WikiStyle("Return Blade", 2, new StyleDetails(Style.eOpening.Defensive, Style.eAttackResultRequirement.Parry, null, null, ["Slicing Blade"], 5, 10, 0, 0.5f, false, null), []),
                new WikiStyle("Slicing Blade", 8, new StyleDetails(Style.eOpening.Offensive, Style.eAttackResultRequirement.Style, null, "Return Blade", [], 10, 15, 5, 1.2f, false, null), ["Bleed: Does 5 damage every 5 seconds. (50% chance)"]),
            ]),
            new WikiSpecialization("Celtic Dual", 5, [],
            [
                new WikiStyle("Shadow's Edge", 4, new StyleDetails(Style.eOpening.Positional, Style.eAttackResultRequirement.Any, Style.eOpeningPosition.Back, null, [], 10, 0, 0, 1f, true, "Left hand"), []),
            ]),
        ]);

        private static IReadOnlyDictionary<string, string> ClassPages() => Wiki.Pages("Hibernia", [LOUGH_DERG], [ELDRITCH, MAGICIAN, BLADEMASTER]);

        [Test]
        public void ClassPage_ShouldListEachSpellLineOfASpecialization_BaselineFirst_WithWhatTheTooltipShows()
        {
            Assert.That(ClassPages()["classes/eldritch.md"], Is.EqualTo(
                """
                # Eldritch

                A class of Hibernia that starts as a [Magician](magician.md). Each Specialization lists the Spells and Styles it brings. A baseline Spell line gives its Spells as the Character levels, at the Character levels shown; any other gives them as the Specialization rises, at the Specialization levels shown.

                ## Void

                ### Way of the Eclipse (baseline)

                | Level | Spell | Target | Cost | Casting time | Range | Radius | Duration | Magic type | What it does |
                |---|---|---|---|---|---|---|---|---|---|
                | 5 | Major Ethereal Concussion | Enemy | 6 power | 2.5 s | 1500 |  |  | Cold | Does 22 cold damage. |

                ### Void Mastery

                | Level | Spell | Target | Cost | Casting time | Range | Radius | Duration | Magic type | What it does |
                |---|---|---|---|---|---|---|---|---|---|
                | 2 | Frost Wave | Enemy | 10% of max power | 3 s | 1500 | 350 | 9 s | Cold | Slows \| chills. |
                | 3 | Chilling Shield | Self | 4 concentration | instant |  |  |  |  | Adds 5 armor. |
                | 4 | Odd Spell |  |  |  |  |  |  |  |  |

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void ClassPage_ShouldListStylesBySpecializationLevel_WithTheirOpeningsAndWhatTheyAdd()
        {
            Assert.That(ClassPages()["classes/blademaster.md"], Is.EqualTo(
                """
                # Blademaster

                A class of Hibernia that starts as a Guardian. Each Specialization lists the Spells and Styles it brings. A baseline Spell line gives its Spells as the Character levels, at the Character levels shown; any other gives them as the Specialization rises, at the Specialization levels shown.

                ## Blades

                ### Styles

                | Level | Style | Opening | Weapon | Endurance | To hit | Defence | Growth | Opens | Adds |
                |---|---|---|---|---|---|---|---|---|---|
                | 2 | Return Blade | after you parry |  | 5 | 10 | 0 | 0.5 | Slicing Blade |  |
                | 8 | Slicing Blade | after Return Blade |  | 10 | 15 | 5 | 1.2 |  | Bleed: Does 5 damage every 5 seconds. (50% chance) |

                ## Celtic Dual

                From level 5.

                ### Styles

                | Level | Style | Opening | Weapon | Endurance | To hit | Defence | Growth | Opens | Adds |
                |---|---|---|---|---|---|---|---|---|---|
                | 4 | Shadow's Edge | from the target's back, while hidden | Left hand | 10 | 0 | 0 | 1 |  |  |

                """.ReplaceLineEndings("\n")));
        }

        [Test]
        public void ClassPage_ShouldSaySo_ForAClassCharactersStartAs()
        {
            Assert.That(ClassPages()["classes/magician.md"], Does.StartWith("# Magician\n\nA class of Hibernia that Characters start as. "));
        }

        [Test]
        public void Index_ShouldListClassesAfterZones()
        {
            Assert.That(ClassPages()["index.md"], Does.EndWith(
                """
                ## Classes

                Each class's page lists the Spells and Styles its Specializations bring.

                | Class | Starts as |
                |---|---|
                | [Blademaster](classes/blademaster.md) | Guardian |
                | [Eldritch](classes/eldritch.md) | [Magician](classes/magician.md) |
                | [Magician](classes/magician.md) |  |

                """.ReplaceLineEndings("\n")));
        }
    }
}
