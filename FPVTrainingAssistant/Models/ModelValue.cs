namespace FPVTrainingAssistant.Models;

internal static class ModelValue
{
    public static string NonEmpty(string value, string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, propertyName);
        return value;
    }

    public static double Percent(double value, string propertyName)
    {
        if (!double.IsFinite(value) || value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, "Value must be finite and between 0 and 100.");
        }

        return value;
    }
}
