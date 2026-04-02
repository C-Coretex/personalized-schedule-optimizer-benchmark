namespace Specialized.Optimizer.Helpers;

internal static class RandomHelpers
{
    public static T RandomElement<T>(this ICollection<T> source, Random? random = null)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("Source cannot be null or empty.", nameof(source));

        random ??= Random.Shared;
        int index = random.Next(0, source.Count);
        return source.ElementAt(index);
    }

    public static T[] ShuffleElements<T>(this T[] source, Random? random = null)
    {
        if (source == null)
            throw new ArgumentException("Source cannot be null.", nameof(source));

        random ??= Random.Shared;

        int n = source.Length;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            T value = source[k];
            source[k] = source[n];
            source[n] = value;
        }

        return source;
    }
}
