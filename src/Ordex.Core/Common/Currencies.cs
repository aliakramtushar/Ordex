namespace Ordex.Core.Common;

/// <summary>A currency the business can buy or sell in.</summary>
public sealed record CurrencyInfo(string Code, string Symbol, string Name, int Decimals, bool LakhGrouping);

/// <summary>
/// Supported currencies. A company buys abroad in its purchase currency and sells
/// to customers in its sales currency (e.g. buys in SGD, sells in BDT).
/// Add a line here to support another currency.
/// </summary>
public static class Currencies
{
    public static readonly CurrencyInfo Bdt = new("BDT", "৳", "Bangladeshi Taka", 0, true);
    public static readonly CurrencyInfo Usd = new("USD", "$", "US Dollar", 2, false);
    public static readonly CurrencyInfo Sgd = new("SGD", "S$", "Singapore Dollar", 2, false);

    public static IReadOnlyList<CurrencyInfo> All { get; } = [Bdt, Usd, Sgd];

    public const string DefaultPurchase = "SGD";
    public const string DefaultSales = "BDT";

    public static bool IsSupported(string? code) => All.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public static CurrencyInfo Find(string? code, CurrencyInfo fallback) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) ?? fallback;
}

/// <summary>
/// Currencies of the data being viewed. <see cref="IsMixed"/> = several companies with
/// different currencies are shown together (SuperAdmin, "All companies").
/// </summary>
public sealed record CurrencySettings(CurrencyInfo Purchase, CurrencyInfo Sales, bool IsMixed)
{
    public static CurrencySettings Default { get; } =
        new(Currencies.Find(Currencies.DefaultPurchase, Currencies.Sgd), Currencies.Find(Currencies.DefaultSales, Currencies.Bdt), false);
}
