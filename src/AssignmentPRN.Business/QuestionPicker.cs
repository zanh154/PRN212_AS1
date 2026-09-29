namespace AssignmentPRN.Business;

/// <summary>
/// The shuffling half of handing out questions, kept free of any database call so
/// the "no repeats inside one exam" rule can be tested on its own.
/// </summary>
public static class QuestionPicker
{
    /// <summary>
    /// Takes <paramref name="count"/> questions from <paramref name="pool"/> at random.
    /// Every identifier is returned at most once, so calling this for each student of
    /// the same exam — passing the running total of <paramref name="taken"/> each time —
    /// hands out a different paper to everybody.
    /// </summary>
    public static IReadOnlyList<int> Pick(
        IReadOnlyList<int> pool,
        int count,
        IReadOnlyCollection<int>? taken = null,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (count <= 0)
        {
            return Array.Empty<int>();
        }

        var used = taken is null ? new HashSet<int>() : new HashSet<int>(taken);
        var source = random ?? Random.Shared;

        // Copy before shuffling: the caller may hand us a cached list.
        var candidates = pool.Where(id => !used.Contains(id)).ToList();
        Shuffle(candidates, source);

        return candidates.Take(count).ToList();
    }

    /// <summary>How many questions are still free for an exam that already handed out <paramref name="taken"/>.</summary>
    public static int CountAvailable(IReadOnlyList<int> pool, IReadOnlyCollection<int> taken)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(taken);

        var used = new HashSet<int>(taken);
        return pool.Count(id => !used.Contains(id));
    }

    private static void Shuffle(List<int> values, Random source)
    {
        // Fisher-Yates: unbiased, and unlike OrderBy(_ => random.Next()) it cannot
        // return a different order for the same input.
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = source.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
