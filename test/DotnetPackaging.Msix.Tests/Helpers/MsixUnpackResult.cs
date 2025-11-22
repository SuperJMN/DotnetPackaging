namespace MsixPackaging.Tests.Helpers;

using System;

/// <summary>
/// Clase que representa el resultado de una operación de desempaquetado MSIX
/// </summary>
public class MsixUnpackResult
{
    /// <summary>
    /// Indica si la operación fue exitosa
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Código de salida del proceso makeappx
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Salida estándar del proceso
    /// </summary>
    public string StandardOutput { get; set; }

    /// <summary>
    /// Salida de error del proceso
    /// </summary>
    public string ErrorOutput { get; set; }

    /// <summary>
    /// Mensaje de error, si hay alguno
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Excepción que se produjo, si hay alguna
    /// </summary>
    public Exception Exception { get; set; }
}