// Carga los datos del campus, los mismos archivos que usa el mapa web (Assets/Esan/Resources/Datos).
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Esan
{
    /// <summary>Destino de la lista: un evento en un punto del campus.</summary>
    public sealed class Lugar
    {
        public string Nombre;
        public string Fecha;
        public Coord Coordenada;
    }

    public static class EsanDatos
    {
        public static GrafoCaminos CargarGrafo()
        {
            JObject json = Leer("grafo-caminos-esan");
            var grafo = new GrafoCaminos();
            foreach (JToken nodo in json["nodos"]) grafo.AgregarNodo((string)nodo["id"], Coordenada(nodo["coordenadas"]));
            foreach (JToken arista in json["aristas"]) grafo.AgregarUnion((string)arista["desde"], (string)arista["hasta"]);
            return grafo;
        }

        public static List<Coord> CargarPerimetro()
        {
            var perimetro = new List<Coord>();
            foreach (JToken punto in Leer("esan-boundary")["features"][0]["geometry"]["coordinates"]) perimetro.Add(Coordenada(punto));
            return perimetro;
        }

        /// <summary>Eventos de Resources/Datos/eventos.json: los únicos destinos de la lista.</summary>
        public static List<Lugar> CargarEventos()
        {
            var eventos = new List<Lugar>();
            foreach (JToken evento in Leer("eventos")["eventos"])
            {
                string nombre = ((string)evento["nombre"] ?? "").Trim();
                JToken coordenadas = evento["coordenadas"];
                if (nombre.Length == 0 || coordenadas == null || coordenadas.Type != JTokenType.Array) continue;
                eventos.Add(new Lugar { Nombre = nombre, Fecha = ((string)evento["fecha"] ?? "").Trim(), Coordenada = Coordenada(coordenadas) });
            }
            return eventos;
        }

        /// <summary>Encaje del modelo 3D del campus con las coordenadas geográficas.</summary>
        public static Calibracion CargarCalibracion()
        {
            JObject json = Leer("calibracion");
            var calibracion = new Calibracion { Origen = Coordenada(json["origin"]), Anclaje = Coordenada(json["anchor"]) };
            foreach (JToken punto in json["base"]) calibracion.Base.Add(new[] { (double)punto[0], (double)punto[1] });
            foreach (JToken punto in json["target"]) calibracion.Destino.Add(new[] { (double)punto[0], (double)punto[1] });
            foreach (JToken cara in json["triangles"]) calibracion.Triangulos.Add(new[] { (int)cara[0], (int)cara[1], (int)cara[2] });
            return calibracion;
        }

        static Coord Coordenada(JToken par) => new Coord((double)par[0], (double)par[1]);

        static JObject Leer(string nombre)
        {
            var archivo = Resources.Load<TextAsset>("Datos/" + nombre);
            if (archivo == null) throw new InvalidOperationException("Falta Assets/Esan/Resources/Datos/" + nombre + ".json");
            return JObject.Parse(archivo.text);
        }
    }
}
