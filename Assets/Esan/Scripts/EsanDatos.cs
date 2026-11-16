// Carga los datos del campus, los mismos archivos que usa el mapa web (Assets/Esan/Resources/Datos).
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Esan
{
    public sealed class Lugar
    {
        public string Nombre;
        public Coord Coordenada;
        public List<string> Aulas = new List<string>();
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

        /// <summary>Lugares con nombre y coordenadas: los únicos que pueden ser destino.</summary>
        public static List<Lugar> CargarLugares()
        {
            var lugares = new List<Lugar>();
            foreach (JToken elemento in Leer("nombres-elementos")["elementos"])
            {
                string nombre = ((string)elemento["sobrenombre"] ?? "").Trim();
                JToken coordenadas = elemento["coordenadas"];
                if (nombre.Length == 0 || coordenadas == null || coordenadas.Type != JTokenType.Array) continue;
                var lugar = new Lugar { Nombre = nombre, Coordenada = Coordenada(coordenadas) };
                if (elemento["aulas"] is JArray aulas)
                    foreach (JToken aula in aulas)
                    {
                        string texto = ((string)aula ?? "").Trim();
                        if (texto.Length > 0 && !lugar.Aulas.Contains(texto)) lugar.Aulas.Add(texto);
                    }
                // Un nombre repetido se queda con su último punto, como en el mapa web.
                lugares.RemoveAll(otro => otro.Nombre == nombre);
                lugares.Add(lugar);
            }
            return lugares;
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
