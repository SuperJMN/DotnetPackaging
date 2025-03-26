namespace MsixPackaging.Core.ContentTypes;

// Representa una entrada de Default en el [Content_Types].xml
public record DefaultContentType(string Extension, string ContentType);

// Representa una entrada de Override en el [Content_Types].xml

// Modelo inmutable que agrupa ambas colecciones.