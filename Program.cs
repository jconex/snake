using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace SnakePOO;

// Coordenada de una casilla del tablero.
public readonly record struct Posicion(int X, int Y)
{
    public Posicion Desplazar(Direccion direccion) => direccion switch
    {
        Direccion.Arriba => this with { Y = Y - 1 },
        Direccion.Abajo => this with { Y = Y + 1 },
        Direccion.Izquierda => this with { X = X - 1 },
        Direccion.Derecha => this with { X = X + 1 },
        _ => this
    };
}

public enum Direccion { Arriba, Abajo, Izquierda, Derecha }

public class Serpiente
{
    private readonly LinkedList<Posicion> cuerpo = new();
    public Direccion DireccionActual { get; private set; } = Direccion.Derecha;
    public Posicion Cabeza => cuerpo.First!.Value;
    public IEnumerable<Posicion> Cuerpo => cuerpo;

    public Serpiente(Posicion posicionInicial, int longitudInicial = 4)
    {
        for (int i = 0; i < longitudInicial; i++)
            cuerpo.AddLast(new Posicion(posicionInicial.X - i, posicionInicial.Y));
    }

    public void CambiarDireccion(Direccion nueva)
    {
        // Impide girar 180 grados sobre el propio cuerpo.
        bool opuesta = (DireccionActual, nueva) switch
        {
            (Direccion.Arriba, Direccion.Abajo) => true,
            (Direccion.Abajo, Direccion.Arriba) => true,
            (Direccion.Izquierda, Direccion.Derecha) => true,
            (Direccion.Derecha, Direccion.Izquierda) => true,
            _ => false
        };
        if (!opuesta) DireccionActual = nueva;
    }

    public Posicion SiguienteCabeza() => Cabeza.Desplazar(DireccionActual);

    public bool ChocaConsigoMisma(Posicion destino, bool crecera)
    {
        // Al avanzar sin comer, la casilla de la cola se libera en ese turno.
        return (crecera ? cuerpo : cuerpo.SkipLast(1)).Contains(destino);
    }

    public void Mover(Posicion destino, bool crecer)
    {
        cuerpo.AddFirst(destino);
        if (!crecer) cuerpo.RemoveLast();
    }
}

public class Comida
{
    private static readonly Random Aleatorio = new();
    public Posicion PosicionActual { get; private set; }

    public bool Generar(Tablero tablero, Serpiente serpiente)
    {
        var ocupadas = serpiente.Cuerpo.ToHashSet();
        var libres = new List<Posicion>();
        for (int y = 1; y < tablero.Alto - 1; y++)
            for (int x = 1; x < tablero.Ancho - 1; x++)
            {
                var posicion = new Posicion(x, y);
                if (!ocupadas.Contains(posicion)) libres.Add(posicion);
            }

        if (libres.Count == 0) return false;
        PosicionActual = libres[Aleatorio.Next(libres.Count)];
        return true;
    }
}

public class Tablero
{
    public const char SimboloPared = '■';
    public const char SimboloSerpiente = '▣';
    public const char SimboloComida = '◎';

    public int Ancho { get; } = 61;
    public int Alto { get; } = 30;

    public bool EsPared(Posicion posicion) =>
        posicion.X <= 0 || posicion.X >= Ancho - 1 ||
        posicion.Y <= 0 || posicion.Y >= Alto - 1;

    public void Dibujar(Serpiente serpiente, Comida comida, int puntos)
    {
        // Limpiamos mediante una llamada a la API de la consola.
        Console.Clear();
        var partes = serpiente.Cuerpo.ToHashSet();
        var pantalla = new StringBuilder((Ancho * 2 + 1) * (Alto + 2));
        for (int y = 0; y < Alto; y++)
        {
            for (int x = 0; x < Ancho; x++)
            {
                var p = new Posicion(x, y);
                char simbolo = EsPared(p) ? SimboloPared
                    : partes.Contains(p) ? SimboloSerpiente
                    : p == comida.PosicionActual ? SimboloComida : ' ';
                pantalla.Append(simbolo).Append(' ');
            }
            pantalla.AppendLine();
        }
        pantalla.AppendLine($"Puntos: {puntos} | Flechas: mover | Esc: salir");
        Console.Write(pantalla.ToString());
    }
}

public class Juego
{
    private readonly Tablero tablero = new();
    private readonly Serpiente serpiente = new(new Posicion(12, 15));
    private readonly Comida comida = new();
    private int puntos;
    private bool terminado;
    private bool victoria;

    public void Ejecutar()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        try
        {
            if (!comida.Generar(tablero, serpiente)) return;
            tablero.Dibujar(serpiente, comida, puntos);
            var reloj = Stopwatch.StartNew();
            long siguienteMovimiento = 1000;
            while (!terminado)
            {
                // La lectura de teclas no requiere pulsar Enter.
                while (Console.KeyAvailable)
                {
                    var tecla = Console.ReadKey(intercept: true).Key;
                    switch (tecla)
                    {
                        case ConsoleKey.UpArrow: serpiente.CambiarDireccion(Direccion.Arriba); break;
                        case ConsoleKey.DownArrow: serpiente.CambiarDireccion(Direccion.Abajo); break;
                        case ConsoleKey.LeftArrow: serpiente.CambiarDireccion(Direccion.Izquierda); break;
                        case ConsoleKey.RightArrow: serpiente.CambiarDireccion(Direccion.Derecha); break;
                        case ConsoleKey.Escape: terminado = true; break;
                    }
                }
                if (terminado) break;
                if (reloj.ElapsedMilliseconds < siguienteMovimiento)
                {
                    Thread.Sleep(10);
                    continue;
                }
                siguienteMovimiento += 400;
                Actualizar();
                if (!terminado) tablero.Dibujar(serpiente, comida, puntos);
            }
            Console.Clear();
            Console.WriteLine(victoria ? "¡HAS GANADO!" : "GAME OVER");
            Console.WriteLine($"Puntos conseguidos: {puntos}");
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    private void Actualizar()
    {
        var destino = serpiente.SiguienteCabeza();
        bool come = destino == comida.PosicionActual;
        if (tablero.EsPared(destino) || serpiente.ChocaConsigoMisma(destino, come))
        {
            terminado = true;
            return;
        }
        serpiente.Mover(destino, come);
        if (come)
        {
            puntos++;
            if (!comida.Generar(tablero, serpiente))
            {
                terminado = true;
                victoria = true;
            }
        }
    }
}

public static class Program
{
    public static void Main()
    {
        new Juego().Ejecutar();
    }
}
