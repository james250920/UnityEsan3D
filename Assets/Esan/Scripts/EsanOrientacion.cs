// Hacia dónde mira la cámara del celular, con la orientación completa que calcula Android y no con la brújula
// plana: esa solo acierta con el celular acostado y se descompone al ponerlo vertical.
// Se leen los sensores de Android directamente. Con giroscopio se usa el vector de rotación; sin él (por ejemplo
// el Galaxy A15), el vector de rotación geomagnético, que sale del acelerómetro y el magnetómetro y tiembla más.
using UnityEngine;

namespace Esan
{
    public sealed class EsanOrientacion : MonoBehaviour
    {
        const float InclinacionSupuesta = 10;  // Sin sensor, grados que se supone que la cámara mira hacia abajo.

        const int VectorDeRotacion = 11, VectorGeomagnetico = 20, RitmoDeJuego = 1;

        readonly object candado = new object();
        Quaternion lectura;
        bool hayLectura;
        Quaternion objetivo = Quaternion.identity;
        bool hayObjetivo, conGiroscopio;
        float proximoRegistro;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject gestor, hilo;
        Oyente oyente;
#endif

        /// <summary>Hay una lectura del sensor, no una suposición.</summary>
        public bool ConSensor { get; private set; }
        /// <summary>Rumbo de la mirada de la cámara: 0 = norte, 90 = este. Null si aún no se sabe.</summary>
        public double? Rumbo { get; private set; }
        /// <summary>Rotación de la cámara en el mundo geográfico de Unity: X = este, Y = arriba, Z = norte.</summary>
        public Quaternion Rotacion { get; private set; } = Quaternion.identity;
        /// <summary>Rumbo de reserva (simulado o deducido al caminar) cuando no hay sensor.</summary>
        public double? RumboDeReserva;
        /// <summary>Con un rumbo simulado se ignora el sensor.</summary>
        public bool ForzarReserva;

        void OnEnable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var actividad = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    gestor = actividad.Call<AndroidJavaObject>("getSystemService", "sensor");
                AndroidJavaObject sensor = gestor.Call<AndroidJavaObject>("getDefaultSensor", VectorDeRotacion);
                conGiroscopio = sensor != null;
                sensor ??= gestor.Call<AndroidJavaObject>("getDefaultSensor", VectorGeomagnetico);
                if (sensor == null) { Debug.LogWarning("ESAN: el celular no tiene sensor de orientación; la guía se orienta al caminar."); return; }
                oyente = new Oyente(this);
                // Las lecturas llegan a un hilo propio: en el hilo principal de Android lo saturan y la app deja de responder.
                hilo = new AndroidJavaObject("android.os.HandlerThread", "EsanSensores");
                hilo.Call("start");
                using (var bucle = hilo.Call<AndroidJavaObject>("getLooper"))
                using (var manejador = new AndroidJavaObject("android.os.Handler", bucle))
                    gestor.Call<bool>("registerListener", oyente, sensor, RitmoDeJuego, manejador);
                Debug.Log("ESAN: orientación con " + sensor.Call<string>("getName"));
            }
            catch (System.Exception error) { Debug.LogWarning("ESAN: no se pudo leer el sensor de orientación: " + error.Message); }
#endif
        }

        void OnDisable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (gestor != null && oyente != null) gestor.Call("unregisterListener", oyente);
            hilo?.Call<bool>("quitSafely");
            hilo?.Dispose();
            hilo = null;
            oyente = null;
#endif
            lock (candado) hayLectura = false;
        }

        // Android pausa la app al salir: se sueltan los sensores para no gastar batería.
        void OnApplicationPause(bool pausa)
        {
            if (pausa) OnDisable(); else if (isActiveAndEnabled) OnEnable();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android llama a estos métodos desde el hilo EsanSensores.
        sealed class Oyente : AndroidJavaProxy
        {
            readonly EsanOrientacion destino;
            public Oyente(EsanOrientacion destino) : base("android.hardware.SensorEventListener") { this.destino = destino; }

            [UnityEngine.Scripting.Preserve]
            public void onSensorChanged(AndroidJavaObject evento)
            {
                float[] v = evento.Get<float[]>("values");
                evento.Dispose();
                if (v == null || v.Length < 3) return;
                // El cuarto valor puede faltar: el cuaternión es unitario.
                float w = v.Length > 3 ? v[3] : Mathf.Sqrt(Mathf.Max(0, 1 - v[0] * v[0] - v[1] * v[1] - v[2] * v[2]));
                lock (destino.candado) { destino.lectura = new Quaternion(v[0], v[1], v[2], w); destino.hayLectura = true; }
            }

            [UnityEngine.Scripting.Preserve]
            public void onAccuracyChanged(AndroidJavaObject sensor, int precision) { sensor?.Dispose(); }
        }
#endif

        void Update()
        {
            Quaternion q;
            lock (candado) { q = lectura; ConSensor = hayLectura && !ForzarReserva; }
            if (ConSensor) DesdeSensor(q);
            else if (RumboDeReserva.HasValue)
            {
                Rumbo = RumboDeReserva;
                objetivo = Quaternion.Euler(InclinacionSupuesta, (float)RumboDeReserva.Value, 0);
                hayObjetivo = true;
            }
            else { Rumbo = null; hayObjetivo = false; }
            if (!hayObjetivo) return;
            // Suavizado: el sensor tiembla, sobre todo sin giroscopio, y la guía no debe vibrar.
            Rotacion = Quaternion.Slerp(Rotacion, objetivo, 1 - Mathf.Exp(-Time.unscaledDeltaTime * (conGiroscopio ? 14 : 6)));
            if (Debug.isDebugBuild && Time.unscaledTime > proximoRegistro)
            {
                proximoRegistro = Time.unscaledTime + 3;
                Vector3 mirada = Rotacion * Vector3.forward;
                Debug.Log($"ESAN: orientación {(ConSensor ? "del sensor" : "de reserva")} · rumbo {Rumbo:0}° · elevación {Mathf.Asin(Mathf.Clamp(mirada.y, -1, 1)) * Mathf.Rad2Deg:0}°");
            }
        }

        // q lleva del celular (X a la derecha de la pantalla, Y hacia su borde superior, Z saliendo de la pantalla)
        // al mundo de Android (X este, Y norte, Z arriba). La cámara trasera mira por -Z.
        void DesdeSensor(Quaternion q)
        {
            Vector3 mirada = q * Vector3.back, arriba = q * Vector3.up;
            objetivo = Quaternion.LookRotation(new Vector3(mirada.x, mirada.z, mirada.y), new Vector3(arriba.x, arriba.z, arriba.y));
            hayObjetivo = true;
            // Mirando casi en vertical, el rumbo sale del borde superior del celular.
            Vector2 plano = Mathf.Abs(mirada.z) < .92f ? new Vector2(mirada.x, mirada.y) : (mirada.z < 0 ? 1 : -1) * new Vector2(arriba.x, arriba.y);
            Rumbo = (Mathf.Atan2(plano.x, plano.y) * Mathf.Rad2Deg + 360) % 360;
        }
    }
}
