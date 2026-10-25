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

    public static string ToPascalCase(this string input)
    {
        var sb = new StringBuilder(input.Length);

        for (int i = 0; i < input.Length; i++)
        {
            ;
            if (i == 0)
            {
                sb.Append(char.ToUpperInvariant(input[i]));
            }
            else if (input[i] is ' ' or '-' or '_' && i + 1 < input.Length)
            {
                i++;
                sb.Append(char.ToUpperInvariant(input[i]));
            }
            else
            {
                sb.Append(char.ToLowerInvariant(input[i]));
            }
        }
        return sb.ToString();
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
