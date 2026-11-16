// Interfaz de la experiencia inmersiva, la misma del mapa web: barra superior, estado, tarjeta de destino,
// indicación de giro y lista de lugares. Se construye por código al iniciar la escena, sin prefabs.
// Las medidas van en las mismas unidades que los píxeles CSS de la web (pantalla de 390 de ancho).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Esan
{
    public sealed class EsanInterfaz : MonoBehaviour
    {
        const string MapaWeb = "https://esan3d.menfroyt-dev.com/", DominioMapa = "esan3d.menfroyt-dev.com";
        // Los mismos valores que navigation en ar-config.js del mapa web.
        const double AnticipoMetros = 12, RecalcularMetros = 25, AjusteMinMetros = 8, AjusteMaxMetros = 15, LlegadaMinMetros = 15, LlegadaMaxMetros = 30;

        static readonly Color Tinta = Hex("253d32"), Apagado = Hex("617269"), Rojo = Hex("ae2239"), Verde = Hex("237044"), Ambar = Hex("7a5a00"), Granate = Hex("a51d35");
        static readonly Color Panel = new Color(1, 1, 1, .94f), VerdeClaro = Hex("e3f3ea"), VerdeOscuro = Hex("14492d"), Gris = Hex("eef1ee"), Rosa = Hex("f9e8ec");

        EsanUbicacion gps;
        EsanOrientacion orientacion;
        EsanGuia guia;
        GrafoCaminos grafo;
        List<Lugar> lugares;
        Lugar destino;
        Ruta ruta;
        Progreso progreso;
        bool llegada;
        float proximoRefresco, ocultarMensaje;
        Rect areaSegura;

        Font fuente;
        RectTransform raiz, flecha;
        Image chipZona;
        Text textoZona, textoModo, mensaje, etiqueta, nombre, linea, indicacion, botonElegir;
        GameObject cajaMensaje, cajaIndicacion, selector;
        RectTransform borde, flechaBorde;
        RectTransform listaLugares;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (FindAnyObjectByType<EsanInterfaz>() == null) new GameObject("ESAN Interfaz").AddComponent<EsanInterfaz>();
        }

        void Awake()
        {
            grafo = EsanDatos.CargarGrafo();
            lugares = EsanDatos.CargarLugares();
            gps = gameObject.AddComponent<EsanUbicacion>();
            gps.Preparar(new Ubicacion(EsanDatos.CargarPerimetro()));
            gps.Cambio += Actualizar;
            orientacion = gameObject.AddComponent<EsanOrientacion>();
            guia = gameObject.AddComponent<EsanGuia>();
            guia.Preparar(orientacion, lugares.Count > 0 ? lugares[0].Coordenada : new Coord(-76.9612, -12.1050));
            // La interfaz y el sensor de orientación están pensados para el celular en vertical.
            Screen.orientation = ScreenOrientation.Portrait;
            EsanMarcadores.Reconocido += AlReconocer;
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Construir();
            Actualizar();
        }

        void OnDestroy()
        {
            EsanMarcadores.Reconocido -= AlReconocer;
        }

        void AlReconocer(string marcador) => Mensaje("Marcador reconocido: " + marcador);

        void Update()
        {
            if (Screen.safeArea != areaSegura) AjustarAreaSegura();
            if (cajaMensaje.activeSelf && ocultarMensaje > 0 && Time.unscaledTime > ocultarMensaje) cajaMensaje.SetActive(false);
            // Botón Atrás de Android.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (selector.activeSelf) selector.SetActive(false); else Application.Quit();
            }
            orientacion.RumboDeReserva = gps.RumboDeReserva;
            orientacion.ForzarReserva = gps.RumboSimulado;
            ActualizarBorde();
            if (Time.unscaledTime < proximoRefresco) return;
            gps.Estado.Tic(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Actualizar();
        }

        // ---------- Estado ----------

        void Actualizar()
        {
            proximoRefresco = Time.unscaledTime + .25f;
            Ubicacion estado = gps.Estado;
            Guia situacion = estado.Guia;
            textoZona.text = situacion == Guia.Ok ? "Dentro del campus" : situacion == Guia.FueraDelCampus ? "Fuera del campus" : "Ubicación por confirmar";
            chipZona.color = situacion == Guia.Ok ? Verde : situacion == Guia.FueraDelCampus ? Granate : Ambar;
            double? rumbo = orientacion.Rumbo;
            textoModo.text = EsanMarcadores.VuforiaFallo ? "Sin cámara" : !EsanMarcadores.VuforiaListo ? "Abriendo cámara…" : rumbo.HasValue ? "RA activa" : "Orientando la guía";

            if (situacion == Guia.Ok && destino != null)
            {
                Coord usuario = estado.Coordenada;
                double ajuste = Math.Min(AjusteMaxMetros, Math.Max(AjusteMinMetros, estado.Precision));
                if (ruta == null) ruta = grafo.RutaMasCorta(usuario, destino.Coordenada);
                progreso = ruta == null ? null : Navegacion.ProgresoEnRuta(ruta.Puntos, usuario, AnticipoMetros, ajuste);
                if (progreso != null && progreso.FueraDeRutaMetros > RecalcularMetros)
                {
                    ruta = grafo.RutaMasCorta(usuario, destino.Coordenada);
                    progreso = ruta == null ? null : Navegacion.ProgresoEnRuta(ruta.Puntos, usuario, AnticipoMetros, ajuste);
                }
                double directa = Geo.DistanciaMetros(usuario, destino.Coordenada), umbral = Navegacion.UmbralDeLlegada(estado.Precision, LlegadaMinMetros, LlegadaMaxMetros);
                if (!llegada && directa <= umbral) llegada = true;
                else if (llegada && directa > umbral * 1.5) llegada = false;
            }
            else progreso = null;

            etiqueta.gameObject.SetActive(!llegada);
            nombre.text = llegada ? "¡Llegaste a tu destino!" : destino != null ? destino.Nombre : "Elige un destino";
            botonElegir.text = llegada ? "Explorar otro lugar" : destino != null ? "Cambiar destino" : "Elegir destino";
            if (gps.PermisoNegado) linea.text = "Permite la ubicación en los ajustes de la app";
            else if (gps.Apagada) linea.text = "Activa la ubicación del celular";
            else if (situacion == Guia.FueraDelCampus) linea.text = "Indicaciones en pausa";
            else if (situacion != Guia.Ok) linea.text = destino != null ? "Ubicación por confirmar" : "Toca «Elegir destino»";
            else if (destino == null) linea.text = "Elige un lugar de la lista";
            else if (llegada) linea.text = destino.Nombre;
            else if (progreso != null)
            {
                // La ruta termina en el camino más cercano: se suma el último tramo a la distancia, sin trazarlo.
                double restante = progreso.RestanteMetros + Geo.DistanciaMetros(ruta.Puntos[ruta.Puntos.Count - 1], destino.Coordenada);
                linea.text = Navegacion.TextoDistancia(restante) + " · " + Navegacion.TiempoAPie(restante);
            }
            else linea.text = "Sin ruta peatonal disponible";

            // Cerca del camino, el error del GPS no debe desplazar la guía a un lado: sale de los pies, sobre el camino.
            if (situacion == Guia.Ok && progreso != null && !llegada && rumbo.HasValue)
                guia.Mostrar(ruta, progreso.Volviendo ? estado.Coordenada : progreso.Proyeccion);
            else guia.Ocultar();

            bool indicar = situacion == Guia.Ok && progreso != null && !llegada;
            cajaIndicacion.SetActive(indicar);
            if (indicar)
            {
                flecha.gameObject.SetActive(rumbo.HasValue);
                string texto = "Camina unos pasos para orientar la guía";
                if (rumbo.HasValue)
                {
                    double relativo = Geo.AnguloConSigno(progreso.SiguienteRumbo - rumbo.Value);
                    flecha.localRotation = Quaternion.Euler(0, 0, -(float)relativo);
                    texto = Navegacion.IndicacionDeGiro(relativo);
                    if (progreso.Volviendo) texto = "Vuelve al camino: " + char.ToLowerInvariant(texto[0]) + texto.Substring(1);
                }
                indicacion.text = texto;
            }
        }

        // Si la guía queda fuera del encuadre, una flecha en el borde indica hacia dónde girar.
        void ActualizarBorde()
        {
            bool fuera = guia.FueraDeVista(out Vector2 direccion);
            if (borde.gameObject.activeSelf != fuera) borde.gameObject.SetActive(fuera);
            if (!fuera) return;
            // Recorre el borde de la zona libre entre el estado y la tarjeta.
            float aspecto = raiz.rect.width / Mathf.Max(1, raiz.rect.height), medioX = .5f - 34 / Mathf.Max(1, raiz.rect.width);
            const float centroY = .52f, medioY = .2f;
            float escala = Mathf.Min(medioX / Mathf.Max(1e-4f, Mathf.Abs(direccion.x / aspecto)), medioY / Mathf.Max(1e-4f, Mathf.Abs(direccion.y)));
            borde.anchorMin = borde.anchorMax = new Vector2(.5f + direccion.x / aspecto * escala, centroY + direccion.y * escala);
            flechaBorde.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg - 90);
        }

        void Elegir(Lugar lugar)
        {
            selector.SetActive(false);
            if (lugar == destino) return;
            destino = lugar;
            ruta = null; progreso = null; llegada = false;
            Actualizar();
        }

        void Mensaje(string texto, float segundos = 4)
        {
            mensaje.text = texto;
            cajaMensaje.SetActive(true);
            ocultarMensaje = Time.unscaledTime + segundos;
        }

        // El mapa web se abre dentro de la app, en una ventana de Android encima de la cámara (EsanMapaWeb.java).
        void AbrirMapa()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var actividad = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var mapa = new AndroidJavaClass("pe.esan.mapa.EsanMapaWeb"))
                    mapa.CallStatic("abrir", actividad, MapaWeb, DominioMapa, gameObject.name);
                PausarCamara(true);
                return;
            }
            catch (Exception error) { Debug.LogWarning("ESAN: no se pudo abrir el mapa dentro de la app: " + error.Message); }
#endif
            Application.OpenURL(MapaWeb);
        }

        // Lo llama EsanMapaWeb.java al cerrarse la ventana del mapa.
        public void MapaCerrado(string _) => PausarCamara(false);

        // Con el mapa encima no hace falta la cámara ni dibujar a todo ritmo.
        static void PausarCamara(bool pausar)
        {
            if (Vuforia.VuforiaBehaviour.Instance != null) Vuforia.VuforiaBehaviour.Instance.enabled = !pausar;
            UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = pausar ? 6 : 1;
        }

        void AbrirSelector()
        {
            selector.SetActive(true);
            LlenarLista();
        }

        void LlenarLista()
        {
            for (int i = listaLugares.childCount - 1; i >= 0; i--) Destroy(listaLugares.GetChild(i).gameObject);
            bool conDistancia = gps.Estado.Guia == Guia.Ok;
            var orden = new List<Lugar>(lugares);
            if (conDistancia) orden.Sort((a, b) => Geo.DistanciaMetros(gps.Estado.Coordenada, a.Coordenada).CompareTo(Geo.DistanciaMetros(gps.Estado.Coordenada, b.Coordenada)));
            else orden.Sort((a, b) => string.Compare(a.Nombre, b.Nombre, StringComparison.CurrentCultureIgnoreCase));
            foreach (Lugar lugar in orden)
            {
                bool elegido = lugar == destino;
                Lugar este = lugar;
                Image fila = Caja("Lugar", listaLugares, elegido ? Rosa : Color.white, 10);
                Vertical(fila.gameObject, 10, 10, 7, 7, 1);
                fila.gameObject.AddComponent<LayoutElement>().minHeight = 50;
                fila.gameObject.AddComponent<Button>().onClick.AddListener(() => Elegir(este));
                Escribir(fila.transform, lugar.Nombre, 14, FontStyle.Bold, elegido ? Granate : Tinta);
                Escribir(fila.transform, conDistancia ? Navegacion.TextoDistancia(Geo.DistanciaMetros(gps.Estado.Coordenada, lugar.Coordenada)) + " en línea recta" : "Distancia por confirmar", 12, FontStyle.Normal, elegido ? Granate : Apagado);
            }
        }

        // ---------- Construcción ----------

        void Construir()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventos = new GameObject("EventSystem", typeof(EventSystem));
                eventos.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var lienzo = gameObject.AddComponent<Canvas>();
            lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
            var escala = gameObject.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(390, 844);
            escala.matchWidthOrHeight = 0;
            gameObject.AddComponent<GraphicRaycaster>();
            raiz = Nodo("Área segura", transform);
            AjustarAreaSegura();

            // Barra superior
            Image barra = Caja("Barra", raiz, Panel, 14);
            Anclar(barra.rectTransform, 0, 1, 1, 1, 10, -66, -10, -10);
            Image logo = Caja("Logo", barra.transform, Rojo, 6);
            Anclar(logo.rectTransform, 0, .5f, 0, .5f, 10, -15, 68, 15);
            Text marca = Escribir(logo.transform, "esan", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Estirar(marca.rectTransform);
            Text titulo = Escribir(barra.transform, "Explora el campus", 16, FontStyle.Bold, Tinta, TextAnchor.MiddleLeft);
            Anclar(titulo.rectTransform, 0, 0, 1, 1, 80, 0, -50, 0);
            BotonIcono(barra.transform, "Cerrar", EsanDibujo.Cerrar, Tinta, Application.Quit, 1, .5f, -50, -22, -6, 22);

            // Estado
            RectTransform chips = Nodo("Estado", raiz);
            Anclar(chips, 0, 1, 1, 1, 10, -106, -10, -74);
            Horizontal(chips.gameObject, 0, 0, 0, 0, 6, TextAnchor.MiddleLeft);
            chipZona = Chip(chips, out textoZona, Ambar, Color.white);
            Chip(chips, out textoModo, Panel, Tinta);

            // Aviso temporal
            Image aviso = Caja("Aviso", raiz, Tinta, 12);
            aviso.rectTransform.pivot = new Vector2(.5f, 1);
            Anclar(aviso.rectTransform, 0, 1, 1, 1, 16, -160, -16, -116);
            Vertical(aviso.gameObject, 14, 14, 12, 12, 0);
            aviso.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            mensaje = Escribir(aviso.transform, "", 14, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            cajaMensaje = aviso.gameObject;
            cajaMensaje.SetActive(false);

            // Flecha de borde
            Image circulo = Caja("Borde", raiz, Color.white, 28);
            borde = circulo.rectTransform;
            borde.sizeDelta = new Vector2(56, 56);
            circulo.raycastTarget = false;
            Image fondo = Caja("Fondo", borde, Hex("12a89d"), 25);
            fondo.raycastTarget = false;
            Anclar(fondo.rectTransform, 0, 0, 1, 1, 3, 3, -3, -3);
            Image punta2 = Nodo("Flecha", borde).gameObject.AddComponent<Image>();
            punta2.sprite = EsanDibujo.Icono("flecha", EsanDibujo.Flecha, 2.6f);
            punta2.raycastTarget = false;
            Anclar(punta2.rectTransform, .5f, .5f, .5f, .5f, -14, -14, 14, 14);
            flechaBorde = punta2.rectTransform;
            borde.gameObject.SetActive(false);

            // Tarjeta de destino
            Image tarjeta = Caja("Tarjeta", raiz, new Color(1, 1, 1, .96f), 16);
            tarjeta.rectTransform.pivot = new Vector2(.5f, 0);
            Anclar(tarjeta.rectTransform, 0, 0, 1, 0, 10, 10, -10, 110);
            Vertical(tarjeta.gameObject, 14, 14, 12, 12, 10);
            tarjeta.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform cabecera = Nodo("Destino", tarjeta.transform);
            Vertical(cabecera.gameObject, 0, 0, 0, 0, 2);
            etiqueta = Escribir(cabecera, "TU DESTINO", 12, FontStyle.Bold, Apagado);
            nombre = Escribir(cabecera, "", 20, FontStyle.Bold, Tinta);
            linea = Escribir(cabecera, "", 13, FontStyle.Normal, Apagado);

            Image giro = Caja("Indicación", tarjeta.transform, VerdeClaro, 12);
            Horizontal(giro.gameObject, 12, 12, 6, 6, 10, TextAnchor.MiddleCenter);
            giro.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            Image punta = Nodo("Flecha", giro.transform).gameObject.AddComponent<Image>();
            punta.sprite = EsanDibujo.Icono("flecha", EsanDibujo.Flecha, 2.6f);
            punta.color = VerdeOscuro;
            punta.raycastTarget = false;
            var medida = punta.gameObject.AddComponent<LayoutElement>();
            medida.preferredWidth = medida.preferredHeight = 22;
            flecha = punta.rectTransform;
            indicacion = Escribir(giro.transform, "", 15, FontStyle.Bold, VerdeOscuro, TextAnchor.MiddleCenter);
            cajaIndicacion = giro.gameObject;

            RectTransform acciones = Nodo("Acciones", tarjeta.transform);
            var fila = Horizontal(acciones.gameObject, 0, 0, 0, 0, 8, TextAnchor.MiddleCenter);
            fila.childForceExpandWidth = true;
            botonElegir = Boton(acciones, "Elegir destino", Gris, Tinta, AbrirSelector);
            Boton(acciones, "Volver al mapa", Tinta, Color.white, AbrirMapa);

            // Lista de lugares
            Image hoja = Caja("Lugares", raiz, Color.white, 18);
            Anclar(hoja.rectTransform, 0, 0, 1, .7f, 0, 0, 0, 0);
            Text encabezado = Escribir(hoja.transform, "Elige un destino", 16, FontStyle.Bold, Tinta, TextAnchor.MiddleLeft);
            Anclar(encabezado.rectTransform, 0, 1, 1, 1, 16, -56, -60, -12);
            BotonIcono(hoja.transform, "Cerrar lista", EsanDibujo.Cerrar, Tinta, () => selector.SetActive(false), 1, 1, -56, -56, -12, -12);
            RectTransform zona = Nodo("Desplazable", hoja.transform);
            Anclar(zona, 0, 0, 1, 1, 12, 12, -12, -60);
            zona.gameObject.AddComponent<Image>().color = Color.clear;  // Recibe el arrastre.
            zona.gameObject.AddComponent<RectMask2D>();
            listaLugares = Nodo("Contenido", zona);
            listaLugares.pivot = new Vector2(.5f, 1);
            Anclar(listaLugares, 0, 1, 1, 1, 0, 0, 0, 0);
            Vertical(listaLugares.gameObject, 0, 0, 0, 0, 2);
            listaLugares.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var desplazar = zona.gameObject.AddComponent<ScrollRect>();
            desplazar.content = listaLugares;
            desplazar.viewport = zona;
            desplazar.horizontal = false;
            desplazar.scrollSensitivity = 30;
            selector = hoja.gameObject;
            selector.SetActive(false);
        }

        void AjustarAreaSegura()
        {
            areaSegura = Screen.safeArea;
            raiz.anchorMin = new Vector2(areaSegura.xMin / Screen.width, areaSegura.yMin / Screen.height);
            raiz.anchorMax = new Vector2(areaSegura.xMax / Screen.width, areaSegura.yMax / Screen.height);
            raiz.offsetMin = raiz.offsetMax = Vector2.zero;
        }

        // ---------- Piezas ----------

        static Color Hex(string valor) => ColorUtility.TryParseHtmlString("#" + valor, out Color color) ? color : Color.magenta;

        static RectTransform Nodo(string nombre, Transform padre)
        {
            var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(padre, false);
            return rect;
        }

        // Anclas (0 a 1) y distancias a ellas: izquierda, abajo, derecha, arriba.
        static void Anclar(RectTransform rect, float minX, float minY, float maxX, float maxY, float izquierda, float abajo, float derecha, float arriba)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = new Vector2(izquierda, abajo);
            rect.offsetMax = new Vector2(derecha, arriba);
        }

        static void Estirar(RectTransform rect) => Anclar(rect, 0, 0, 1, 1, 0, 0, 0, 0);

        static Image Caja(string nombre, Transform padre, Color color, int radio)
        {
            var imagen = Nodo(nombre, padre).gameObject.AddComponent<Image>();
            imagen.sprite = EsanDibujo.Redondeado(radio);
            imagen.type = Image.Type.Sliced;
            imagen.color = color;
            return imagen;
        }

        Text Escribir(Transform padre, string contenido, int cuerpo, FontStyle estilo, Color color, TextAnchor alineacion = TextAnchor.UpperLeft)
        {
            var texto = Nodo("Texto", padre).gameObject.AddComponent<Text>();
            texto.font = fuente;
            texto.text = contenido;
            texto.fontSize = cuerpo;
            texto.fontStyle = estilo;
            texto.color = color;
            texto.alignment = alineacion;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            texto.raycastTarget = false;
            return texto;
        }

        static VerticalLayoutGroup Vertical(GameObject objeto, int izquierda, int derecha, int arriba, int abajo, float espacio)
        {
            var grupo = objeto.AddComponent<VerticalLayoutGroup>();
            Ajustar(grupo, izquierda, derecha, arriba, abajo, espacio, TextAnchor.UpperLeft);
            grupo.childForceExpandWidth = true;
            return grupo;
        }

        static HorizontalLayoutGroup Horizontal(GameObject objeto, int izquierda, int derecha, int arriba, int abajo, float espacio, TextAnchor alineacion)
        {
            var grupo = objeto.AddComponent<HorizontalLayoutGroup>();
            Ajustar(grupo, izquierda, derecha, arriba, abajo, espacio, alineacion);
            return grupo;
        }

        static void Ajustar(HorizontalOrVerticalLayoutGroup grupo, int izquierda, int derecha, int arriba, int abajo, float espacio, TextAnchor alineacion)
        {
            grupo.padding = new RectOffset(izquierda, derecha, arriba, abajo);
            grupo.spacing = espacio;
            grupo.childAlignment = alineacion;
            grupo.childControlWidth = grupo.childControlHeight = true;
            grupo.childForceExpandWidth = grupo.childForceExpandHeight = false;
        }

        Image Chip(Transform padre, out Text texto, Color fondo, Color color)
        {
            Image chip = Caja("Chip", padre, fondo, 16);
            Horizontal(chip.gameObject, 12, 12, 0, 0, 0, TextAnchor.MiddleCenter);
            chip.gameObject.AddComponent<LayoutElement>().minHeight = 32;
            chip.raycastTarget = false;
            texto = Escribir(chip.transform, "", 12, FontStyle.Bold, color, TextAnchor.MiddleCenter);
            return chip;
        }

        Text Boton(Transform padre, string rotulo, Color fondo, Color color, Action alPulsar)
        {
            Image caja = Caja(rotulo, padre, fondo, 10);
            var medida = caja.gameObject.AddComponent<LayoutElement>();
            medida.preferredWidth = 0; medida.flexibleWidth = 1; medida.preferredHeight = 44;
            caja.gameObject.AddComponent<Button>().onClick.AddListener(() => alPulsar());
            Text texto = Escribir(caja.transform, rotulo, 14, FontStyle.Bold, color, TextAnchor.MiddleCenter);
            Estirar(texto.rectTransform);
            return texto;
        }

        static void BotonIcono(Transform padre, string nombre, float[] trazos, Color color, Action alPulsar, float anclaX, float anclaY, float izquierda, float abajo, float derecha, float arriba)
        {
            var zona = Nodo(nombre, padre).gameObject.AddComponent<Image>();
            zona.color = Color.clear;
            Anclar(zona.rectTransform, anclaX, anclaY, anclaX, anclaY, izquierda, abajo, derecha, arriba);
            zona.gameObject.AddComponent<Button>().onClick.AddListener(() => alPulsar());
            var icono = Nodo("Icono", zona.transform).gameObject.AddComponent<Image>();
            icono.sprite = EsanDibujo.Icono(nombre, trazos);
            icono.color = color;
            icono.raycastTarget = false;
            Anclar(icono.rectTransform, .5f, .5f, .5f, .5f, -12, -12, 12, 12);
        }
    }
}
