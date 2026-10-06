using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;

namespace Ordex.Web.Infrastructure;

/// <summary>
/// Formats money in the currencies of the company being viewed
/// (e.g. buys in SGD / sells in BDT, or buys in USD / sells in USD).
/// Views use <c>Money.Sale(x)</c> and <c>Money.Buy(x)</c>; loaded once per request
/// by <see cref="MoneyLoaderFilter"/> before any view renders.
/// </summary>
public sealed class Money
{
    private static readonly NumberFormatInfo LakhFormat = new()
    {
        NumberGroupSeparator = ",", NumberDecimalSeparator = ".", NumberGroupSizes = [3, 2], NegativeSign = "-"
    };

    private static readonly NumberFormatInfo WesternFormat = new()
    {
        NumberGroupSeparator = ",", NumberDecimalSeparator = ".", NumberGroupSizes = [3], NegativeSign = "-"
    };

    public CurrencySettings Settings { get; private set; } = CurrencySettings.Default;

    public void Load(CurrencySettings settings) => Settings = settings;

    public CurrencyInfo Purchase => Settings.Purchase;
    public CurrencyInfo Sales => Settings.Sales;

    /// <summary>True when "All companies" mixes companies with different currencies – amounts are shown without a symbol.</summary>
    public bool IsMixed => Settings.IsMixed;

    public string BuyCode => IsMixed ? "FX" : Purchase.Code;
    public string SaleCode => IsMixed ? "local" : Sales.Code;
    public string BuySymbol => IsMixed ? "" : Purchase.Symbol;
    public string SaleSymbol => IsMixed ? "" : Sales.Symbol;

    /// <summary>"1 SGD = ? BDT" – label for exchange-rate inputs.</summary>
    public string RateLabel => $"1 {BuyCode} = ? {SaleCode}";

    /// <summary>"1 SGD = 90.50 BDT"</summary>
    public string RateText(decimal rate) => $"1 {BuyCode} = {Fmt.Rate(rate)} {SaleCode}";

    /// <summary>Selling-side amount: ৳ 12,500 / $ 1,250.00</summary>
    public string Sale(decimal value) => Format(value, IsMixed ? null : Sales);
    public string Sale(decimal? value) => value is null ? "—" : Sale(value.Value);

    /// <summary>Purchase-side amount: S$ 120.50</summary>
    public string Buy(decimal value) => Format(value, IsMixed ? null : Purchase);
    public string Buy(decimal? value) => value is null ? "—" : Buy(value.Value);

    private static string Format(decimal value, CurrencyInfo? currency)
    {
        var decimals = currency?.Decimals ?? 2;
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var pattern = decimals == 0 ? "#,0" : "#,0." + new string('0', decimals);
        var text = Math.Abs(rounded).ToString(pattern, currency?.LakhGrouping == true ? LakhFormat : WesternFormat);
        var symbol = currency is null ? "" : currency.Symbol + " ";
        return rounded < 0 ? $"-{symbol}{text}" : $"{symbol}{text}";
    }
}

/// <summary>Loads the viewed company's currencies before a signed-in page renders.</summary>
public sealed class MoneyLoaderFilter(Money money, ILookupService lookups) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult or PartialViewResult or ViewComponentResult &&
            context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            money.Load(await lookups.CurrencySettingsAsync());
        }

        await next();
    }
}
