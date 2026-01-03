using System;
using System.Text;
using System.Linq;

public static class NumberToText
{
    private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
    private static readonly string[] Teens = { "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
    private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    // Scale goes up to Decillion (10^33)
    private static readonly string[] Scales = {
        "", "Thousand", "Million", "Billion", "Trillion", "Quadrillion",
        "Quintillion", "Sextillion", "Septillion", "Octillion", "Nonillion", "Decillion"
    };

    public static string Convert(double number) => Convert((decimal)number);
    public static string Convert(long number) => Convert((decimal)number);

    public static string Convert(decimal number)
    {
        if (number == 0) return "Zero";

        // Handle negative numbers
        string prefix = "";
        if (number < 0)
        {
            prefix = "Minus ";
            number = Math.Abs(number);
        }

        // Split whole number and decimals using string to maintain absolute precision
        string[] parts = number.ToString(System.Globalization.CultureInfo.InvariantCulture).Split('.');

        // 1. Process the Whole Number
        long wholePart = (long)Math.Truncate(number);
        string wholeText = ConvertWholeNumber(wholePart);

        // 2. Process the Decimals (digit by digit)
        string decimalText = "";
        if (parts.Length > 1)
        {
            StringBuilder sb = new StringBuilder(" Point");
            foreach (char digit in parts[1])
            {
                int d = (int)char.GetNumericValue(digit);
                sb.Append(" " + (d == 0 ? "Zero" : Ones[d]));
            }
            decimalText = sb.ToString();
        }

        return (prefix + wholeText + decimalText).Trim();
    }

    private static string ConvertWholeNumber(long number)
    {
        if (number == 0) return "";

        string result = "";
        int scaleIndex = 0;

        while (number > 0)
        {
            int threeDigitGroup = (int)(number % 1000);
            if (threeDigitGroup != 0)
            {
                string groupText = ConvertGroup(threeDigitGroup);
                result = groupText + " " + Scales[scaleIndex] + " " + result;
            }
            number /= 1000;
            scaleIndex++;
        }

        return result.Trim();
    }

    private static string ConvertGroup(int number)
    {
        string groupWords = "";

        if (number >= 100)
        {
            groupWords += Ones[number / 100] + " Hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (groupWords != "") groupWords += "and ";

            if (number < 10) groupWords += Ones[number];
            else if (number < 20) groupWords += Teens[number - 10];
            else
            {
                groupWords += Tens[number / 10];
                if ((number % 10) > 0) groupWords += "-" + Ones[number % 10];
            }
        }

        return groupWords.Trim();
    }
}