namespace Infrastructure.Extensions
{
    public static class VariableNameExtractor
    {
        public static string? ExtractVariable(this string text)
        {
            var startIndex = text.IndexOf('@');
            if (startIndex == -1)
            {
                return null;
            }
            var endIndex = text.IndexOf(' ', startIndex);
            if (endIndex == -1)
            {
                endIndex = text.Length;
            }
            return text.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
    }
}
