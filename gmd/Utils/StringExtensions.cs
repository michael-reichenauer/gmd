


namespace System
{
    public static class StringExtensions
    {
        /// <summary>
        /// Method that limits the length of text to a defined length.
        /// </summary>
        /// <param name="source">The source text.</param>
        /// <param name="maxLength">The maximum limit of the string to return.</param>
        public static string Max(this string source, int maxLength)
        {
            if (source.Length <= maxLength)
            {
                return source;
            }

            return source.Substring(0, maxLength);
        }

        public static string TrimPrefix(this string source, string prefix)
        {
            if (!source.StartsWith(prefix))
            {
                return source;
            }

            return source.Substring(prefix.Length);
        }

        public static string TrimSuffix(this string source, string suffix)
        {
            if (!source.EndsWith(suffix))
            {
                return source;
            }

            return source.Substring(0, source.Length - suffix.Length);
        }
    }
}
