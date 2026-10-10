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
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Per-season goal tracker (UX2 from the 2026-05-26 playtest). The weekly hub is the SELECTION
    /// surface; bundle progress belongs on its own page so the hub stays focused on "pick a theme."
    /// This menu lists every classified bundle for the current run with:
    ///   - bundle name + theme tag,
    ///   - donated / required count (toward full bundle completion),
    ///   - badge "needs N before {NextSeason} 1" (or empty when this season's checkpoint is met),
    ///   - icons for the still-missing ingredients that gate THIS season's checkpoint.
    /// </summary>
    internal sealed partial class SeasonGoalsMenu : IClickableMenu
    {
        private const int PanelWidth = 1180;
        private const int PanelHeight = 760;
        private const int PanelPadding = 32;
        private const int TitleBarHeight = 80;

        // Normal Stardew inventory icon size = 64px (16px sprite × 4 scale). The row needs to
        // fit a header line + a row of 64px icons + padding above & below.
        // Gap is intentionally wide (24px) — Item.drawInMenu's stack-count badge renders
        // beyond the icon's bottom-right corner; an 8px gap was letting "99" of one icon
        // collide with the next icon's left edge.
        private const int RowHeight = 144;
        private const int RowSpacing = 10;

        private const int IngredientIconSize = 64;
        private const int IngredientIconGap = 24;
        private const int IngredientIconY = 64;        // offset from row top (below the header line)

        // Gold-coin sprite in Game1.mouseCursors (the QuestLog reward coin); drawn on the vault row
        // in place of item sprites, one per still-owed payment.
        private static readonly Rectangle CoinIconSource = new Rectangle(280, 410, 16, 16);

        private const int RowIdBase = 8000;
        private const int ScrollUpId = 8900;
        private const int ScrollDownId = 8901;

        private readonly IMonitor _monitor;
        private readonly RunState _run;
        private readonly MetaState _meta;
        private readonly IReadOnlyList<BundleRequirement> _requirements;
        private readonly CoreSeason _season;

        private List<BundleEntry> _entries = new();
        private int _scrollIndex;
        private int _rowsPerPage;

        private readonly List<ClickableComponent> _rowSlots = new();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;

        private string _hoverText = "";

        // The CC's board as of BuildEntries, and its stacks per bundle index (filled on first use).
        private IReadOnlyDictionary<string, string> _liveBoard;
        private readonly Dictionary<int, IReadOnlyDictionary<string, int>> _liveStacks = new();

        public SeasonGoalsMenu(IMonitor monitor, RunState run, MetaState meta,
            IReadOnlyList<BundleRequirement> requirements)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _run = run;
            _meta = meta;
            _requirements = requirements ?? new List<BundleRequirement>();
            _season = run.Season;

            BuildEntries();
            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            CompleteFireplaceIntroQuest();
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            CompleteFireplaceIntroQuest();
        }

        /// <summary>Mark the fireplace intro quest (tly.-9004) complete in the questLog the
        /// first time the player opens this menu — vanilla's <see cref="StardewValley.Quests.Quest.questComplete"/>
        /// fires the "Quest Complete!" notification and the quest gets removed from the
        /// active list. Also adds the dismissal to <see cref="MetaState.DismissedIndicators"/>
        /// so the intro doesn't re-add on the next save load / reset (matches the cookbook /
        /// craftbook / stash pattern). Idempotent — questComplete bails on already-completed.</summary>
        private void CompleteFireplaceIntroQuest()
        {
            _meta?.DismissedIndicators.Add(TheLongestYear.Core.IntroQuestIds.LegacyFireplaceDismissed);
            if (Game1.player?.questLog == null) return;
            foreach (var q in Game1.player.questLog)
            {
                if (q != null && q.id.Value == TheLongestYear.Core.IntroQuestIds.LegacyFireplaceQuest)
                {
                    q.questComplete();
                    break;
                }
            }
        }

        // ---------- data ----------

        private void BuildEntries()
        {
            SlotLedger donated = _run.DonatedLedger();
            _entries.Clear();
            _liveBoard = Game1.netWorldState?.Value?.BundleData;
            _liveStacks.Clear();

            // Collect first, then sort. Completed bundles (Have >= Need) sink to the bottom
            // so the active obligations stay at the top where the player scans first
            // (2026-05-29 user feedback). Within each completion bucket, order by theme then
            // bundle name so the same physical menu position holds the same bundle
            // session-over-session (less scanning churn).
            var candidates = new List<BundleEntry>();
            foreach (var br in _requirements)
            {
                // Only show bundles with an obligation due BY this season's checkpoint —
                // Seasonal bundles for the current season, PerItem bundles with any pin
                // due ≤ current, Percentage bundles with a non-zero quota this season.
                // Past-season Seasonal and future-only PerItem bundles are hidden; this is
                // the "what do I owe THIS season" tracker, not a year-wide overview.
                if (!IsRelevantForCurrentSeason(br)) continue;

                int have = donated.FilledCount(br.BundleIndex);
                int need = br.NumberOfSlots;

                var (missingCount, missingThisSeason) = br.MissingForSeason(_season, donated);

                candidates.Add(new BundleEntry(br, have, need, missingCount, missingThisSeason));
            }

            _entries.AddRange(candidates
                .OrderBy(e => e.Have >= e.Need ? 1 : 0)
                .ThenBy(e => e.Bundle.Theme)
                .ThenBy(e => e.Bundle.Name, StringComparer.Ordinal));

            // The Vault (bus-repair) goal is a gate term but not an item bundle, so it has no
            // ingredient row to fall out of _requirements. Add it as its own list row (it used to be
            // a thin pinned banner, inconsistent with the bundle rows). An unmet vault sits at the
            // top as an active obligation; once met it sinks to the bottom with completed bundles.
            int vaultCount = TheLongestYear.Integration.VaultBundleMap.Count();
            bool vaultMet = VaultRules.IsVaultGateSatisfied(_season, _run, _meta, vaultCount);
            BundleEntry vaultEntry = BundleEntry.Vault(
                VaultRules.PaidCount(_run), VaultRules.RequiredPaid(_season, vaultCount), vaultMet);
            if (vaultMet)
                _entries.Add(vaultEntry);
            else
                _entries.Insert(0, vaultEntry);
        }

        /// <summary>How many of the item the CC asks for right now. Read from the live board, not the
        /// requirement: requirements are built at save load, and the theme week discount lowers and
        /// restores stacks mid-session (Reddit report 2026-10-07: the Log showed fewer Tulips than the
        /// CC wanted). Falls back to the requirement's stack when the board has no such bundle.</summary>
        private int StackFor(BundleRequirement bundle, string itemId)
        {
            if (!_liveStacks.TryGetValue(bundle.BundleIndex, out IReadOnlyDictionary<string, int> live))
            {
                live = BundleStacks.ForBundle(_liveBoard, bundle.BundleIndex) ?? bundle.IngredientStacks;
                _liveStacks[bundle.BundleIndex] = live;
            }
            return live.TryGetValue(itemId, out int stack) ? stack : 1;
        }

        /// <summary>True if this bundle has any obligation that's due BY the current season's
        /// day-28 checkpoint. Mirrors the kind-specific shape:
        /// Seasonal — its season matches the current one.
        /// PerItem — at least one pin has season ≤ current.
        /// Percentage — cumulative quota for the current season is &gt; 0.</summary>
        private bool IsRelevantForCurrentSeason(BundleRequirement br)
        {
            switch (br.Kind)
            {
                case BundleKind.Seasonal:
                    return br.SeasonalSeason!.Value == _season;
                case BundleKind.PerItem:
                    return br.ItemSeasonPins!.Any(kv => (int)kv.Value <= (int)_season);
                case BundleKind.Percentage:
                    return br.CumulativeRequiredBySeason![(int)_season] > 0;
                default:
                    return false;
            }
        }

        // ---------- layout ----------

        private void RecomputeBoundsAndLayout()
        {
            width = Math.Min(PanelWidth, Game1.uiViewport.Width - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPadding;
            int listY = yPositionOnScreen + TitleBarHeight;
            int listWidth = width - PanelPadding * 2 - 56;     // leave room for scroll arrows
            int listHeight = height - TitleBarHeight - PanelPadding;
            _rowsPerPage = Math.Max(1, listHeight / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                var slot = new ClickableComponent(new Rectangle(listX, rowY, listWidth, RowHeight),
                    "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                    rightNeighborID = ScrollUpId
                };
                _rowSlots.Add(slot);
            }

            int arrowX = listX + listWidth + 8;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            {
                myID = ScrollUpId,
                downNeighborID = ScrollDownId,
                leftNeighborID = RowIdBase
            };
            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, listY + listHeight - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            {
                myID = ScrollDownId,
                upNeighborID = ScrollUpId,
                leftNeighborID = RowIdBase + _rowsPerPage - 1
            };

            this.initializeUpperRightCloseButton();

            // populateClickableComponentList defaults to public-only via reflection — fill manually.
            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.AddRange(_rowSlots);
            allClickableComponents.Add(_scrollUp);
            allClickableComponents.Add(_scrollDown);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _rowSlots.Count > 0 ? _rowSlots[0] : null;
            if (currentlySnappedComponent != null)
                this.snapCursorToCurrentSnappedComponent();
        }

        /// <summary>Vanilla's own localized season name (SDV 1.6 <c>Utility.getSeasonNameFromNumber</c>),
        /// used for the <c>{{season}}</c> token in the <c>menu.goals.title</c> /
        /// <c>menu.goals.badge-needs-before</c> i18n keys so translated builds get the game's
        /// own "Spring"/"Summer"/"Fall"/"Winter" instead of our own (unmaintained) season key.
        /// English output is byte-identical to the prior <c>(CoreSeason)n</c> enum-name rendering.</summary>
        internal static string SeasonName(CoreSeason season)
            => StardewValley.Utility.getSeasonNameFromNumber((int)season);

        private static Item ResolveItem(string id, int stack = 1, int quality = 0)
        {
            try { return ItemRegistry.Create(id, stack, quality, allowNull: true); }
            catch (Exception ex)
            {
                TheLongestYear.Loop.PatchLog.TraceOnce("goals-item:" + id,
                    $"Season Goals: item '{id}' could not be created ({ex.GetType().Name}); its icon is left empty.");
                return null;
            }
        }

        // ---------- types ----------

        private sealed class BundleEntry
        {
            public BundleRequirement Bundle { get; }   // null for the synthetic Vault row
            public bool IsVault { get; }
            public string Title { get; }               // headline name ("Bus Repair" or the bundle name)
            public string ThemeTag { get; }            // parenthetical tag ("Vault" or the bundle theme)
            public int Have { get; }
            public int Need { get; }
            public int MissingThisSeasonCount { get; }
            public IReadOnlyList<string> MissingItems { get; }

            public bool IsMet => MissingThisSeasonCount == 0;

            public BundleEntry(BundleRequirement bundle, int have, int need, int missingThisSeason,
                IReadOnlyList<string> missingItems)
            {
                Bundle = bundle;
                IsVault = false;
                Title = bundle.Name;
                ThemeTag = ThemeDisplay.Name(bundle.Theme);
                Have = have;
                Need = need;
                MissingThisSeasonCount = missingThisSeason;
                MissingItems = missingItems;
            }

            private BundleEntry(int paid, int need, int missing)
            {
                Bundle = null;
                IsVault = true;
                Title = Strings.Get("menu.goals.bus-repair");
                ThemeTag = Strings.Get("menu.goals.vault");
                Have = Math.Min(paid, need);            // cap so a fully-prepaid run reads "N/N", not "4/1"
                Need = need;
                MissingThisSeasonCount = missing;
                MissingItems = Array.Empty<string>();
            }

            /// <summary>The synthetic vault (bus-repair) goal row. <paramref name="paid"/> vault
            /// bundles paid this run vs. <paramref name="need"/> required by this season; when
            /// <paramref name="met"/> (count satisfied OR keep_bus_unlocked) it counts as complete.</summary>
            public static BundleEntry Vault(int paid, int need, bool met)
                => new BundleEntry(paid, need, met ? 0 : Math.Max(0, need - paid));
        }
    }
}
