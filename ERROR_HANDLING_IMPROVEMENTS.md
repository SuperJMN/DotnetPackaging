# Mejoras en el Manejo de Errores de DotnetPackaging

## Problema Original

Cuando `dotnet publish` fallaba, la herramienta mostraba:
1. Todo el output masivo del comando `dotnet publish` (cientos de líneas)
2. Un stack trace largo y confuso con `System.InvalidOperationException: Process failed with exit code 1`
3. No era claro cuál era el verdadero error de compilación
4. El usuario tenía que buscar manualmente el error en el "muro de texto"

## Cambios Implementados

### 1. **DotnetPublisher.cs** - Extracción Inteligente de Errores

Se agregó el método `ExtractPublishErrors()` que:
- Filtra el output del `dotnet publish` para extraer solo las líneas relevantes
- Identifica errores por patrones comunes: `CS####`, `MSB####`, `NETSDK####`
- Busca líneas que contienen ": error " o ": warning "
- Captura mensajes de "Build FAILED"
- Limita la salida a las primeras 20 errores para evitar sobrecarga
- Si no encuentra errores específicos, muestra las últimas 30 líneas del output

**Beneficio**: El usuario ve solo los mensajes de error relevantes, no todo el output del build.

### 2. **RpmPackagerExtensions.cs** - Manejo Estructurado de Fases

Se refactorizó `PackProject()` para:
- Separar claramente las fases: Publishing → Packaging → Writing
- Manejar errores en cada fase con mensajes específicos
- Proporcionar contexto sobre en qué fase falló
- Evitar stack traces confusos
- Usar try-catch para capturar errores inesperados

**Beneficio**: El usuario sabe exactamente en qué etapa falló (publish, packaging, o escritura del archivo).

### 3. **ConsoleMixin.cs** - Mejores Mensajes y Exit Codes

Se mejoró `WriteResult()` para:
- Mostrar mensajes claros: "Operation failed: {Error}" vs solo el error crudo
- Establecer `Environment.ExitCode = 1` cuando hay fallos (importante para scripts y CI/CD)
- Mensajes de éxito más descriptivos

**Beneficio**: La herramienta comunica correctamente el estado de la operación al sistema operativo y muestra mensajes más amigables.

## Ejemplo de Salida Mejorada

### Antes:
```
[11:05:32 ERR DotnetPackaging.Tool] dotnet publish failed for /path/to/project.csproj: Process failed with exit code 1
Excepción no interceptada:System.InvalidOperationException: Process failed with exit code 1
   at Zafiro.DivineBytes.ByteSourceExtensions.<>c__DisplayClass9_0.<<WriteTo>b__1>d.MoveNext()
[... stack trace enorme ...]
```

### Después:
```
[11:05:32 ERR DotnetPackaging.Tool] dotnet publish failed for /path/to/project.csproj
[11:05:32 ERR DotnetPackaging.Tool] Build errors:
/path/to/MyClass.cs(42,15): error CS0246: The type or namespace name 'InvalidType' could not be found
/path/to/AnotherFile.cs(10,20): error CS1002: ; expected
Build FAILED.
[11:05:32 ERR DotnetPackaging.Tool] Failed to publish project: dotnet publish failed. Build errors: [...]
[11:05:32 ERR DotnetPackaging.Tool] Operation failed: Project publish failed: dotnet publish failed. Build errors: [...]
```

## Recomendaciones para el Usuario

Para mejores mensajes de error, usar la bandera `--verbose`:
```bash
dotnet src/DotnetPackaging.Tool/bin/Debug/net10.0/DotnetPackaging.Tool.dll rpm from-project --verbose --project ...
```

## Testing

Para probar los cambios:
1. Compilar: `dotnet build src/DotnetPackaging.Tool/DotnetPackaging.Tool.csproj`
2. Crear un proyecto con errores de compilación intencionales
3. Ejecutar el empaquetado y verificar que los errores se muestren claramente
