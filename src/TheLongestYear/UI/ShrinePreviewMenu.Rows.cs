using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    internal sealed partial class ShrinePreviewMenu
    {
        // ------------------------------------------------------------------ rows per tab

        private static Row Header(string text) => new() { Kind = RowKind.Header, Text = text };
        private static Row Note(string text) => new() { Kind = RowKind.Note, Text = text };

        private void BuildRows()
        {
            _rows.Clear();
            switch (_tab)
            {
                case ShrineTab.Active: BuildActiveRows(); break;
                case ShrineTab.Boosts: BuildBoostRows(); break;
                case ShrineTab.Donate: BuildDonateGoals(); break;   // no rows: slots + inventory
                default: BuildPlanRows(); break;
            }
        }

        /// <summary>The Active tab's theme line. <paramref name="theme"/> is set only on a double
        /// week, where each line names its theme; otherwise the single-week text is unchanged.</summary>
        private static string ThemeNote(string bonus, string liability, bool lifted, Theme? theme)
        {
            string bonusName = ThemeModifiers.DisplayNameFor(bonus);
            string liabilityName = ThemeModifiers.DisplayNameFor(liability)
                + (lifted ? " " + Strings.Get("shrine.active.lifted") : "");
            if (theme is not Theme named)
                return Strings.Get("shrine.active.theme", new Dictionary<string, string>
                {
                    ["bonus"] = bonusName,
                    ["liability"] = liabilityName,
                });
            return Strings.Get("shrine.active.theme-named", new Dictionary<string, string>
            {
                ["theme"] = ThemeDisplay.Name(named),
                ["bonus"] = bonusName,
                ["liability"] = liabilityName,
            });
        }

        private void BuildActiveRows()
        {
            _rows.Add(Header(Strings.Get("shrine.active.running")));
            int today = Today;
            List<ActiveBoost> running = _run == null
                ? new List<ActiveBoost>()
                : BoostPurchase.ActiveEntries(_run, today).ToList();
            if (running.Count == 0)
                _rows.Add(Note(Strings.Get("shrine.active.none")));
            foreach (ActiveBoost b in running)
            {
                if (!Enum.TryParse(b.Id, out BoostId id)) continue;
                BoostDefinition def = BoostCatalog.Get(id);
                string name = Strings.Get(def.NameKey);
                if (id == BoostId.CrashCourse && b.Skill >= 0)
                    name += " (" + SkillName(b.Skill) + ")";
                _rows.Add(new Row
                {
                    Kind = RowKind.Running, Text = name, Note = ExpiryLabel(b, def, today),
                    Tooltip = Strings.Get(def.DescKey),
                });
            }

            _rows.Add(Header(Strings.Get("shrine.active.this-week")));
            string bonus = ActiveEffectsProvider.BonusId;
            string liability = ActiveEffectsProvider.LiabilityId;
            if (bonus == null)
                _rows.Add(Note(Strings.Get("shrine.active.no-theme")));
            else if (ActiveEffectsProvider.SecondBonusId == null)
                _rows.Add(Note(ThemeNote(bonus, liability, ActiveEffectsProvider.LiabilitySuppressed, null)));
            else
            {
                // Double theme week: one note per theme, each with its own "(lifted)".
                _rows.Add(Note(ThemeNote(bonus, liability, ActiveEffectsProvider.LiabilitySuppressed, _run?.CurrentSelection)));
                _rows.Add(Note(ThemeNote(ActiveEffectsProvider.SecondBonusId, ActiveEffectsProvider.SecondLiabilityId,
                    ActiveEffectsProvider.SecondLiabilitySuppressed, _run?.SecondSelection)));
            }
            // Wildcard day: today's twist, on its own line (it is not part of either theme).
            if (DayEffects.Today is string twist)
                _rows.Add(Note(Strings.Get("shrine.active.wildcard",
                    new Dictionary<string, string> { ["twist"] = WildcardText.Name(twist) })));

            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> owned =
                    KeepShopFilter.OwnedLeavesInCategory(cat, _state, RunReachEvaluator.Meets);
                if (owned.Count == 0) continue;
                _rows.Add(Header(ThemeDisplay.CategoryName(cat)));
                foreach (UpgradeDefinition def in owned)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Upgrade, Def = def, IsOwned = true,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-owned",
                            new Dictionary<string, string> { ["description"] = def.Description }),
                    });
            }
        }

        private string ExpiryLabel(ActiveBoost b, BoostDefinition def, int today)
        {
            if (def.Duration == BoostDuration.Loop)
                return Strings.Get("shrine.active.this-loop");
            if (b.ExpiresAfterDay == today)
                return Strings.Get("shrine.active.tonight");
            if (def.Duration == BoostDuration.Instant)
                return Strings.Get("shrine.active.tomorrow");
            return Strings.Get("shrine.active.through", new Dictionary<string, string>
            {
                ["season"] = Utility.getSeasonNameFromNumber((int)Calendar.SeasonOfDay(b.ExpiresAfterDay)),
                ["day"] = ((b.ExpiresAfterDay - 1) % Calendar.DaysPerMonth + 1).ToString(),
            });
        }

        /// <summary>Vanilla skill index order (Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4).</summary>
        private static string SkillName(int skill) => skill switch
        {
            0 => Strings.Get("skill.farming"),
            1 => Strings.Get("skill.fishing"),
            2 => Strings.Get("skill.foraging"),
            3 => Strings.Get("skill.mining"),
            4 => Strings.Get("skill.combat"),
            _ => "",
        };

        private static string GroupHeader(BoostDuration d) => d switch
        {
            BoostDuration.Instant => Strings.Get("shrine.boosts.group.instant"),
            BoostDuration.Week => Strings.Get("shrine.boosts.group.week"),
            BoostDuration.Season => Strings.Get("shrine.boosts.group.season"),
            _ => Strings.Get("shrine.boosts.group.loop"),
        };

        private void BuildBoostRows()
        {
            if (!BoostsWired)
            {
                _rows.Add(Note(Strings.Get("shrine.active.none")));
                return;
            }
            if (!Context.IsMainPlayer)
            {
                _rows.Add(Note(Strings.Get("shrine.boosts.host-only")));
                return;
            }
            foreach (BoostDuration d in new[] { BoostDuration.Instant, BoostDuration.Week, BoostDuration.Season, BoostDuration.Loop })
            {
                _rows.Add(Header(GroupHeader(d)));
                foreach (BoostDefinition boost in BoostCatalog.All.Where(b => b.Duration == d))
                {
                    _rows.Add(new Row { Kind = RowKind.Boost, Boost = boost, Tooltip = Strings.Get(boost.DescKey) });
                    if (boost.Id == BoostId.CrashCourse)
                    {
                        // A skill at 9 is never offered (10 must be earned): no row rather than "Not now".
                        IReadOnlyList<int> levels = BoostContextBuilder.Build(_run).SkillLevels;
                        for (int skill = 0; skill < SkillCount; skill++)
                        {
                            if (levels[skill] + 1 >= BoostPricing.MaxSkillLevel) continue;
                            _rows.Add(new Row { Kind = RowKind.Boost, Boost = boost, Skill = skill, Tooltip = Strings.Get(boost.DescKey) });
                        }
                    }
                }
            }
        }

        private void BuildPlanRows()
        {
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> buyable =
                    KeepShopFilter.BuyableInCategory(cat, _state, RunReachEvaluator.Meets);
                List<UpgradeDefinition> locked = UpgradeCatalog.ByCategory(cat)
                    .Where(d => !_state.HasUpgrade(d.Id)
                                && (d.PrerequisiteId == null || _state.HasUpgrade(d.PrerequisiteId))
                                && _state.MeetsMetaRequirement(d.MetaRequirement)
                                && d.RunReachRequirement != null
                                && !RunReachEvaluator.Meets(d.RunReachRequirement))
                    .ToList();
                // Room-blocked animal keeps (AnimalCapacityRule) sit with the locked rows, the
                // reason in place of the reach text.
                locked.AddRange(KeepShopFilter.RoomBlockedInCategory(cat, _state, RunReachEvaluator.Meets));
                if (buyable.Count == 0 && locked.Count == 0)
                    continue;

                _rows.Add(Header(ThemeDisplay.CategoryName(cat)));
                foreach (UpgradeDefinition def in buyable)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Upgrade, Def = def,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-buyable", new Dictionary<string, string>
                        {
                            ["description"] = def.Description,
                            ["owned"] = OwnedLabel(def),
                        }),
                    });
                if (locked.Count == 0) continue;
                _rows.Add(new Row
                {
                    Kind = RowKind.LockedToggle, Category = cat,
                    Text = Strings.Get("shrine.plan.locked", new Dictionary<string, string> { ["count"] = locked.Count.ToString() }),
                });
                if (!_expandedLocked.Contains(cat)) continue;
                foreach (UpgradeDefinition def in locked)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Locked, Def = def,
                        Requirement = def.RunReachRequirement != null && !RunReachEvaluator.Meets(def.RunReachRequirement)
                            ? ReachText.Describe(def.RunReachRequirement)
                            : AnimalCapacityRule.BlockReason(_state, def.Id) ?? "",
                        Tooltip = def.Description,
                    });
            }
            if (_rows.Count == 0)
                _rows.Add(Note(Strings.Get("menu.shrine-preview.nothing-new")));
        }

        private static string OwnedLabel(UpgradeDefinition def)
        {
            if (def.PrerequisiteId == null)
                return Strings.Get("menu.shrine-preview.owned-none");
            return UpgradeCatalog.TryGet(def.PrerequisiteId)?.DisplayName ?? def.PrerequisiteId;
        }

        // ------------------------------------------------------------------ boost row state

        private enum BoostRowState { Buy, Active, NotAvailable }

        private BoostContext ContextFor(Row row) => BoostContextBuilder.Build(_run, row.Skill);

        /// <summary>Rendered straight from <see cref="BoostPurchase.StateOf"/>, the same check
        /// TryBuy runs, so the control a player sees can never disagree with what a click does.
        /// NotEnoughJp still draws a Buy button: the shrine reports the shortfall on click.</summary>
        private BoostRowState StateOf(Row row)
            => BoostPurchase.StateOf(_state, _run, row.Boost.Id, ContextFor(row)) switch
            {
                BoostPurchase.Result.NotAvailable => BoostRowState.NotAvailable,
                BoostPurchase.Result.AlreadyActive => BoostRowState.Active,
                _ => BoostRowState.Buy,
            };

        /// <summary>The Crash Course parent row is a label; its five sub-rows carry the buttons.</summary>
        private static bool HasButton(Row row) => row.Kind == RowKind.Boost
            && (row.Boost.Id != BoostId.CrashCourse || row.Skill >= 0);

        private Rectangle BoostButtonBounds(int rowY)
            => new(_listX + _listWidth - 64 - BoostButtonWidth, rowY + 4, BoostButtonWidth, BoostButtonHeight);
    }
}
