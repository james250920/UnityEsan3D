// Estado de la ubicación: lecturas, caducidad y comprobación estable del perímetro. Portado de location-state.js
// y de updateCourse (ar-geo.js) del mapa web. Los tiempos van en milisegundos.
using System;
using System.Collections.Generic;

namespace Esan
{
    public enum Zona { Desconocida, Dentro, Fuera }
    public enum Guia { PorConfirmar, Ok, FueraDelCampus }

    public sealed class ConfigUbicacion
    {
        public double MaxPrecisionParaGuiaMetros = 65;  // Por encima, las indicaciones pasan a "Ubicación por confirmar".
        public double CaducaMs = 30000;                 // Una medición más antigua deja de ser fiable.
        public double MargenMetros = 12;                // Margen mínimo junto al perímetro antes de contar una salida.
        public double MargenMaximoMetros = 40;
        public double SalirMs = 6000;                   // Tiempo fuera del margen para confirmar la salida...
        public int SalirLecturas = 3;                   // ...o número de lecturas seguidas.
        public double EntrarMs = 3000;
        public int EntrarLecturas = 2;
    }

    public sealed class Ubicacion
    {
        readonly IReadOnlyList<Coord> perimetro;
        readonly ConfigUbicacion config;
        bool siguiendo, hayPendiente;
        Zona pendiente;
        int pendienteLecturas;
        double pendienteDesde;

        public bool Caducada { get; private set; }
        public bool HayLectura { get; private set; }
        public Coord Coordenada { get; private set; }
        public double Precision { get; private set; }
        public double MarcaMs { get; private set; }
        public Zona Zona { get; private set; }

        public Ubicacion(IReadOnlyList<Coord> perimetro, ConfigUbicacion config = null)
        {
            this.perimetro = perimetro;
            this.config = config ?? new ConfigUbicacion();
        }

        bool Utilizable => !(Precision > config.MaxPrecisionParaGuiaMetros);

        public void Lectura(Coord coordenada, double precision, double marcaMs, double ahoraMs)
        {
            siguiendo = true; Caducada = false; HayLectura = true;
            Coordenada = coordenada; Precision = precision; MarcaMs = marcaMs;
            if (ahoraMs - marcaMs > config.CaducaMs) { siguiendo = false; Caducada = true; hayPendiente = false; return; }
            // Una lectura muy imprecisa no sirve para decidir que alguien entró o salió.
            if (!Utilizable) { hayPendiente = false; return; }
            bool dentro = Geo.DentroDelPerimetro(coordenada, perimetro), borde = false;
            if (!dentro)
            {
                double margen = Math.Min(config.MargenMaximoMetros, Math.Max(config.MargenMetros, precision > 0 ? precision : 0));
                borde = Geo.DistanciaAlPerimetro(coordenada, perimetro) <= margen;
            }
            if (Zona == Zona.Desconocida) { Zona = dentro ? Zona.Dentro : Zona.Fuera; hayPendiente = false; return; }
            if (borde || dentro && Zona == Zona.Dentro || !dentro && Zona == Zona.Fuera) { hayPendiente = false; return; }
            Zona destino = dentro ? Zona.Dentro : Zona.Fuera;
            if (hayPendiente && pendiente == destino) pendienteLecturas++;
            else { hayPendiente = true; pendiente = destino; pendienteLecturas = 1; pendienteDesde = ahoraMs; }
            Asentar(ahoraMs);
        }

        /// <summary>Sin lecturas nuevas: confirma una transición pendiente o marca la medición como caducada.</summary>
        public void Tic(double ahoraMs)
        {
            if (!siguiendo) return;
            if (ahoraMs - MarcaMs >= config.CaducaMs) { siguiendo = false; Caducada = true; hayPendiente = false; return; }
            if (hayPendiente) Asentar(ahoraMs);
        }

        void Asentar(double ahoraMs)
        {
            bool saliendo = pendiente == Zona.Fuera;
            double ms = saliendo ? config.SalirMs : config.EntrarMs;
            int lecturas = saliendo ? config.SalirLecturas : config.EntrarLecturas;
            if (ahoraMs - pendienteDesde >= ms || pendienteLecturas >= lecturas) { Zona = pendiente; hayPendiente = false; }
        }

        /// <summary>¿Se pueden mostrar distancias e indicaciones como actuales?</summary>
        public Guia Guia =>
            !siguiendo || !Utilizable ? Guia.PorConfirmar : Zona == Zona.Dentro ? Guia.Ok : Zona == Zona.Fuera ? Guia.FueraDelCampus : Guia.PorConfirmar;
    }

    /// <summary>Rumbo deducido al caminar: compara la posición con la última de referencia.</summary>
    public sealed class RumboAlCaminar
    {
        bool hayAncla;
        Coord ancla;
        public double? Rumbo { get; private set; }

        public void Actualizar(Coord coordenada, double precision, double minimoMetros = 5, double maximaPrecisionMetros = 20)
        {
            if (precision > maximaPrecisionMetros) return;
            if (!hayAncla) { ancla = coordenada; hayAncla = true; return; }
            if (Geo.DistanciaMetros(ancla, coordenada) < minimoMetros) return;
            Rumbo = Geo.Rumbo(ancla, coordenada);
            ancla = coordenada;
        }
    }
}
