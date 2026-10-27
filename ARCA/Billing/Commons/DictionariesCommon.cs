// <copyright file="DictionariesCommon.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace ElRoso.ARCA.Billing;

internal static class DictionariesCommon
{
    public static readonly IReadOnlyDictionary<string, string> Currencies = new Dictionary<string, string>
    {
        { "Pesos",   "PES" },
        { "Dolares", "DOL" },
        { "Euro",    "060" },
    };

    public static readonly IReadOnlyDictionary<string, int> Tax = new Dictionary<string, int>
    {
        { "No Gravado", 3 },
        { "Exento",     2 },
        { "0.00",       3 },
        { "10.50",      4 },
        { "21.00",      5 },
        { "27.00",      6 },
        { "5.00",       8 },
        { "2.50",       9 },
    };

    public static readonly IReadOnlyDictionary<string, short> OtherTax = new Dictionary<string, short>
    {
        { "Impuestos nacionales",    1 },
        { "Impuestos provinciales",  2 },
        { "Impuestos municipales",   3 },
        { "Impuestos internos",      4 },
        { "Otros",                  99 },
    };

    public static readonly IReadOnlyDictionary<string, short> Language = new Dictionary<string, short>
    {
        { "Español",   1 },
        { "Inglés",    2 },
        { "Portugués", 3 },
    };
}
