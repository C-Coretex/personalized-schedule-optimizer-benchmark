namespace Specialized.Optimizer.Helpers;

internal static class RandomHelpers
{
    public static T RandomElement<T>(this T[] source, Random? random = null)
    {
        if (source == null || source.Length == 0)
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        random ??= Random.Shared;
        return source.ElementAt(random.Next(0, source.Length));
    }

    public static T RandomElement<T>(this ICollection<T> source, Random? random = null)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        random ??= Random.Shared;
        return source.ElementAt(random.Next(0, source.Count));
    }

    /// <summary>
    /// Does not insure if elements are different (for performance).
    /// </summary>
    public static (T First, T Second) TwoRandomElements<T>(this ICollection<T> source, Random? random = null)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        random ??= Random.Shared;

        return (source.ElementAt(random.Next(0, source.Count)), source.ElementAt(random.Next(0, source.Count)));
    }

    /// <summary>
    /// Does not insure if elements are different (for performance).
    /// </summary>
    public static (T First, T Second) TwoRandomElements<T>(this T[] source, Random? random = null)
    {
        if (source == null || source.Length == 0)
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        random ??= Random.Shared;

        return (source.ElementAt(random.Next(0, source.Length)), source.ElementAt(random.Next(0, source.Length)));
    }

    public static bool RandomBool(this Random random)
        => random.Next() > int.MaxValue / 2; //a fast and simple way to return bool

    public static bool RandomByPercent(this Random random, int percent)
        => random.Next(100) < percent;

    public static T[] ShuffleElements<T>(this T[] source, Random? random = null)
    {
        if (source == null)
            throw new ArgumentException("Source cannot be null.", nameof(source));

        random ??= Random.Shared;

        T[] copy = new T[source.Length];
        Array.Copy(source, copy, source.Length);
        int n = copy.Length;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            T value = copy[k];
            copy[k] = copy[n];
            copy[n] = value;
        }

        return copy;
    }
}
