using System.Globalization;

namespace DashSpec.Core.Runtime;

public static class MeasureValues
{
    public static bool TryReadDouble(object? value, out double number)
    {
        number = 0;
        return value switch
        {
            null or DBNull => false,
            double d => Accept(d, out number),
            float f => Accept(f, out number),
            decimal m => Accept((double)m, out number),
            byte or sbyte or short or ushort or int or uint or long or ulong =>
                Accept(Convert.ToDouble(value, CultureInfo.InvariantCulture), out number),
            string s when double.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => Accept(parsed, out number),
            IConvertible c => Accept(Convert.ToDouble(c, CultureInfo.InvariantCulture), out number),
            _ => false,
        };

        static bool Accept(double d, out double n)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                n = 0;
                return false;
            }

            n = d;
            return true;
        }
    }
}
