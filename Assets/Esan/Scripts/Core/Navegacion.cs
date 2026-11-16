// Progreso sobre la ruta, tramo de guía y textos de las indicaciones. Portado de ar-geo.js del mapa web.
using System;
using System.Collections.Generic;

namespace Esan
{
    public sealed class Progreso
    {
        public double FueraDeRutaMetros;   // Distancia del usuario al punto más cercano de la ruta.
        public double RecorridoMetros;
        public double RestanteMetros;
        public bool Volviendo;             // Está lejos del camino: la indicación lo lleva de vuelta.
        public Coord Proyeccion;           // Punto más cercano de la ruta.
        public double SiguienteMetros;
        public double SiguienteRumbo;      // 0 = norte, 90 = este.
    }

    public readonly struct Muestra
    {
        public readonly double Este, Norte, Rumbo;
        public Muestra(double este, double norte, double rumbo) { Este = este; Norte = norte; Rumbo = rumbo; }
    }

    public static class Navegacion
    {
        const double Rad = Math.PI / 180, MetrosPorMinuto = 72;

        /// <summary>Sitúa al usuario sobre la ruta y calcula lo que falta y hacia dónde seguir.</summary>
        public static Progreso ProgresoEnRuta(IReadOnlyList<Coord> ruta, Coord usuario, double anticipoMetros, double volverMetros)
        {
            var local = Locales(ruta, usuario);
            var acumulado = new double[local.Length];
            for (int i = 1; i < local.Length; i++) acumulado[i] = acumulado[i - 1] + Largo(local[i].Este - local[i - 1].Este, local[i].Norte - local[i - 1].Norte);
            double total = acumulado[local.Length - 1];
            double fuera = local[0].Longitud, recorrido = 0;
            Metros proyeccion = local[0];
            for (int i = 1; i < local.Length; i++)
            {
                Cercano(local[i - 1], local[i], out double t, out Metros punto, out double largo);
                if (punto.Longitud < fuera) { fuera = punto.Longitud; recorrido = acumulado[i - 1] + t * largo; proyeccion = punto; }
            }
            bool volviendo = fuera > volverMetros;
            Metros siguiente = proyeccion;
            if (!volviendo)
            {
                int indice = Array.FindIndex(acumulado, valor => valor - recorrido >= anticipoMetros);
                siguiente = local[indice < 0 ? local.Length - 1 : indice];
            }
            return new Progreso
            {
                FueraDeRutaMetros = fuera,
                RecorridoMetros = recorrido,
                RestanteMetros = fuera + total - recorrido,
                Volviendo = volviendo,
                Proyeccion = Geo.DesdeMetrosLocales(proyeccion, usuario),
                SiguienteMetros = siguiente.Longitud,
                SiguienteRumbo = (Math.Atan2(siguiente.Este, siguiente.Norte) / Rad + 360) % 360
            };
        }

        /// <summary>
        /// Tramo que dibuja la guía, en metros respecto a origen: sale del usuario, llega al camino y lo sigue.
        /// Si el usuario ya está en el camino, se une un poco más adelante para no dibujar un quiebre.
        /// </summary>
        public static List<Metros> TramoDeGuia(IReadOnlyList<Coord> ruta, Coord usuario, Coord origen, double alcanceMetros, double unionMetros)
        {
            var local = Locales(ruta, usuario);
            double fuera = local[0].Longitud;
            int segmento = 1;
            Metros en = local[0];
            for (int i = 1; i < local.Length; i++)
            {
                Cercano(local[i - 1], local[i], out _, out Metros punto, out _);
                if (punto.Longitud < fuera) { fuera = punto.Longitud; segmento = i; en = punto; }
            }
            var tramo = new List<Metros> { new Metros(0, 0) };
            double queda = alcanceMetros, salto = unionMetros;
            if (fuera > unionMetros) { tramo.Add(en); queda -= fuera; salto = 0; }
            Metros previo = en;
            for (int i = segmento; i < local.Length && queda > 0; i++)
            {
                Metros punto = local[i];
                double largo = Largo(punto.Este - previo.Este, punto.Norte - previo.Norte);
                if (largo <= salto) { salto -= largo; previo = punto; continue; }
                double util = largo - salto;
                if (salto > 0) tramo.Add(Entre(previo, punto, salto / largo));
                tramo.Add(Entre(previo, punto, Math.Min(1, (salto + Math.Min(queda, util)) / largo)));
                queda -= util; salto = 0; previo = punto;
            }
            if (tramo.Count < 2 && local.Length > 0) tramo.Add(local[local.Length - 1]);
            Metros desplazamiento = Geo.AMetrosLocales(usuario, origen);
            return tramo.ConvertAll(p => new Metros(p.Este + desplazamiento.Este, p.Norte + desplazamiento.Norte));
        }

        /// <summary>Redondea las esquinas y reparte puntos equidistantes con su rumbo.</summary>
        public static List<Muestra> Muestrear(IReadOnlyList<Metros> tramo, double separacion)
        {
            var puntos = new List<Metros>(tramo);
            for (int ronda = 0; ronda < 2 && puntos.Count > 2; ronda++)
            {
                var suaves = new List<Metros> { puntos[0] };
                for (int i = 1; i < puntos.Count; i++) { suaves.Add(Entre(puntos[i - 1], puntos[i], .25)); suaves.Add(Entre(puntos[i - 1], puntos[i], .75)); }
                suaves.Add(puntos[puntos.Count - 1]);
                puntos = suaves;
            }
            var muestras = new List<Muestra>();
            double caminado = 0, proxima = 0;
            for (int i = 1; i < puntos.Count; i++)
            {
                Metros a = puntos[i - 1], b = puntos[i];
                double largo = Largo(b.Este - a.Este, b.Norte - a.Norte);
                if (largo == 0) continue;
                double rumbo = (Math.Atan2(b.Este - a.Este, b.Norte - a.Norte) / Rad + 360) % 360;
                while (proxima <= caminado + largo)
                {
                    double t = (proxima - caminado) / largo;
                    muestras.Add(new Muestra(a.Este + t * (b.Este - a.Este), a.Norte + t * (b.Norte - a.Norte), rumbo));
                    proxima += separacion;
                }
                caminado += largo;
            }
            return muestras;
        }

        /// <summary>relativo: ángulo del siguiente punto respecto a donde mira el usuario; positivo a la derecha.</summary>
        public static string IndicacionDeGiro(double relativo)
        {
            double magnitud = Math.Abs(relativo);
            string lado = relativo > 0 ? "derecha" : "izquierda";
            if (magnitud < 25) return "Continúa recto";
            if (magnitud < 70) return "Gira ligeramente a la " + lado;
            if (magnitud < 135) return "Gira a la " + lado;
            return "Da la vuelta";
        }

        public static double UmbralDeLlegada(double precision, double minimo, double maximo) =>
            Math.Min(maximo, Math.Max(minimo, precision > 0 ? precision : 0));

        public static string TextoDistancia(double metros)
        {
            if (double.IsNaN(metros) || double.IsInfinity(metros)) return "—";
            return (metros < 50 ? Redondear(metros) : Redondear(metros / 5) * 5) + " m";
        }

        public static string TiempoAPie(double metros) =>
            metros < 60 ? "menos de 1 min a pie" : "unos " + (long)Math.Ceiling(metros / MetrosPorMinuto) + " min a pie";

        // Math.Round de C# redondea ,5 al par; el mapa web redondea hacia arriba.
        static long Redondear(double valor) => (long)Math.Floor(valor + .5);
        static double Largo(double x, double y) => Math.Sqrt(x * x + y * y);
        static Metros Entre(Metros a, Metros b, double t) => new Metros(a.Este + t * (b.Este - a.Este), a.Norte + t * (b.Norte - a.Norte));

        static Metros[] Locales(IReadOnlyList<Coord> ruta, Coord usuario)
        {
            if (ruta == null || ruta.Count == 0) throw new ArgumentException("La ruta no tiene puntos.");
            var local = new Metros[ruta.Count];
            for (int i = 0; i < local.Length; i++) local[i] = Geo.AMetrosLocales(ruta[i], usuario);
            return local;
        }

        // Punto del segmento a-b más cercano al usuario, que está en el origen.
        static void Cercano(Metros a, Metros b, out double t, out Metros punto, out double largo)
        {
            double dx = b.Este - a.Este, dy = b.Norte - a.Norte, largo2 = dx * dx + dy * dy;
            t = largo2 > 0 ? Math.Max(0, Math.Min(1, -(a.Este * dx + a.Norte * dy) / largo2)) : 0;
            punto = new Metros(a.Este + t * dx, a.Norte + t * dy);
            largo = Math.Sqrt(largo2);
        }
    }
}
