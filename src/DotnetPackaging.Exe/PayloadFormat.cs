namespace DotnetPackaging.Exe;

internal static class PayloadFormat
{
    public const string Magic = "DPACKEXE1"; // 9 ASCII bytes
    public const int FooterLength = 8 /*len*/ + 9 /*magic*/; // 17 bytes
}
