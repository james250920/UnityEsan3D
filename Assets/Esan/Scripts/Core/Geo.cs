// Coordenadas geográficas y metros locales. Portado de geo-coordinates.js y ar-geo.js del mapa web.
// No depende de Unity: se prueba con `dotnet run` en PruebasCore.
using System;
using System.Collections.Generic;

namespace Esan
{
    /// <summary>Coordenada WGS84. En los JSON va como [longitud, latitud].</summary>
    public readonly struct Coord
    {
        public readonly double Lon, Lat;
        public Coord(double lon, double lat)
        {
            if (double.IsNaN(lon) || double.IsNaN(lat) || Math.Abs(lon) > 180 || Math.Abs(lat) > 90)
                throw new ArgumentException("Coordenada inválida: usa [longitud, latitud].");
            Lon = lon;
            Lat = lat;
        }
        public override string ToString() => $"[{Lon}, {Lat}]";
    }

    /// <summary>Metros respecto a un origen: X hacia el este, Y hacia el norte.</summary>
    public readonly struct Metros
    {
        public readonly double Este, Norte;
        public Metros(double este, double norte) { Este = este; Norte = norte; }
        public double Longitud => Math.Sqrt(Este * Este + Norte * Norte);
    }

    public static class Geo
    {
        const double R = 6371008.8, Rad = Math.PI / 180;

        public static double DistanciaMetros(Coord a, Coord b)
        {
            double dLat = (b.Lat - a.Lat) * Rad, dLon = (b.Lon - a.Lon) * Rad;
            double h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Pow(Math.Sin(dLon / 2), 2);
            return 2 * R * Math.Asin(Math.Sqrt(Math.Min(1, Math.Max(0, h))));
        }

        public static Metros AMetrosLocales(Coord c, Coord origen) =>
            new Metros((c.Lon - origen.Lon) * Rad * R * Math.Cos(origen.Lat * Rad), (c.Lat - origen.Lat) * Rad * R);

        public static Coord DesdeMetrosLocales(Metros m, Coord origen) =>
            new Coord(origen.Lon + m.Este / (Rad * R * Math.Cos(origen.Lat * Rad)), origen.Lat + m.Norte / (Rad * R));

        /// <summary>0 = norte, 90 = este.</summary>
        public static double Rumbo(Coord desde, Coord hasta)
        {
            var m = AMetrosLocales(hasta, desde);
            return (Math.Atan2(m.Este, m.Norte) / Rad + 360) % 360;
        }

        /// <summary>Lleva un ángulo a (-180, 180].</summary>
        public static double AnguloConSigno(double grados)
        {
            double valor = ((grados + 180) % 360 + 360) % 360 - 180;
            return valor == -180 ? 180 : valor;
        }

        /// <summary>El perímetro se cierra solo; un punto sobre el borde cuenta como dentro.</summary>
        public static bool DentroDelPerimetro(Coord p, IReadOnlyList<Coord> perimetro)
        {
            if (perimetro == null || perimetro.Count < 3) return false;
            bool dentro = false;
            for (int i = 0, j = perimetro.Count - 1; i < perimetro.Count; j = i++)
            {
                Coord a = perimetro[j], b = perimetro[i];
                double dx = b.Lon - a.Lon, dy = b.Lat - a.Lat, largo2 = dx * dx + dy * dy;
                if (largo2 > 0)
                {
                    double t = ((p.Lon - a.Lon) * dx + (p.Lat - a.Lat) * dy) / largo2;
                    double ex = p.Lon - a.Lon - t * dx, ey = p.Lat - a.Lat - t * dy;
                    if (t >= 0 && t <= 1 && Math.Sqrt(ex * ex + ey * ey) < 1e-10) return true;
                }
                if ((a.Lat > p.Lat) != (b.Lat > p.Lat) && p.Lon < (b.Lon - a.Lon) * (p.Lat - a.Lat) / (b.Lat - a.Lat) + a.Lon) dentro = !dentro;
            }
            return dentro;
        }

        public static double DistanciaAlPerimetro(Coord p, IReadOnlyList<Coord> perimetro)
        {
            double mejor = double.PositiveInfinity;
            for (int i = 0, j = perimetro.Count - 1; i < perimetro.Count; j = i++)
            {
                Metros a = AMetrosLocales(perimetro[j], p), b = AMetrosLocales(perimetro[i], p);
                double dx = b.Este - a.Este, dy = b.Norte - a.Norte, largo2 = dx * dx + dy * dy;
                double t = largo2 > 0 ? Math.Max(0, Math.Min(1, -(a.Este * dx + a.Norte * dy) / largo2)) : 0;
                mejor = Math.Min(mejor, new Metros(a.Este + t * dx, a.Norte + t * dy).Longitud);
            }
            return mejor;
        }
    }
}
