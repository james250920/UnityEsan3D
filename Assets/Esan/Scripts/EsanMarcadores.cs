// Prueba de marcadores: al reconocer una de las fotos de la pileta, muestra a Chuwi sobre ella.
// Se crea solo al iniciar la escena; solo necesita que exista la ARCamera de Vuforia.
using System;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace Esan
{
    public sealed class EsanMarcadores : MonoBehaviour
    {
        sealed class Marcador
        {
            public string Nombre, Imagen;
            public float AnchoMetros;          // Ancho real de lo que abarca la foto, a la distancia de la pileta.
            public float Altura;               // Altura del modelo, en anchos de la foto.
            public Vector3 Desplazamiento;     // Desde el centro de la foto, en anchos: derecha, arriba, hacia la cámara.
            public Color Color;
        }

        // Los mismos valores que models.markers en ar-config.js del mapa web.
        static readonly Marcador[] Lista =
        {
            new Marcador { Nombre = "Pileta, de lejos", Imagen = "M1", AnchoMetros = 4f, Altura = .45f, Desplazamiento = new Vector3(0, -.23f, 0), Color = new Color32(0xc9, 0xa0, 0x6a, 255) },
            new Marcador { Nombre = "Pileta, de cerca", Imagen = "M2", AnchoMetros = 4f, Altura = .45f, Desplazamiento = new Vector3(0, -.09f, 0), Color = new Color32(0x12, 0xa8, 0x9d, 255) }
        };

        // La interfaz (EsanInterfaz) lee este estado y avisa cuando se reconoce un marcador.
        public static bool VuforiaListo { get; private set; }
        public static bool VuforiaFallo { get; private set; }
        public static bool SinPermisoCamara { get; private set; }
        public static string Estado { get; private set; } = "Iniciando Vuforia…";
        public static event Action<string> Reconocido;

        static string estado { set { Estado = value; Debug.Log("ESAN: " + value); } }
        readonly HashSet<string> vistos = new HashSet<string>();
        bool creados;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (FindAnyObjectByType<EsanMarcadores>() == null) new GameObject("ESAN Marcadores").AddComponent<EsanMarcadores>();
        }

        void Start()
        {
            VuforiaListo = VuforiaFallo = SinPermisoCamara = false;
            if (VuforiaBehaviour.Instance == null)
            {
                VuforiaFallo = true;
                estado = "Falta la ARCamera de Vuforia en la escena.";
                return;
            }
            VuforiaApplication.Instance.OnVuforiaInitialized += AlInicializar;
            VuforiaApplication.Instance.OnVuforiaStarted += CrearMarcadores;
            if (VuforiaApplication.Instance.IsRunning) CrearMarcadores();
        }

        void OnDestroy()
        {
            VuforiaApplication.Instance.OnVuforiaInitialized -= AlInicializar;
            VuforiaApplication.Instance.OnVuforiaStarted -= CrearMarcadores;
        }

        void AlInicializar(VuforiaInitError error)
        {
            if (error == VuforiaInitError.NONE) return;
            VuforiaFallo = true;
            SinPermisoCamara = error == VuforiaInitError.PERMISSION_ERROR;
            estado = "Vuforia no pudo iniciar: " + error;
        }

        void CrearMarcadores()
        {
            if (creados) return;
            creados = true;
            var modelo = Resources.Load<GameObject>("Modelos/chuwi");
            int listos = 0;
            foreach (Marcador marcador in Lista)
            {
                var archivo = Resources.Load<TextAsset>("Marcadores/" + marcador.Imagen);
                var foto = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (archivo == null || !foto.LoadImage(archivo.bytes))
                {
                    Debug.LogWarning("No se pudo leer la foto del marcador " + marcador.Imagen);
                    continue;
                }
                ImageTargetBehaviour objetivo = VuforiaBehaviour.Instance.ObserverFactory.CreateImageTarget(foto, marcador.AnchoMetros, marcador.Imagen);
                if (objetivo == null)
                {
                    Debug.LogWarning("Vuforia rechazó la foto del marcador " + marcador.Imagen);
                    continue;
                }
                objetivo.gameObject.name = "Marcador " + marcador.Nombre;
                GameObject chuwi = modelo != null ? CrearModelo(modelo, marcador.Color) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                float ancho = marcador.AnchoMetros;
                // La foto queda acostada en el plano XZ del objetivo: +Z es su borde superior y +Y sale hacia la cámara.
                chuwi.transform.SetParent(objetivo.transform, false);
                chuwi.transform.localPosition = new Vector3(marcador.Desplazamiento.x, marcador.Desplazamiento.z, marcador.Desplazamiento.y) * ancho;
                chuwi.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                chuwi.transform.localScale = Vector3.one * (marcador.Altura * ancho);
                chuwi.SetActive(false);
                objetivo.OnTargetStatusChanged += (observador, situacion) =>
                {
                    bool visible = situacion.Status == Status.TRACKED || situacion.Status == Status.EXTENDED_TRACKED;
                    chuwi.SetActive(visible);
                    if (!visible) vistos.Remove(marcador.Nombre);
                    else if (vistos.Add(marcador.Nombre)) Reconocido?.Invoke(marcador.Nombre);
                };
                listos++;
            }
            VuforiaListo = true;
            estado = $"Vuforia activo · {listos} de {Lista.Length} marcadores" + (modelo == null ? " · falta el modelo de Chuwi" : "");
        }

        // Deja el modelo de pie sobre el origen, centrado y con altura 1.
        static GameObject CrearModelo(GameObject original, Color color)
        {
            GameObject copia = Instantiate(original);
            var limites = new Bounds();
            bool primero = true;
            foreach (Renderer malla in copia.GetComponentsInChildren<Renderer>())
            {
                if (primero) limites = malla.bounds; else limites.Encapsulate(malla.bounds);
                primero = false;
                if (malla.sharedMaterial != null) malla.material.color = color;
            }
            float alto = limites.size.y > 0 ? limites.size.y : 1;
            var raiz = new GameObject("Chuwi");
            copia.transform.SetParent(raiz.transform, false);
            copia.transform.localScale = copia.transform.localScale / alto;
            copia.transform.localPosition = (copia.transform.localPosition - new Vector3(limites.center.x, limites.min.y, limites.center.z)) / alto;
            return raiz;
        }
    }
}
