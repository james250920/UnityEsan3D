// Mapa 3D del campus dentro de la app, en lugar del mapa web: modelo del campus, cámara orbital, ruta y proyección
// de coordenadas a pantalla. La interfaz del mapa la construye EsanInterfaz. Va en su propia capa y con su propia
// cámara, que tapa la de Vuforia mientras está abierto.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Esan
{
    public sealed class EsanMapa : MonoBehaviour
    {
        const int Capa = 31;
        const float DistanciaMin = 15, DistanciaMax = 1800, InclinacionMin = 10, InclinacionMax = 85, InclinacionPlano = 89.9f;
        static readonly Color Fondo = new Color(.86f, .89f, .87f), ColorRuta = new Color(.682f, .133f, .224f);
        // El plano del suelo es casi todo blanco: se oscurece un poco para que destaquen caminos, áreas verdes y edificios.
        static readonly Color TonoSuelo = new Color(.84f, .86f, .83f), TonoEdificios = new Color(.62f, .6f, .57f), Ambiente = new Color(.5f, .52f, .52f);

        Calibracion calibracion;
        Camera camara;
        Light sol;
        LineRenderer linea;
        Bounds limites;
        Vector3 objetivo;
        float distancia, giro, inclinacion;
        bool plano;
        readonly List<Camera> pausadas = new List<Camera>();
        readonly List<Light> lucesApagadas = new List<Light>();
        UnityEngine.Rendering.AmbientMode modoAmbiente;
        Color colorAmbiente;
        readonly Dictionary<int, Vector2> toques = new Dictionary<int, Vector2>();
        readonly HashSet<int> toquesEnInterfaz = new HashSet<int>();

        float ultimoToque = -1;
        Vector2 lugarUltimoToque;

        public bool Abierto { get; private set; }
        /// <summary>El modelo ya está cargado: abrir el mapa es inmediato.</summary>
        public bool Cargado => camara != null;
        public bool Plano => plano;

        public void Preparar(Calibracion datos) => calibracion = datos;

        public void Abrir()
        {
            if (camara == null) { IEnumerator pasos = Cargar(null); while (pasos.MoveNext()) { } }
            pausadas.Clear();
            foreach (Camera otra in Camera.allCameras)
                if (otra != camara) { otra.enabled = false; pausadas.Add(otra); }
            // Las luces de la escena de la cámara se sumarían a la del mapa y lo dejarían blanco.
            lucesApagadas.Clear();
            foreach (Light luz in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (luz != sol && luz.enabled) { luz.enabled = false; lucesApagadas.Add(luz); }
            modoAmbiente = RenderSettings.ambientMode;
            colorAmbiente = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Ambiente;
            camara.enabled = sol.enabled = true;
            Abierto = true;
        }

        public void Cerrar()
        {
            if (!Abierto) return;
            camara.enabled = sol.enabled = false;
            foreach (Camera otra in pausadas) if (otra != null) otra.enabled = true;
            pausadas.Clear();
            foreach (Light luz in lucesApagadas) if (luz != null) luz.enabled = true;
            lucesApagadas.Clear();
            RenderSettings.ambientMode = modoAmbiente;
            RenderSettings.ambientLight = colorAmbiente;
            toques.Clear();
            Abierto = false;
        }

        // ---------- Vista ----------

        public void VistaGeneral()
        {
            objetivo = limites.center;
            giro = 140; inclinacion = 46;
            float aspecto = camara.pixelWidth / (float)Mathf.Max(1, camara.pixelHeight);
            distancia = limites.size.magnitude * Mathf.Max(1.4f, 1 / aspecto);
        }

        public void Enfocar(Coord punto)
        {
            objetivo = Superficie(punto);
            distancia = 160;
        }

        public void Acercar(float factor) => distancia = Mathf.Clamp(distancia * factor, DistanciaMin, DistanciaMax);

        public void CambiarPlano(bool activar) => plano = activar;

        /// <summary>Dibuja la ruta sobre el campus; null la borra.</summary>
        public void MostrarRuta(IReadOnlyList<Coord> puntos)
        {
            if (camara == null) return;
            linea.enabled = puntos != null && puntos.Count > 1;
            if (!linea.enabled) return;
            linea.positionCount = puntos.Count;
            for (int i = 0; i < puntos.Count; i++) linea.SetPosition(i, Superficie(puntos[i]) + Vector3.up * .6f);
        }

        /// <summary>Posición en pantalla (píxeles) de una coordenada, sobre la superficie del modelo.</summary>
        public bool Proyectar(Coord punto, out Vector2 pantalla)
        {
            Vector3 vista = camara.WorldToScreenPoint(Superficie(punto));
            pantalla = vista;
            return vista.z > 0;
        }

        // ---------- Construcción ----------

        /// <summary>Carga el modelo por partes, para poder mostrar el progreso. progreso va de 0 a 1.</summary>
        public IEnumerator Cargar(Action<float> progreso)
        {
            if (camara != null) yield break;
            GameObject modelo = Instantiate(Resources.Load<GameObject>("Modelos/campus"));
            modelo.name = "Campus";
            // Primero a la capa del mapa, para que la cámara de Vuforia no lo vea mientras carga.
            foreach (Transform parte in modelo.GetComponentsInChildren<Transform>(true)) parte.gameObject.layer = Capa;
            progreso?.Invoke(.15f);
            yield return null;
            bool primero = true;
            MeshFilter[] mallas = modelo.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mallas.Length; i++)
            {
                MeshFilter malla = mallas[i];
                if (i % 25 == 24) { progreso?.Invoke(.15f + .8f * i / mallas.Length); yield return null; }
                // Las colisiones solo sirven para saber a qué altura queda cada coordenada.
                malla.gameObject.AddComponent<MeshCollider>().sharedMesh = malla.sharedMesh;
                var dibujo = malla.GetComponent<Renderer>();
                foreach (Material material in dibujo.materials) Matizar(material);
                Bounds caja = dibujo.bounds;
                if (primero) { limites = caja; primero = false; } else limites.Encapsulate(caja);
            }
            Physics.SyncTransforms();

            camara = new GameObject("Cámara del mapa").AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Fondo;
            camara.cullingMask = 1 << Capa;
            camara.fieldOfView = 38;
            camara.nearClipPlane = 1;
            camara.farClipPlane = 5000;
            camara.depth = 100;
            camara.enabled = false;

            sol = new GameObject("Luz del mapa").AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.transform.rotation = Quaternion.Euler(55, 150, 0);
            sol.intensity = .9f;
            sol.shadows = LightShadows.None;
            sol.enabled = false;

            linea = new GameObject("Ruta del mapa").AddComponent<LineRenderer>();
            linea.gameObject.layer = Capa;
            linea.sharedMaterial = new Material(Resources.Load<Shader>("EsanGuia"));
            linea.startColor = linea.endColor = ColorRuta;
            linea.numCapVertices = linea.numCornerVertices = 4;
            linea.alignment = LineAlignment.View;
            linea.enabled = false;

            VistaGeneral();
            progreso?.Invoke(1);
        }

        // Sin brillo especular, que desde arriba blanquea techos y suelo, y con tonos que separan suelo y edificios.
        static void Matizar(Material material)
        {
            material.SetFloat("_Smoothness", .05f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_SpecularHighlights", 0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            string nombre = material.name;
            if (nombre.StartsWith("Mat_Terreno") || nombre.StartsWith("Mat_Vias")) material.SetColor("_BaseColor", TonoSuelo);
            else if (nombre.StartsWith("Mat_Edificios") && !nombre.StartsWith("Mat_Edificios_")) material.SetColor("_BaseColor", TonoEdificios);
        }

        // Altura del modelo en una coordenada: techo, camino o terreno.
        Vector3 Superficie(Coord punto)
        {
            double[] m = calibracion.AModelo(punto);
            // El modelo viene del glTF del mapa web; al importarlo, Unity invierte el eje X.
            var posicion = new Vector3(-(float)m[0], limites.max.y + 50, (float)m[1]);
            if (Physics.Raycast(posicion, Vector3.down, out RaycastHit golpe, limites.size.y + 100, 1 << Capa)) return golpe.point;
            posicion.y = limites.min.y;
            return posicion;
        }

        // ---------- Gestos ----------

        void LateUpdate()
        {
            if (!Abierto) return;
            if (Touchscreen.current != null) Toques();
            if (Mouse.current != null) Raton();
            float elevacion = plano ? InclinacionPlano : inclinacion;
            Quaternion orientacion = Quaternion.Euler(elevacion, giro, 0);
            camara.transform.SetPositionAndRotation(objetivo + orientacion * new Vector3(0, 0, -distancia), orientacion);
            linea.widthMultiplier = Mathf.Clamp(distancia * .006f, .8f, 6);
        }

        // Un dedo gira la vista; dos dedos la desplazan y la acercan con el pellizco.
        void Toques()
        {
            var activos = new List<KeyValuePair<int, Vector2>>();
            foreach (var toque in Touchscreen.current.touches)
            {
                int id = toque.touchId.ReadValue();
                var fase = toque.phase.ReadValue();
                if (fase == UnityEngine.InputSystem.TouchPhase.None || fase == UnityEngine.InputSystem.TouchPhase.Ended || fase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    toquesEnInterfaz.Remove(id);
                    continue;
                }
                Vector2 posicion = toque.position.ReadValue();
                if (fase == UnityEngine.InputSystem.TouchPhase.Began && !toques.ContainsKey(id))
                {
                    if (SobreInterfaz(posicion)) toquesEnInterfaz.Add(id);
                    else DobleToque(posicion);
                }
                if (!toquesEnInterfaz.Contains(id)) activos.Add(new KeyValuePair<int, Vector2>(id, posicion));
            }
            if (activos.Count == 1 && toques.TryGetValue(activos[0].Key, out Vector2 antes) && toques.Count == 1)
                Girar(activos[0].Value - antes);
            else if (activos.Count == 2 && toques.TryGetValue(activos[0].Key, out Vector2 a) && toques.TryGetValue(activos[1].Key, out Vector2 b))
            {
                float largoAntes = Vector2.Distance(a, b), largo = Vector2.Distance(activos[0].Value, activos[1].Value);
                if (largo > 5) Acercar(largoAntes / largo);
                Desplazar((activos[0].Value + activos[1].Value - a - b) * .5f);
            }
            toques.Clear();
            foreach (var par in activos) toques[par.Key] = par.Value;
        }

        // Dos toques seguidos acercan la vista, como en los mapas de siempre.
        void DobleToque(Vector2 posicion)
        {
            if (ultimoToque > 0 && Time.unscaledTime - ultimoToque < .3f && Vector2.Distance(posicion, lugarUltimoToque) < 80)
            {
                Acercar(.6f);
                ultimoToque = -1;
                return;
            }
            ultimoToque = Time.unscaledTime;
            lugarUltimoToque = posicion;
        }

        // En el editor: arrastrar gira, botón derecho desplaza, la rueda acerca.
        void Raton()
        {
            Mouse raton = Mouse.current;
            Vector2 movimiento = raton.delta.ReadValue();
            bool libre = !SobreInterfaz(raton.position.ReadValue());
            if (libre && raton.leftButton.isPressed) Girar(movimiento);
            if (libre && raton.rightButton.isPressed) Desplazar(movimiento);
            float rueda = raton.scroll.ReadValue().y;
            if (libre && rueda != 0) Acercar(rueda > 0 ? .88f : 1 / .88f);
        }

        void Girar(Vector2 movimiento)
        {
            float grados = 180f / Mathf.Max(1, Screen.height) * 2;
            giro += movimiento.x * grados;
            if (!plano) inclinacion = Mathf.Clamp(inclinacion - movimiento.y * grados, InclinacionMin, InclinacionMax);
        }

        void Desplazar(Vector2 movimiento)
        {
            Vector3 derecha = camara.transform.right, adelante = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up);
            if (adelante.sqrMagnitude < 1e-6f) adelante = Vector3.ProjectOnPlane(camara.transform.up, Vector3.up);
            float escala = distancia * Mathf.Tan(camara.fieldOfView * .5f * Mathf.Deg2Rad) * 2 / Mathf.Max(1, Screen.height);
            objetivo -= (derecha * movimiento.x + adelante.normalized * movimiento.y) * escala;
            objetivo.x = Mathf.Clamp(objetivo.x, limites.min.x, limites.max.x);
            objetivo.z = Mathf.Clamp(objetivo.z, limites.min.z, limites.max.z);
        }

        static readonly List<RaycastResult> golpes = new List<RaycastResult>();

        static bool SobreInterfaz(Vector2 posicion)
        {
            if (EventSystem.current == null) return false;
            golpes.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = posicion }, golpes);
            return golpes.Count > 0;
        }
    }
}
