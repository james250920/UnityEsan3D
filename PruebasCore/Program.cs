// Compara el código C# de Assets/Esan/Scripts/Core con los resultados del mapa web (esperado.json).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Esan;

static class Pruebas
{
    static int fallos, comprobaciones;

    static void Igual(double real, double esperado, string que, double tolerancia = 1e-6)
    {
        comprobaciones++;
        if (Math.Abs(real - esperado) <= tolerancia) return;
        if (fallos++ < 20) Console.WriteLine($"FALLO {que}: {real} ≠ {esperado}");
    }
    static void Mismo<T>(T real, T esperado, string que)
    {
        comprobaciones++;
        if (EqualityComparer<T>.Default.Equals(real, esperado)) return;
        if (fallos++ < 20) Console.WriteLine($"FALLO {que}: {real} ≠ {esperado}");
    }
    static Coord C(JsonElement e) => new Coord(e[0].GetDouble(), e[1].GetDouble());

    static int Main(string[] args)
    {
        string carpeta = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(carpeta, "esperado.json"))) carpeta = Path.GetDirectoryName(carpeta) ?? throw new FileNotFoundException("esperado.json");
        string datos = Path.Combine(carpeta, "..", "Assets", "Esan", "Resources", "Datos");
        using var grafoJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(datos, "grafo-caminos-esan.json")));
        using var perimetroJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(datos, "esan-boundary.json")));
        using var esperado = JsonDocument.Parse(File.ReadAllText(Path.Combine(carpeta, "esperado.json")));

        var grafo = new GrafoCaminos();
        foreach (var nodo in grafoJson.RootElement.GetProperty("nodos").EnumerateArray()) grafo.AgregarNodo(nodo.GetProperty("id").GetString(), C(nodo.GetProperty("coordenadas")));
        foreach (var arista in grafoJson.RootElement.GetProperty("aristas").EnumerateArray()) grafo.AgregarUnion(arista.GetProperty("desde").GetString(), arista.GetProperty("hasta").GetString());
        var perimetro = perimetroJson.RootElement.GetProperty("features")[0].GetProperty("geometry").GetProperty("coordinates").EnumerateArray().Select(C).ToList();
        Coord origen = C(esperado.RootElement.GetProperty("origin"));

        foreach (var caso in esperado.RootElement.GetProperty("cases").EnumerateArray())
        {
            Ruta ruta = grafo.RutaMasCorta(C(caso.GetProperty("from")), C(caso.GetProperty("to")));
            Mismo(string.Join(",", ruta.Ids), string.Join(",", caso.GetProperty("ids").EnumerateArray().Select(id => id.GetString())), "puntos de la ruta");
            Igual(ruta.Metros, caso.GetProperty("distance").GetDouble(), "distancia de la ruta");
            Mismo(ruta.NodosVisitados, caso.GetProperty("visited").GetInt32(), "nodos visitados");
            foreach (var u in caso.GetProperty("users").EnumerateArray())
            {
                Coord usuario = C(u.GetProperty("user"));
                var p = u.GetProperty("progress");
                Progreso progreso = Navegacion.ProgresoEnRuta(ruta.Puntos, usuario, 12, 8);
                Igual(progreso.FueraDeRutaMetros, p.GetProperty("offRouteMeters").GetDouble(), "fuera de ruta");
                Igual(progreso.RecorridoMetros, p.GetProperty("alongMeters").GetDouble(), "recorrido");
                Igual(progreso.RestanteMetros, p.GetProperty("remainingMeters").GetDouble(), "restante");
                Mismo(progreso.Volviendo, p.GetProperty("rejoin").GetBoolean(), "volviendo");
                Igual(progreso.Proyeccion.Lon, p.GetProperty("projection")[0].GetDouble(), "proyección lon", 1e-10);
                Igual(progreso.Proyeccion.Lat, p.GetProperty("projection")[1].GetDouble(), "proyección lat", 1e-10);
                Igual(progreso.SiguienteMetros, p.GetProperty("waypointMeters").GetDouble(), "siguiente metros");
                Igual(Geo.AnguloConSigno(progreso.SiguienteRumbo - p.GetProperty("waypointBearing").GetDouble()), 0, "siguiente rumbo", 1e-4);

                var tramo = Navegacion.TramoDeGuia(ruta.Puntos, usuario, origen, 40, 6);
                var tramoEsperado = u.GetProperty("path");
                Mismo(tramo.Count, tramoEsperado.GetArrayLength(), "puntos del tramo");
                for (int i = 0; i < Math.Min(tramo.Count, tramoEsperado.GetArrayLength()); i++)
                {
                    Igual(tramo[i].Este, tramoEsperado[i][0].GetDouble(), "tramo este");
                    Igual(tramo[i].Norte, tramoEsperado[i][1].GetDouble(), "tramo norte");
                }
                var muestras = Navegacion.Muestrear(tramo, 1);
                var muestrasEsperadas = u.GetProperty("samples");
                Mismo(muestras.Count, muestrasEsperadas.GetArrayLength(), "cantidad de muestras");
                for (int i = 0; i < Math.Min(muestras.Count, muestrasEsperadas.GetArrayLength()); i++)
                {
                    Igual(muestras[i].Este, muestrasEsperadas[i].GetProperty("east").GetDouble(), "muestra este");
                    Igual(muestras[i].Norte, muestrasEsperadas[i].GetProperty("north").GetDouble(), "muestra norte");
                    Igual(Geo.AnguloConSigno(muestras[i].Rumbo - muestrasEsperadas[i].GetProperty("bearing").GetDouble()), 0, "muestra rumbo", 1e-4);
                }
                Mismo(Geo.DentroDelPerimetro(usuario, perimetro), u.GetProperty("inside").GetBoolean(), "dentro del campus");
                Igual(Geo.DistanciaAlPerimetro(usuario, perimetro), u.GetProperty("toBoundary").GetDouble(), "distancia al perímetro");
                Metros local = Geo.AMetrosLocales(usuario, origen);
                Igual(local.Este, u.GetProperty("local")[0].GetDouble(), "local este");
                Igual(local.Norte, u.GetProperty("local")[1].GetDouble(), "local norte");
                Igual(Geo.DistanciaMetros(Geo.DesdeMetrosLocales(local, origen), usuario), 0, "ida y vuelta");
                Igual(Geo.AnguloConSigno(Geo.Rumbo(origen, usuario) - u.GetProperty("bearing").GetDouble()), 0, "rumbo", 1e-6);
                Igual(Geo.DistanciaMetros(origen, usuario), u.GetProperty("direct").GetDouble(), "distancia directa");
            }
        }
        foreach (var t in esperado.RootElement.GetProperty("texts").EnumerateArray())
        {
            double n = t.GetProperty("n").GetDouble();
            Mismo(Navegacion.TextoDistancia(n), t.GetProperty("distance").GetString(), $"texto de distancia de {n}");
            Mismo(Navegacion.TiempoAPie(Math.Abs(n)), t.GetProperty("time").GetString(), $"tiempo a pie de {n}");
            Mismo(Navegacion.IndicacionDeGiro(n), t.GetProperty("turn").GetString(), $"giro de {n}");
            Igual(Geo.AnguloConSigno(n), t.GetProperty("signed").GetDouble(), $"ángulo con signo de {n}");
            Igual(Navegacion.UmbralDeLlegada(n, 15, 30), t.GetProperty("arrival").GetDouble(), $"umbral de llegada de {n}");
        }
        var ubicacion = new Ubicacion(perimetro);
        var caminar = new RumboAlCaminar();
        foreach (var paso in esperado.RootElement.GetProperty("location").EnumerateArray())
        {
            double ahora = paso.GetProperty("now").GetDouble();
            if (paso.GetProperty("op").GetString() == "tick") ubicacion.Tic(ahora);
            else
            {
                Coord c = C(paso.GetProperty("coordinate"));
                double precision = paso.GetProperty("accuracy").GetDouble();
                ubicacion.Lectura(c, precision, paso.GetProperty("timestamp").GetDouble(), ahora);
                caminar.Actualizar(c, precision);
                var rumbo = paso.GetProperty("course");
                Mismo(caminar.Rumbo.HasValue, rumbo.ValueKind != JsonValueKind.Null, "hay rumbo al caminar");
                if (caminar.Rumbo.HasValue && rumbo.ValueKind != JsonValueKind.Null) Igual(Geo.AnguloConSigno(caminar.Rumbo.Value - rumbo.GetDouble()), 0, "rumbo al caminar");
            }
            string guia = ubicacion.Guia == Guia.Ok ? "ok" : ubicacion.Guia == Guia.FueraDelCampus ? "left" : "unconfirmed";
            string zona = ubicacion.Zona == Zona.Dentro ? "inside" : ubicacion.Zona == Zona.Fuera ? "outside" : "unknown";
            Mismo(guia, paso.GetProperty("guidance").GetString(), "guía de ubicación");
            Mismo(zona, paso.GetProperty("zone").GetString(), "zona");
        }
        Console.WriteLine(fallos == 0 ? $"OK: {comprobaciones} comprobaciones coinciden con el mapa web ({grafo.Nodos} puntos, {grafo.Uniones} uniones)." : $"{fallos} FALLOS de {comprobaciones} comprobaciones.");
        return fallos == 0 ? 0 : 1;
    }
}
