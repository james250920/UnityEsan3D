// Lleva una coordenada geográfica al modelo 3D del campus (Resources/Modelos/campus.fbx), con el encaje del mapa web:
// rotación y escala hacia un punto de anclaje y luego una corrección por triángulos (Resources/Datos/calibracion.json).
using System;
using System.Collections.Generic;

namespace Esan
{
    public sealed class Calibracion
    {
        const double R = 6371008.8;
        public Coord Origen, Anclaje;
        public List<double[]> Base = new List<double[]>(), Destino = new List<double[]>();
        public List<int[]> Triangulos = new List<int[]>();

        /// <summary>Punto del plano del mapa web (en píxeles del plano).</summary>
        public double[] APlano(Coord c)
        {
            double coseno = Math.Cos(Rad(Origen.Lat));
            double e = Rad(c.Lon - Origen.Lon) * R * coseno, n = Rad(c.Lat - Origen.Lat) * R;
            double puntaE = Rad(Anclaje.Lon - Origen.Lon) * R * coseno, puntaN = Rad(Anclaje.Lat - Origen.Lat) * R;
            double rumbo = Math.Atan2(puntaN, puntaE), factor = 999.0 / Math.Sqrt(puntaE * puntaE + puntaN * puntaN);
            double x = 61.0 + (e * Math.Cos(rumbo) + n * Math.Sin(rumbo)) * factor;
            double z = 798.0 - (-e * Math.Sin(rumbo) + n * Math.Cos(rumbo)) * factor;
            double px = 560.5 + (x - 560.5) * 1.08, py = 798.0 + (z - 798.0) * 1.08 + 31.04;
            foreach (int[] cara in Triangulos)
            {
                double[] a = Base[cara[0]], b = Base[cara[1]], k = Base[cara[2]];
                double denominador = (b[1] - k[1]) * (a[0] - k[0]) + (k[0] - b[0]) * (a[1] - k[1]);
                if (Math.Abs(denominador) < 1e-10) continue;
                double u = ((b[1] - k[1]) * (px - k[0]) + (k[0] - b[0]) * (py - k[1])) / denominador;
                double v = ((k[1] - a[1]) * (px - k[0]) + (a[0] - k[0]) * (py - k[1])) / denominador;
                double w = 1 - u - v;
                if (Math.Min(u, Math.Min(v, w)) < -1e-8) continue;
                double[] ta = Destino[cara[0]], tb = Destino[cara[1]], tk = Destino[cara[2]];
                return new[] { ta[0] * u + tb[0] * v + tk[0] * w, ta[1] * u + tb[1] * v + tk[1] * w };
            }
            return new[] { px, py };
        }

        /// <summary>Posición horizontal en el modelo, con los ejes del glTF original (x, z).</summary>
        public static double[] PlanoAModelo(double[] p) => new[]
        {
            (p[0] * 0.00028223005544546724 - p[1] * 0.0000000567301173405602 + 0.000023240634454648802) * 1000,
            (p[0] * 0.000000017227872639671277 + p[1] * 0.00028211226278032994 - 0.2423586899631824) * 1000
        };

        public double[] AModelo(Coord c) => PlanoAModelo(APlano(c));

        static double Rad(double grados) => grados * Math.PI / 180;
    }
}
