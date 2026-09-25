using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Joja;

/// <summary>What Morris says when the player walks up to his counter.</summary>
public enum JojaMorrisLine { Ask, RefuseAgain, PositionFilled }

/// <summary>What the cashier says when the player tries to shop.</summary>
public enum JojaCashierLine { Undecided, Refused }

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
