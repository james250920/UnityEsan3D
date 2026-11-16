// Grafo peatonal del campus y ruta más corta con A*. Portado de path-graph.js del mapa web.
using System;
using System.Collections.Generic;

namespace Esan
{
    public sealed class Ruta
    {
        public List<string> Ids;      // Puntos del grafo, del origen al destino.
        public List<Coord> Puntos;    // Sus coordenadas.
        public double Metros;
        public int NodosVisitados;
    }

    public sealed class GrafoCaminos
    {
        readonly List<string> ids = new List<string>();
        readonly Dictionary<string, Coord> nodos = new Dictionary<string, Coord>();
        readonly Dictionary<string, List<(string id, double metros)>> vecinos = new Dictionary<string, List<(string, double)>>();
        readonly HashSet<string> uniones = new HashSet<string>();

        public int Nodos => ids.Count;
        public int Uniones => uniones.Count;
        public Coord Coordenada(string id) => nodos[id];

        public void AgregarNodo(string id, Coord coordenada)
        {
            if (string.IsNullOrWhiteSpace(id) || nodos.ContainsKey(id)) throw new ArgumentException("Cada punto necesita un identificador único.");
            ids.Add(id);
            nodos[id] = coordenada;
            vecinos[id] = new List<(string, double)>();
        }

        public void AgregarUnion(string desde, string hasta)
        {
            string clave = string.CompareOrdinal(desde, hasta) < 0 ? desde + "|" + hasta : hasta + "|" + desde;
            if (!nodos.ContainsKey(desde) || !nodos.ContainsKey(hasta) || desde == hasta || !uniones.Add(clave))
                throw new ArgumentException("Hay una unión duplicada o con extremos inválidos.");
            double metros = Geo.DistanciaMetros(nodos[desde], nodos[hasta]);
            vecinos[desde].Add((hasta, metros));
            vecinos[hasta].Add((desde, metros));
        }

        public string NodoMasCercano(Coord c)
        {
            string mejor = null;
            double distancia = double.PositiveInfinity;
            foreach (string id in ids)
            {
                double d = Geo.DistanciaMetros(c, nodos[id]);
                if (d < distancia) { distancia = d; mejor = id; }
            }
            return mejor;
        }

        /// <summary>Ruta entre los puntos del grafo más cercanos al origen y al destino. Null si no están conectados.</summary>
        public Ruta RutaMasCorta(Coord origen, Coord destino)
        {
            if (ids.Count == 0) return null;
            string inicio = NodoMasCercano(origen), fin = NodoMasCercano(destino);
            Coord meta = nodos[fin];
            var g = new Dictionary<string, double> { [inicio] = 0 };
            var anterior = new Dictionary<string, string>();
            var visitados = new HashSet<string>();
            var cola = new Monticulo();
            // La distancia en línea recta nunca sobreestima: la ruta es óptima.
            cola.Agregar(Geo.DistanciaMetros(nodos[inicio], meta), inicio);
            while (cola.Cantidad > 0)
            {
                string actual = cola.Sacar();
                if (!visitados.Add(actual)) continue;
                if (actual == fin) break;
                double gActual = g[actual];
                foreach (var (id, metros) in vecinos[actual])
                {
                    if (visitados.Contains(id)) continue;
                    double tentativa = gActual + metros;
                    if (g.TryGetValue(id, out double conocida) && tentativa >= conocida) continue;
                    g[id] = tentativa;
                    anterior[id] = actual;
                    cola.Agregar(tentativa + Geo.DistanciaMetros(nodos[id], meta), id);
                }
            }
            if (!visitados.Contains(fin)) return null;
            var ruta = new List<string> { fin };
            while (ruta[0] != inicio) ruta.Insert(0, anterior[ruta[0]]);
            return new Ruta { Ids = ruta, Puntos = ruta.ConvertAll(id => nodos[id]), Metros = g[fin], NodosVisitados = visitados.Count };
        }

        // Cola de prioridad mínima, igual que la del mapa web para que los empates se resuelvan igual.
        sealed class Monticulo
        {
            readonly List<(double clave, string valor)> datos = new List<(double, string)>();
            public int Cantidad => datos.Count;
            public void Agregar(double clave, string valor)
            {
                datos.Add((clave, valor));
                int i = datos.Count - 1;
                while (i > 0)
                {
                    int padre = (i - 1) >> 1;
                    if (datos[padre].clave <= datos[i].clave) break;
                    (datos[padre], datos[i]) = (datos[i], datos[padre]);
                    i = padre;
                }
            }
            public string Sacar()
            {
                var cima = datos[0];
                var ultimo = datos[datos.Count - 1];
                datos.RemoveAt(datos.Count - 1);
                if (datos.Count > 0)
                {
                    datos[0] = ultimo;
                    int i = 0, n = datos.Count;
                    while (true)
                    {
                        int menor = i, izquierda = 2 * i + 1, derecha = 2 * i + 2;
                        if (izquierda < n && datos[izquierda].clave < datos[menor].clave) menor = izquierda;
                        if (derecha < n && datos[derecha].clave < datos[menor].clave) menor = derecha;
                        if (menor == i) break;
                        (datos[menor], datos[i]) = (datos[i], datos[menor]);
                        i = menor;
                    }
                }
                return cima.valor;
            }
        }
    }
}
