// Guía azul sobre el suelo, la misma del mapa web: una franja que sigue el camino, con flechas, y que se
// desvanece hacia el final. Se dibuja en metros reales (X = este, Z = norte) y se coloca delante de la cámara
// según el GPS y la orientación del celular; no depende del seguimiento de Vuforia.
using System.Collections.Generic;
using UnityEngine;

namespace Esan
{
    public sealed class EsanGuia : MonoBehaviour
    {
        // Los mismos valores que guide en ar-config.js del mapa web.
        public const float AlturaOjos = 1.5f, AlcanceMetros = 40, SeparacionMetros = 1, FlechaCadaMetros = 4, UnionMetros = 6, RefrescoMetros = 2, SaltoMetros = 12;
        const float MedioAncho = 1, AnchoBorde = .12f, BordeMetros = 8, VisibleHastaMetros = 20;
        const int MaxFlechas = 10;
        // El proyecto trabaja en espacio de color lineal: los colores de la web (sRGB) se convierten.
        static readonly Color ColorGuia = new Color(.15f, .9f, .85f).linear, ColorFlecha = ((Color)new Color32(0xd6, 0xff, 0xfb, 242)).linear;

        EsanOrientacion orientacion;
        Camera camara;
        Transform mundo;
        Mesh malla;
        Coord origen;
        Vector3 posicion, destinoPosicion;
        bool hayPosicion;
        Ruta rutaDibujada;
        Coord anclaDibujada;
        List<Muestra> muestras = new List<Muestra>();

        public bool Visible { get; private set; }

        public void Preparar(EsanOrientacion orientacion, Coord origen)
        {
            this.orientacion = orientacion;
            this.origen = origen;
            mundo = new GameObject("ESAN Mundo geográfico").transform;
            var guia = new GameObject("Guía", typeof(MeshFilter), typeof(MeshRenderer));
            guia.transform.SetParent(mundo, false);
            malla = new Mesh { name = "Guía" };
            malla.MarkDynamic();
            guia.GetComponent<MeshFilter>().sharedMesh = malla;
            var dibujo = guia.GetComponent<MeshRenderer>();
            Shader sombreador = Resources.Load<Shader>("EsanGuia");
            if (sombreador == null) Debug.LogError("ESAN: falta Assets/Esan/Resources/EsanGuia.shader");
            else dibujo.sharedMaterial = new Material(sombreador);
            dibujo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dibujo.receiveShadows = false;
            guia.SetActive(false);
        }

        /// <summary>Dibuja la guía desde ancla (el usuario, o su punto sobre el camino) siguiendo la ruta.</summary>
        public void Mostrar(Ruta ruta, Coord ancla)
        {
            Metros local = Geo.AMetrosLocales(ancla, origen);
            destinoPosicion = new Vector3((float)local.Este, 0, (float)local.Norte);
            // Un salto grande del GPS no se suaviza: la cámara se quedaría lejos de la guía.
            if (!hayPosicion || Vector3.Distance(posicion, destinoPosicion) > SaltoMetros) { posicion = destinoPosicion; hayPosicion = true; }
            if (Visible && rutaDibujada == ruta && Geo.DistanciaMetros(anclaDibujada, ancla) < RefrescoMetros) return;
            muestras = Navegacion.Muestrear(Navegacion.TramoDeGuia(ruta.Puntos, ancla, origen, AlcanceMetros, UnionMetros), SeparacionMetros);
            Construir();
            rutaDibujada = ruta; anclaDibujada = ancla;
            Visible = true;
            mundo.GetChild(0).gameObject.SetActive(true);
        }

        public void Ocultar()
        {
            if (!Visible) return;
            Visible = false;
            rutaDibujada = null;
            muestras.Clear();
            mundo.GetChild(0).gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (mundo == null) return;
            if (camara == null)
            {
                camara = Camera.main;
                if (camara == null) return;
                mundo.SetParent(camara.transform, false);
            }
            posicion = Vector3.Lerp(posicion, destinoPosicion, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5));
            // El mundo geográfico cuelga de la cámara con la pose inversa de esta: así se ve como si la cámara
            // estuviera en el punto del GPS y mirando hacia donde dice el sensor.
            Quaternion inversa = Quaternion.Inverse(orientacion.Rotacion);
            mundo.localRotation = inversa;
            mundo.localPosition = -(inversa * posicion);
        }

        /// <summary>
        /// Si no se ve ningún tramo cercano de la guía, da hacia dónde girar: dirección en pantalla (x a la derecha,
        /// y hacia arriba) del punto de la guía a unos metros.
        /// </summary>
        public bool FueraDeVista(out Vector2 direccion)
        {
            direccion = Vector2.up;
            if (!Visible || camara == null || muestras.Count == 0) return false;
            int hasta = Mathf.Min(muestras.Count - 1, Mathf.RoundToInt(VisibleHastaMetros / SeparacionMetros));
            for (int i = Mathf.Min(hasta, Mathf.RoundToInt(2 / SeparacionMetros)); i <= hasta; i++)
            {
                Vector3 p = EnPantalla(muestras[i]);
                if (p.z > 0 && p.x > .08f && p.x < .92f && p.y > .2f && p.y < .8f) return false;
            }
            Vector3 punto = EnPantalla(muestras[Mathf.Min(muestras.Count - 1, Mathf.RoundToInt(BordeMetros / SeparacionMetros))]);
            direccion = new Vector2((punto.x - .5f) * camara.aspect, punto.y - .5f);
            if (punto.z < 0) direccion = -direccion;
            if (direccion.sqrMagnitude < 1e-6f) direccion = Vector2.down;
            direccion.Normalize();
            return true;
        }

        Vector3 EnPantalla(Muestra m) => camara.WorldToViewportPoint(mundo.TransformPoint(new Vector3((float)m.Este, -AlturaOjos, (float)m.Norte)));

        void Construir()
        {
            var vertices = new List<Vector3>();
            var colores = new List<Color>();
            var triangulos = new List<int>();
            if (muestras.Count >= 2)
            {
                Franja(vertices, colores, triangulos, -MedioAncho, MedioAncho, .3f);
                Franja(vertices, colores, triangulos, -MedioAncho - AnchoBorde, -MedioAncho, .95f);
                Franja(vertices, colores, triangulos, MedioAncho, MedioAncho + AnchoBorde, .95f);
            }
            int paso = Mathf.Max(1, Mathf.RoundToInt(FlechaCadaMetros / SeparacionMetros)), flechas = 0;
            for (int i = paso; i < muestras.Count - paso && flechas < MaxFlechas; i += paso, flechas++) Flecha(vertices, colores, triangulos, muestras[i]);
            malla.Clear();
            malla.SetVertices(vertices);
            malla.SetColors(colores);
            malla.SetTriangles(triangulos, 0);
            // La cámara está siempre dentro de la guía: no hay nada que recortar.
            malla.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
        }

        // Cinta entre dos desplazamientos laterales, que se desvanece hacia el final.
        void Franja(List<Vector3> vertices, List<Color> colores, List<int> triangulos, float interior, float exterior, float opacidad)
        {
            int baseIndice = vertices.Count;
            for (int i = 0; i < muestras.Count; i++)
            {
                Muestra m = muestras[i];
                float angulo = (float)m.Rumbo * Mathf.Deg2Rad, ladoEste = Mathf.Cos(angulo), ladoNorte = -Mathf.Sin(angulo);
                float alfa = Mathf.Min(1, (muestras.Count - 1 - i) / Mathf.Max(muestras.Count * .3f, 1e-3f)) * opacidad;
                var color = new Color(ColorGuia.r, ColorGuia.g, ColorGuia.b, alfa);
                vertices.Add(new Vector3((float)m.Este + ladoEste * interior, -AlturaOjos, (float)m.Norte + ladoNorte * interior));
                vertices.Add(new Vector3((float)m.Este + ladoEste * exterior, -AlturaOjos, (float)m.Norte + ladoNorte * exterior));
                colores.Add(color); colores.Add(color);
                if (i == 0) continue;
                int a = baseIndice + 2 * i;
                triangulos.Add(a - 2); triangulos.Add(a - 1); triangulos.Add(a);
                triangulos.Add(a - 1); triangulos.Add(a + 1); triangulos.Add(a);
            }
        }

        // Punta de flecha acostada en el suelo, apuntando en el sentido del camino.
        static void Flecha(List<Vector3> vertices, List<Color> colores, List<int> triangulos, Muestra m)
        {
            const float w = .62f, h = .5f, t = .42f;
            float angulo = (float)m.Rumbo * Mathf.Deg2Rad;
            Vector3 centro = new Vector3((float)m.Este, -AlturaOjos, (float)m.Norte), lado = new Vector3(Mathf.Cos(angulo), 0, -Mathf.Sin(angulo)), frente = new Vector3(Mathf.Sin(angulo), 0, Mathf.Cos(angulo));
            int n = vertices.Count;
            foreach (Vector2 p in new[] { new Vector2(-w, -h), new Vector2(0, h - t), new Vector2(w, -h), new Vector2(w, -h + t), new Vector2(0, h), new Vector2(-w, -h + t) })
            {
                vertices.Add(centro + lado * p.x + frente * p.y);
                colores.Add(ColorFlecha);
            }
            foreach (int i in new[] { 0, 1, 4, 0, 4, 5, 1, 2, 3, 1, 3, 4 }) triangulos.Add(n + i);
        }
    }
}
