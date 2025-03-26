using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace MsixPackaging.Core.Compression;

public static class Extensions
{
    public static IObservable<byte[]> ReadMyBytes(this Stream stream, long offset, int tamaño)
    { 
        // Posicionamos el stream en el offset indicado.
        stream.Seek(offset, SeekOrigin.Begin);

        return Observable.Create<byte[]>(observer =>
        {
            // Creamos un token para poder cancelar la lectura en caso de desuscripción.
            var cts = new CancellationTokenSource();

            // Función recursiva asíncrona que realiza la lectura.
            Func<Task> leerRecursivamente = null;
            leerRecursivamente = async () =>
            {
                try
                {
                    // Si se solicita cancelar, se sale.
                    cts.Token.ThrowIfCancellationRequested();

                    var buffer = new byte[tamaño];
                    int bytesLeidos = await stream.Leer(buffer, 0, tamaño).ConfigureAwait(false);
                    if (bytesLeidos == 0)
                    {
                        // No hay más datos: completamos la secuencia.
                        observer.OnCompleted();
                        return;
                    }

                    // Si se leyeron menos bytes de los esperados, ajustamos el buffer.
                    if (bytesLeidos < tamaño)
                        buffer = buffer.Take(bytesLeidos).ToArray();

                    observer.OnNext(buffer);

                    // Llamada recursiva para continuar la lectura.
                    await leerRecursivamente().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            };

            // Arrancamos la lectura recursiva.
            leerRecursivamente();

            // Devolvemos un Disposable que cancele la operación al desuscribirse.
            return Disposable.Create(() => cts.Cancel());
        });
    }
}