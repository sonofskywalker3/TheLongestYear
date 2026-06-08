using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Watches <see cref="JunimoNoteMenu"/> sessions and awards JP each time an ingredient slot
    /// flips from incomplete to complete. Replaces the Harmony patch on <c>Bundle.tryToDepositThisItem</c>
    /// which the 2026-05-26 playtest showed did not fire on real CC deposits — likely a Harmony
    /// resolution issue against the live Stardew binary, but observation-based detection sidesteps
    /// it entirely.
    ///
    /// Design notes:
    ///   - We snapshot every bundle's per-slot <c>completed</c> flag when the menu opens, then
    ///     scan once per tick while it's open. A diff (false → true) means the player just
    ///     deposited; we look the ingredient up in the bundle's <c>ingredients</c> list and award
    ///     JP for that item id × that slot's required stack.
    ///   - <see cref="JunimoNoteMenu.GetRepresentativeItemId"/> resolves category ingredients (e.g.
    ///     -5 → any egg) to the concrete representative id. Plain id ingredients pass through.
    ///   - Ids may be either qualified ("(O)388") or bare ("388") depending on the bundle source —
    ///     we run them through <see cref="BundleParsing.NormalizeItemId"/> so the downstream catalog
    ///     and ledger lookups see the canonical form.
    /// </summary>
    internal sealed class DonationObserver
    {
        private readonly IMonitor _monitor;

        /// <summary>bundleIndex → per-slot completed snapshot taken when JunimoNoteMenu opened.
        /// Entries are mutated in place as each newly-completed slot is awarded, so a single
        /// deposit fires exactly once.</summary>
        private readonly Dictionary<int, bool[]> _snapshot = new();

        /// <summary>bundleIndex → snapshot of <c>Bundle.complete</c> at menu open. Diff-on-tick
        /// awards the bundle bonus the moment the slots all flip. 2026-05-28 round 4: the
        /// Harmony postfix on <c>JunimoNoteMenu.checkIfBundleIsComplete</c> wasn't firing for
        /// the playtester ("I completed a bundle and didn't get any extra jp for doing so"),
        /// same shape as the per-item patch that motivated the observer in the first place.
        /// Move detection here so both per-slot AND bundle-complete bonuses follow the same
        /// reliable diff path.</summary>
        private readonly Dictionary<int, bool> _bundleCompleteSnapshot = new();

        private bool _menuOpen;

        public DonationObserver(IModHelper helper, IMonitor monitor)
        {
            _monitor = monitor;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is JunimoNoteMenu newJnm)
            {
                CaptureSnapshot(newJnm);
                _menuOpen = true;
            }
            else if (e.OldMenu is JunimoNoteMenu oldJnm)
            {
                // Run one final diff so a deposit on the same tick the menu closes still pays.
                DiffAndAward(oldJnm);
                _menuOpen = false;
                _snapshot.Clear();
                _bundleCompleteSnapshot.Clear();
            }
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_menuOpen) return;
            if (Game1.activeClickableMenu is not JunimoNoteMenu jnm)
            {
                // Menu was swapped out without MenuChanged catching it (rare — defensive).
                _menuOpen = false;
                _snapshot.Clear();
                _bundleCompleteSnapshot.Clear();
                return;
            }
            DiffAndAward(jnm);
        }

        private void CaptureSnapshot(JunimoNoteMenu jnm)
        {
            _snapshot.Clear();
            _bundleCompleteSnapshot.Clear();
            if (jnm.bundles == null) return;
            foreach (Bundle b in jnm.bundles)
            {
                if (b == null) continue;
                if (b.ingredients != null)
                {
                    int n = b.ingredients.Count;
                    bool[] state = new bool[n];
                    for (int i = 0; i < n; i++)
                        state[i] = b.ingredients[i].completed;
                    _snapshot[b.bundleIndex] = state;
                }
                _bundleCompleteSnapshot[b.bundleIndex] = b.complete;
            }
        }

        private void DiffAndAward(JunimoNoteMenu jnm)
        {
            if (DonationService.Active == null || jnm?.bundles == null) return;

            foreach (Bundle b in jnm.bundles)
            {
                if (b?.ingredients == null) continue;
                if (!_snapshot.TryGetValue(b.bundleIndex, out bool[] prev))
                {
                    // Bundle appeared mid-session (e.g. JunimoNoteMenu rebuilt its list);
                    // capture now so the NEXT tick can detect changes against it.
                    int newCount = b.ingredients.Count;
                    bool[] state = new bool[newCount];
                    for (int i = 0; i < newCount; i++)
                        state[i] = b.ingredients[i].completed;
                    _snapshot[b.bundleIndex] = state;
                    continue;
                }

                int n = b.ingredients.Count;
                int compareLen = n < prev.Length ? n : prev.Length;
                for (int i = 0; i < compareLen; i++)
                {
                    if (!prev[i] && b.ingredients[i].completed)
                    {
                        BundleIngredientDescription desc = b.ingredients[i];
                        string rawId = JunimoNoteMenu.GetRepresentativeItemId(desc);
                        string qualifiedId = BundleParsing.NormalizeItemId(rawId);
                        DonationService.Active.OnItemDonated(qualifiedId, desc.stack);
                        // Mark as awarded so the next tick doesn't double-fire if the bundle
                        // stays in scope (e.g. player completes another slot on the same bundle).
                        prev[i] = true;
                    }
                }

                // Bundle completion: false → true on Bundle.complete fires OnBundleCompleted.
                // TryMarkBundleAwarded inside DonationService is idempotent, so even if the
                // Harmony postfix on checkIfBundleIsComplete eventually fires too, only the
                // first call pays.
                if (_bundleCompleteSnapshot.TryGetValue(b.bundleIndex, out bool wasComplete))
                {
                    if (!wasComplete && b.complete)
                    {
                        // Vault money bundles (this save's actual indices, remix-aware) pay gold,
                        // not items: record the payment + gold-scaled JP, NOT the standard
                        // bundle-completion bonus. Everything else takes the normal completion path.
                        if (TheLongestYear.Integration.VaultBundleMap.IsVaultIndex(b.bundleIndex))
                            DonationService.Active.OnVaultBundlePaid(b.bundleIndex);
                        else
                            DonationService.Active.OnBundleCompleted(b.bundleIndex);
                        _bundleCompleteSnapshot[b.bundleIndex] = true;
                    }
                }
                else
                {
                    _bundleCompleteSnapshot[b.bundleIndex] = b.complete;
                }
            }
        }
    }
}
