namespace Dialekt;

using System.Text;

static class StringExtensions
{
    public static string ControlCharacterPictures(this string input)
    {
        return string.Create(input.Length * 4, input, (span, value) =>
        {
            int pos = 0;
            foreach (var c in value)
            {
                if (c <= 32)
                {
                    span[pos++] = (char)(c + 0x2400);
                }
                else
                {
                    span[pos++] = c;
                }
            }
        });
    }

    public static string StripSuffix(this string input, string suffix)
    {
        if (input.EndsWith(suffix))
        {
            return input[..^suffix.Length];
        }
        return input;
    }
}
