using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Joja;

/// <summary>What Morris says when the player walks up to his counter.</summary>
public enum JojaMorrisLine { Ask, RefuseAgain, PositionFilled }

/// <summary>What the cashier says when the player tries to shop.</summary>
public enum JojaCashierLine { Undecided, Refused }

/// <summary>Which of Morris's letters a morning brings.</summary>
public enum JojaLetterKind { None, Come, Decide }

/// <summary>This morning's Morris letter: its kind, its number within that kind (come 1..8,
/// decide 1..4), and whether sending it ends the offer (the fourth decision letter).</summary>
public readonly record struct JojaLetter(JojaLetterKind Kind, int Number, bool Rejects)
{
    public static readonly JojaLetter None = new(JojaLetterKind.None, 0, false);
}

/// <summary>Morris's offer (spec 2026-09-25-joja-offer-design). Pure rules over RunState (per loop)
/// and MetaState (per save). Saying Yes is not recorded anywhere: the game side plays the bad
/// ending and returns to the title without saving.</summary>
public static class JojaOffer
{
    public const int ComeLetters = 8;
    public const int DecisionLetters = 4;
    public const int DaysPerWeek = 7;
    private const int LettersPerSeason = 2;
    private const int FirstLetterDay = 2, LastLetterDay = 27;

    public static bool IsRejected(MetaState meta) => meta.JojaRejectedLoop > 0;

    public static bool ShouldPlayScene(RunState run, MetaState meta, bool busy)
        => !busy && !IsRejected(meta) && run.JojaSceneSeenDay < 0;

    public static bool SceneSkippable(MetaState meta) => meta.JojaOfferEverSeen;

    public static void MarkSceneSeen(RunState run, MetaState meta, int dayOfYear)
    {
        run.JojaSceneSeenDay = dayOfYear;
        meta.JojaOfferEverSeen = true;
    }

    /// <summary>Two distinct days in each season, days 2..27, sorted, from the loop's seed so a
    /// reload plans the same days.</summary>
    public static List<int> PlanLetterDays(int seed)
    {
        var rng = new Random(seed);
        var days = new List<int>();
        for (int season = 0; season < 4; season++)
        {
            var picks = new HashSet<int>();
            while (picks.Count < LettersPerSeason)
                picks.Add(rng.Next(FirstLetterDay, LastLetterDay + 1));
            days.AddRange(picks.OrderBy(d => d).Select(d => Calendar.DayOfYear(season, d)));
        }
        return days;
    }

    /// <summary>The next "come see me" letter (1..8) due this morning, or 0. A day that passed
    /// unseen (a load past it) still delivers the next letter the next morning.</summary>
    public static int ComeLetterDue(RunState run, MetaState meta, int dayOfYear)
    {
        if (IsRejected(meta) || run.JojaSceneSeenDay >= 0) return 0;
        int next = run.JojaLettersSent;
        if (next >= ComeLetters || next >= run.JojaLetterDays.Count) return 0;
        return dayOfYear >= run.JojaLetterDays[next] ? next + 1 : 0;
    }

    /// <summary>The next "make a decision" letter (1..4) due this morning, or 0: one a week after
    /// the scene, while the player has not answered.</summary>
    public static int DecisionLetterDue(RunState run, MetaState meta, int dayOfYear)
    {
        if (IsRejected(meta) || run.JojaSceneSeenDay < 0) return 0;
        int next = run.JojaDecisionLettersSent;
        if (next >= DecisionLetters) return 0;
        int due = run.JojaSceneSeenDay + (next + 1) * DaysPerWeek;
        return dayOfYear >= due ? next + 1 : 0;
    }

    /// <summary>The letter-day seed for a loop, derived from the loop's own seed.</summary>
    public static int LetterSeed(int runSeed) => unchecked(runSeed * 31 + 0x4A6F6A61);

    /// <summary>The one place that decides this morning's Morris letter, at most one a morning.
    /// Nothing while a rewind is pending: that morning still carries the OLD loop's state, and a
    /// fourth decision letter there would reject the player at the loop boundary (the spec says a
    /// rewind before the fourth starts over). A slot that falls on such a morning arrives the next
    /// one, through the <c>&gt;=</c> in <see cref="ComeLetterDue"/> / <see cref="DecisionLetterDue"/>.
    ///
    /// The loop's letter days are planned here the first time they are needed, keeping only the
    /// slots from today on. A fresh loop plans on Spring 1 or 2 and so keeps all eight; a run
    /// already mid-year when the letters first ship (an old save) starts at letter 1 on the next
    /// slot at the normal cadence instead of catching up on every slot it missed. The planned
    /// list is stored, so its index IS the letter number minus one.</summary>
    public static JojaLetter MorningLetter(RunState run, MetaState meta, int today, bool rewindPending)
    {
        if (rewindPending) return JojaLetter.None;
        if (run.JojaLetterDays == null || run.JojaLetterDays.Count == 0)
            run.JojaLetterDays = PlanLetterDays(LetterSeed(run.Seed)).Where(d => d >= today).ToList();

        int come = ComeLetterDue(run, meta, today);
        if (come > 0) return new JojaLetter(JojaLetterKind.Come, come, Rejects: false);
        int decide = DecisionLetterDue(run, meta, today);
        if (decide > 0) return new JojaLetter(JojaLetterKind.Decide, decide, Rejects: decide == DecisionLetters);
        return JojaLetter.None;
    }

    /// <summary>Record a delivered letter: advance its counter, and on the fourth decision letter
    /// take the silence as a rejection in this loop.</summary>
    public static void Record(RunState run, MetaState meta, JojaLetter letter)
    {
        switch (letter.Kind)
        {
            case JojaLetterKind.Come:
                run.JojaLettersSent = letter.Number;
                break;
            case JojaLetterKind.Decide:
                run.JojaDecisionLettersSent = letter.Number;
                if (letter.Rejects) Reject(meta, run.RunNumber);
                break;
        }
    }

    /// <summary>The player and Morris part ways. Keeps the FIRST loop it happened in.</summary>
    public static void Reject(MetaState meta, int runNumber)
    {
        if (!IsRejected(meta)) meta.JojaRejectedLoop = Math.Max(1, runNumber);
    }

    public static JojaMorrisLine MorrisLine(RunState run, MetaState meta, int runNumber)
    {
        if (!IsRejected(meta)) return JojaMorrisLine.Ask;
        return runNumber == meta.JojaRejectedLoop ? JojaMorrisLine.RefuseAgain : JojaMorrisLine.PositionFilled;
    }

    public static JojaCashierLine CashierLine(MetaState meta)
        => IsRejected(meta) ? JojaCashierLine.Refused : JojaCashierLine.Undecided;
}
