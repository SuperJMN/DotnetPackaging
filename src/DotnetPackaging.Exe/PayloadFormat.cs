namespace DotnetPackaging.Exe;

internal static class PayloadFormat
{
    public const string Magic = "DPACKEXE1"; // 8 ASCII bytes
    public const int FooterLength = 8 /*len*/ + 8 /*magic*/; // 16 bytes
}
