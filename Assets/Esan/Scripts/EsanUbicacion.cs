// GPS y brújula del celular. Alimenta el estado de ubicación que usa la interfaz.
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Esan
{
    public sealed class EsanUbicacion : MonoBehaviour
    {
        public Ubicacion Estado { get; private set; }
        public bool PermisoNegado { get; private set; }
        public bool Apagada { get; private set; }  // La ubicación del celular está desactivada.
        public bool Simulada { get; private set; }
        public event Action Cambio;

        readonly RumboAlCaminar caminar = new RumboAlCaminar();
        double ultimaMarca = -1, rumboSimulado = double.NaN;
        Coord puntoSimulado;
        float proximaSimulada;

        static double AhoraMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>Rumbo simulado, o el deducido al caminar. Sirve de reserva si el celular no tiene sensor de orientación.</summary>
        public double? RumboDeReserva => Simulada ? (double.IsNaN(rumboSimulado) ? (double?)null : rumboSimulado) : caminar.Rumbo;
        /// <summary>La simulación fija el rumbo: se ignora el sensor.</summary>
        public bool RumboSimulado => Simulada && !double.IsNaN(rumboSimulado);

        public void Preparar(Ubicacion estado) { Estado = estado; }

        IEnumerator Start()
        {
            LeerSimulacion();
            if (Simulada) yield break;
            // La cámara pide su permiso primero: dos avisos a la vez se pisan.
            float limite = Time.realtimeSinceStartup + 10;
            while (!EsanMarcadores.VuforiaListo && !EsanMarcadores.VuforiaFallo && Time.realtimeSinceStartup < limite) yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                bool respondido = false;
                var respuestas = new PermissionCallbacks();
                respuestas.PermissionGranted += _ => respondido = true;
                respuestas.PermissionDenied += _ => { respondido = true; PermisoNegado = true; };
                Permission.RequestUserPermission(Permission.FineLocation, respuestas);
                while (!respondido) yield return null;
            }
#endif
            if (PermisoNegado)
            {
                Cambio?.Invoke();
                yield break;
            }
            // Si la ubicación del celular está apagada, se espera a que la enciendan.
            while (!Input.location.isEnabledByUser)
            {
                if (!Apagada) { Apagada = true; Cambio?.Invoke(); }
                yield return new WaitForSecondsRealtime(2);
            }
            Apagada = false;
            Input.location.Start(5f, 1f);
        }

        void Update()
        {
            if (Estado == null) return;
            if (Simulada)
            {
                if (Time.unscaledTime < proximaSimulada) return;
                proximaSimulada = Time.unscaledTime + 1;
                Estado.Lectura(puntoSimulado, 8, AhoraMs, AhoraMs);
                Cambio?.Invoke();
                return;
            }
            if (Input.location.status != LocationServiceStatus.Running) return;
            LocationInfo dato = Input.location.lastData;
            if (dato.timestamp == ultimaMarca) return;
            ultimaMarca = dato.timestamp;
            var punto = new Coord(dato.longitude, dato.latitude);
            Estado.Lectura(punto, dato.horizontalAccuracy, dato.timestamp * 1000, AhoraMs);
            caminar.Actualizar(punto, dato.horizontalAccuracy);
            Cambio?.Invoke();
        }

        void OnDestroy()
        {
            if (!Simulada) Input.location.Stop();
        }

        // Solo en compilaciones de desarrollo: adb shell am start -n <paquete>/<actividad> -e esan_sim "longitud,latitud,rumbo"
        void LeerSimulacion()
        {
            if (!Debug.isDebugBuild) return;
            string texto = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var actividad = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intencion = actividad.Call<AndroidJavaObject>("getIntent"))
                    texto = intencion.Call<string>("getStringExtra", "esan_sim");
            }
            catch (Exception error) { Debug.LogWarning("ESAN: no se pudo leer la simulación: " + error.Message); }
#endif
            if (string.IsNullOrEmpty(texto)) return;
            string[] partes = texto.Split(',');
            if (partes.Length < 2 || !Numero(partes[0], out double lon) || !Numero(partes[1], out double lat)) return;
            puntoSimulado = new Coord(lon, lat);
            if (partes.Length > 2 && Numero(partes[2], out double rumbo)) rumboSimulado = rumbo;
            Simulada = true;
            Debug.Log("ESAN: ubicación simulada en " + puntoSimulado);
        }

        static bool Numero(string texto, out double valor) => double.TryParse(texto.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }
}
