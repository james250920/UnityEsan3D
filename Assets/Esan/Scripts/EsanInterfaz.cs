// Interfaz de la experiencia inmersiva, la misma del mapa web: barra superior, estado, tarjeta de destino,
// indicación de giro y lista de eventos, y la del mapa 3D del campus. Se construye por código al iniciar la escena, sin prefabs.
// Las medidas van en las mismas unidades que los píxeles CSS de la web (pantalla de 390 de ancho).
using System;
using System.Collections;
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
        // Los mismos valores que navigation en ar-config.js del mapa web.
        static readonly double PrecisionMaxima = new ConfigUbicacion().MaxPrecisionParaGuiaMetros;
        const double AnticipoMetros = 12, RecalcularMetros = 25, AjusteMinMetros = 8, AjusteMaxMetros = 15, LlegadaMinMetros = 15, LlegadaMaxMetros = 30;

        static readonly Color Tinta = Hex("253d32"), Apagado = Hex("617269"), Rojo = Hex("ae2239"), Verde = Hex("237044"), Ambar = Hex("7a5a00"), Granate = Hex("a51d35");
        static readonly Color AzulUsuario = Hex("1a73e8"), Panel = new Color(1, 1, 1, .94f), VerdeClaro = Hex("e3f3ea"), VerdeOscuro = Hex("14492d"), Gris = Hex("eef1ee"), Rosa = Hex("f9e8ec");

        EsanUbicacion gps;
        EsanOrientacion orientacion;
        EsanGuia guia;
        EsanMapa mapa;
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
        // Mapa
        RectTransform capaCamara, capaMapa, pinEvento, puntoUsuario;
        Text mapaNombre, mapaLinea, textoPin, botonEvento, rotulo3D, rotuloPlano;
        Image fondo3D, fondoPlano;
        Ruta rutaMapa;
        Coord rutaMapaDesde;
        Lugar rutaMapaHasta;
        // Carga, avisos y diálogos
        GameObject cargando, banda, dialogo;
        RectTransform giroCarga, accionesDialogo;
        Text textoCarga, textoBanda, accionBanda, tituloDialogo, textoDialogo;
        Action alPulsarBanda;
        string bandaVisible, bandaDescartada;
        bool abriendoMapa;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (FindAnyObjectByType<EsanInterfaz>() == null) new GameObject("ESAN Interfaz").AddComponent<EsanInterfaz>();
        }

        void Awake()
        {
            grafo = EsanDatos.CargarGrafo();
            lugares = EsanDatos.CargarEventos();
            gps = gameObject.AddComponent<EsanUbicacion>();
            gps.Preparar(new Ubicacion(EsanDatos.CargarPerimetro()));
            gps.Cambio += Actualizar;
            orientacion = gameObject.AddComponent<EsanOrientacion>();
            guia = gameObject.AddComponent<EsanGuia>();
            guia.Preparar(orientacion, new Coord(-76.9612, -12.1050));
            mapa = gameObject.AddComponent<EsanMapa>();
            mapa.Preparar(EsanDatos.CargarCalibracion());
            // La app lleva a un evento: si solo hay uno, ya es el destino.
            if (lugares.Count == 1) destino = lugares[0];
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
            if (cargando.activeSelf) giroCarga.Rotate(0, 0, -360 * Time.unscaledDeltaTime);
            // Botón Atrás de Android: cierra lo que esté encima antes de salir.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !cargando.activeSelf)
            {
                if (dialogo.activeSelf) dialogo.SetActive(false);
                else if (selector.activeSelf) selector.SetActive(false);
                else if (mapa.Abierto) CerrarMapa();
                else ConfirmarSalida();
            }
            if (mapa.Abierto) ColocarMarcas();
            orientacion.RumboDeReserva = gps.RumboDeReserva;
            orientacion.ForzarReserva = gps.RumboSimulado;
            if (!mapa.Abierto) ActualizarBorde();
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
            textoZona.text = situacion == Guia.Ok ? "Dentro del campus" : situacion == Guia.FueraDelCampus ? "Fuera del campus"
                : gps.PermisoNegado ? "Sin permiso de ubicación" : gps.Apagada ? "Ubicación apagada" : !estado.HayLectura ? "Buscando tu ubicación…"
                : estado.Caducada ? "Ubicación sin actualizar" : estado.Precision > PrecisionMaxima ? "Señal GPS débil" : "Confirmando ubicación…";
            chipZona.color = situacion == Guia.Ok ? Verde : situacion == Guia.FueraDelCampus ? Granate : Ambar;
            double? rumbo = orientacion.Rumbo;
            if (mapa.Abierto) ActualizarMapa();
            ActualizarBanda(estado, situacion);
            textoModo.text = EsanMarcadores.VuforiaFallo ? "Sin cámara" : !EsanMarcadores.VuforiaListo ? "Abriendo cámara…" : rumbo.HasValue ? "Guía lista" : "Orientando la guía";

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
            nombre.text = llegada ? "¡Llegaste al evento!" : destino != null ? destino.Nombre : lugares.Count == 0 ? "No hay eventos por ahora" : "Elige un evento";
            botonElegir.text = destino != null ? "Cambiar evento" : "Elegir evento";
            botonElegir.transform.parent.gameObject.SetActive(lugares.Count > 1);
            if (destino == null && lugares.Count == 0) linea.text = "Pronto verás aquí cómo llegar";
            else if (gps.PermisoNegado) linea.text = "Sin permiso de ubicación";
            else if (gps.Apagada) linea.text = "La ubicación del celular está apagada";
            else if (situacion == Guia.FueraDelCampus) linea.text = "Las indicaciones empiezan al entrar al campus";
            else if (situacion != Guia.Ok) linea.text = destino != null ? "Buscando tu ubicación…" : "Toca «Elegir evento»";
            else if (destino == null) linea.text = "Toca «Elegir evento»";
            else if (llegada) linea.text = destino.Nombre;
            else if (progreso != null)
            {
                // La ruta termina en el camino más cercano: se suma el último tramo a la distancia, sin trazarlo.
                double restante = progreso.RestanteMetros + Geo.DistanciaMetros(ruta.Puntos[ruta.Puntos.Count - 1], destino.Coordenada);
                linea.text = Navegacion.TextoDistancia(restante) + " · " + Navegacion.TiempoAPie(restante);
            }
            else linea.text = "No encontramos un camino peatonal; guíate con el mapa";

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
            if (mapa.Abierto) ColocarMarcas();
        }

        void Mensaje(string texto, float segundos = 4)
        {
            mensaje.text = texto;
            cajaMensaje.SetActive(true);
            ocultarMensaje = Time.unscaledTime + segundos;
        }

        // ---------- Mapa ----------

        void AbrirMapa()
        {
            if (!abriendoMapa && !mapa.Abierto) StartCoroutine(AbrirMapaConCarga());
        }

        // La primera vez el modelo tarda unos segundos: se muestra la carga con su progreso.
        IEnumerator AbrirMapaConCarga()
        {
            abriendoMapa = true;
            if (!mapa.Cargado)
            {
                textoCarga.text = "Cargando el mapa del campus…";
                cargando.SetActive(true);
                yield return null;
                yield return mapa.Cargar(avance => textoCarga.text = "Cargando el mapa del campus… " + Mathf.RoundToInt(avance * 100) + " %");
                cargando.SetActive(false);
            }
            abriendoMapa = false;
            PausarCamara(true);
            mapa.Abrir();
            capaCamara.gameObject.SetActive(false);
            capaMapa.gameObject.SetActive(true);
            rutaMapaHasta = null;
            PosicionarBanda(true);
            ActualizarMapa();
            ColocarMarcas();
            Actualizar();
        }

        void CerrarMapa()
        {
            mapa.Cerrar();
            PausarCamara(false);
            capaMapa.gameObject.SetActive(false);
            capaCamara.gameObject.SetActive(true);
            PosicionarBanda(false);
            Actualizar();
        }

        // Con el mapa encima no hace falta la cámara de Vuforia.
        static void PausarCamara(bool pausar)
        {
            if (Vuforia.VuforiaBehaviour.Instance != null) Vuforia.VuforiaBehaviour.Instance.enabled = !pausar;
        }

        // Tarjeta del evento y ruta dibujada sobre el campus.
        void ActualizarMapa()
        {
            Ubicacion estado = gps.Estado;
            botonEvento.transform.parent.gameObject.SetActive(lugares.Count > 1);
            botonEvento.text = destino != null ? "Cambiar evento" : "Elegir evento";
            textoPin.text = destino != null ? destino.Nombre : "";
            if (destino == null)
            {
                mapaNombre.text = lugares.Count == 0 ? "No hay eventos por ahora" : "Elige un evento";
                mapaLinea.text = lugares.Count == 0 ? "Pronto verás aquí cómo llegar" : "Toca «Elegir evento»";
                rutaMapa = null; rutaMapaHasta = null;
                mapa.MostrarRuta(null);
                return;
            }
            mapaNombre.text = destino.Nombre;
            string fecha = string.IsNullOrEmpty(destino.Fecha) ? "" : destino.Fecha + " · ";
            if (!estado.HayLectura)
            {
                mapaLinea.text = fecha + (gps.PermisoNegado ? "Permite la ubicación para ver la ruta" : gps.Apagada ? "Activa la ubicación para ver la ruta" : "Buscando tu ubicación…");
                mapa.MostrarRuta(null);
                return;
            }
            // La ruta se recalcula al cambiar de destino o tras moverse unos metros.
            if (rutaMapaHasta != destino || Geo.DistanciaMetros(rutaMapaDesde, estado.Coordenada) > 8)
            {
                rutaMapa = grafo.RutaMasCorta(estado.Coordenada, destino.Coordenada);
                rutaMapaDesde = estado.Coordenada;
                rutaMapaHasta = destino;
                if (rutaMapa == null) mapa.MostrarRuta(null);
                else mapa.MostrarRuta(new List<Coord>(rutaMapa.Puntos) { destino.Coordenada });
            }
            if (rutaMapa == null) { mapaLinea.text = fecha + "Sin ruta peatonal disponible"; return; }
            double metros = Geo.DistanciaMetros(estado.Coordenada, rutaMapa.Puntos[0]) + rutaMapa.Metros + Geo.DistanciaMetros(rutaMapa.Puntos[rutaMapa.Puntos.Count - 1], destino.Coordenada);
            mapaLinea.text = fecha + Navegacion.TextoDistancia(metros) + " · " + Navegacion.TiempoAPie(metros);
        }

        // El pin del evento y el punto azul del usuario siguen al modelo mientras se mueve la vista.
        void ColocarMarcas()
        {
            puntoUsuario.gameObject.SetActive(gps.Estado.HayLectura && Colocar(puntoUsuario, gps.Estado.Coordenada));
            pinEvento.gameObject.SetActive(destino != null && Colocar(pinEvento, destino.Coordenada));
        }

        bool Colocar(RectTransform marca, Coord punto)
        {
            if (!mapa.Proyectar(punto, out Vector2 pantalla)) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(capaMapa, pantalla, null, out Vector2 local);
            marca.localPosition = local;
            return true;
        }

        void CambiarPlano(bool plano)
        {
            mapa.CambiarPlano(plano);
            fondo3D.color = plano ? Gris : Tinta;
            rotulo3D.color = plano ? Tinta : Color.white;
            fondoPlano.color = plano ? Tinta : Gris;
            rotuloPlano.color = plano ? Color.white : Tinta;
        }

        void IrAMiUbicacion()
        {
            if (gps.Estado.HayLectura) { mapa.Enfocar(gps.Estado.Coordenada); Mensaje("Centrado en tu ubicación", 2); }
            else if (gps.PermisoNegado) gps.PedirPermiso();
            else if (gps.Apagada) EsanUbicacion.AbrirAjustesDeUbicacion();
            else Mensaje("Todavía buscamos tu ubicación. Al aire libre se encuentra antes.");
        }

        // ---------- Avisos ----------

        // Un solo aviso a la vez, el más importante, con la acción que lo resuelve.
        void ActualizarBanda(Ubicacion estado, Guia situacion)
        {
            string clave = null, texto = null, accion = null;
            Action alPulsar = null;
            if (gps.PermisoNegado)
            {
                clave = "permiso"; accion = "Permitir"; alPulsar = gps.PedirPermiso;
                texto = "Necesitamos tu ubicación para guiarte hasta el evento.";
            }
            else if (gps.Apagada)
            {
                clave = "apagada"; accion = "Activar"; alPulsar = EsanUbicacion.AbrirAjustesDeUbicacion;
                texto = "La ubicación del celular está apagada. Actívala para ver dónde estás y la ruta.";
            }
            else if (!mapa.Abierto && EsanMarcadores.SinPermisoCamara)
            {
                clave = "cámara"; accion = "Permitir"; alPulsar = EsanUbicacion.AbrirAjustesDeApp;
                texto = "Sin permiso de cámara no podemos mostrar la guía. Mientras tanto puedes usar el mapa.";
            }
            else if (!mapa.Abierto && EsanMarcadores.VuforiaFallo)
            {
                clave = "sin cámara"; accion = "Ver mapa"; alPulsar = AbrirMapa;
                texto = "No se pudo abrir la cámara. Usa el mapa para llegar al evento.";
            }
            else if (estado.HayLectura && !estado.Caducada && estado.Precision > PrecisionMaxima)
            {
                clave = "señal";
                texto = "Señal GPS débil (±" + Mathf.RoundToInt((float)estado.Precision) + " m). Sal a un espacio abierto para mejorarla.";
            }
            else if (estado.Caducada)
            {
                clave = "sin actualizar";
                texto = "Hace un rato que no recibimos tu ubicación. Al aire libre se recupera antes.";
            }
            else if (situacion == Guia.FueraDelCampus)
            {
                clave = "fuera";
                texto = "Estás fuera del campus. Las indicaciones empiezan al entrar.";
                if (!mapa.Abierto) { accion = "Ver mapa"; alPulsar = AbrirMapa; }
            }
            // Lo que se cerró vuelve a aparecer solo si cambia la situación.
            if (clave != bandaDescartada) bandaDescartada = null;
            bool ver = clave != null && clave != bandaDescartada;
            banda.SetActive(ver);
            bandaVisible = clave;
            if (!ver) return;
            textoBanda.text = texto;
            accionBanda.transform.parent.gameObject.SetActive(accion != null);
            accionBanda.text = accion ?? "";
            alPulsarBanda = alPulsar;
        }

        void PosicionarBanda(bool enMapa)
        {
            var rect = (RectTransform)banda.transform;
            rect.anchoredPosition = new Vector2(0, enMapa ? -178 : -114);
        }

        void ConfirmarSalida() => Dialogo("¿Salir de la app?", "Dejarás de ver la guía hacia el evento.",
            ("Cancelar", Gris, Tinta, null), ("Salir", Rojo, Color.white, Application.Quit));

        void Ayuda() => Dialogo("Cómo llegar al evento",
            "<b>Con la cámara</b>\nApunta al suelo y sigue la línea azul. La tarjeta de abajo te dice hacia dónde girar y cuánto falta.\n\n" +
            "<b>Con el mapa</b>\nUn dedo gira la vista; dos dedos la mueven y acercan, y dos toques seguidos acercan. El pin rojo es el evento y el punto azul eres tú.\n\n" +
            "<b>Si la guía no aparece</b>\nActiva la ubicación, camina unos pasos para orientar la brújula y, si puedes, sal a un espacio abierto.",
            ("Entendido", Tinta, Color.white, null));

        void Dialogo(string titulo, string texto, params (string rotulo, Color fondo, Color color, Action alPulsar)[] acciones)
        {
            tituloDialogo.text = titulo;
            textoDialogo.text = texto;
            for (int i = accionesDialogo.childCount - 1; i >= 0; i--) Destroy(accionesDialogo.GetChild(i).gameObject);
            foreach (var accion in acciones)
            {
                Action alPulsar = accion.alPulsar;
                Boton(accionesDialogo, accion.rotulo, accion.fondo, accion.color, () => { dialogo.SetActive(false); alPulsar?.Invoke(); });
            }
            dialogo.SetActive(true);
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
            if (orden.Count == 0)
            {
                Text vacio = Escribir(listaLugares, "No hay eventos por ahora", 14, FontStyle.Normal, Apagado);
                vacio.gameObject.AddComponent<LayoutElement>().minHeight = 50;
            }
            foreach (Lugar lugar in orden)
            {
                bool elegido = lugar == destino;
                Lugar este = lugar;
                Image fila = Caja("Lugar", listaLugares, elegido ? Rosa : Color.white, 10);
                Vertical(fila.gameObject, 10, 10, 7, 7, 1);
                fila.gameObject.AddComponent<LayoutElement>().minHeight = 50;
                fila.gameObject.AddComponent<Button>().onClick.AddListener(() => Elegir(este));
                Escribir(fila.transform, lugar.Nombre, 14, FontStyle.Bold, elegido ? Granate : Tinta);
                string distancia = conDistancia ? Navegacion.TextoDistancia(Geo.DistanciaMetros(gps.Estado.Coordenada, lugar.Coordenada)) + " en línea recta" : "Distancia por confirmar";
                Escribir(fila.transform, lugar.Fecha.Length > 0 ? lugar.Fecha + " · " + distancia : distancia, 12, FontStyle.Normal, elegido ? Granate : Apagado);
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
            Anclar(titulo.rectTransform, 0, 0, 1, 1, 80, 0, -94, 0);
            BotonIcono(barra.transform, "Ayuda", EsanDibujo.Ayuda, Tinta, Ayuda, 1, .5f, -94, -22, -50, 22);
            BotonIcono(barra.transform, "Cerrar", EsanDibujo.Cerrar, Tinta, ConfirmarSalida, 1, .5f, -50, -22, -6, 22);

            // Estado
            RectTransform chips = Nodo("Estado", raiz);
            Anclar(chips, 0, 1, 1, 1, 10, -106, -10, -74);
            Horizontal(chips.gameObject, 0, 0, 0, 0, 6, TextAnchor.MiddleLeft);
            chipZona = Chip(chips, out textoZona, Ambar, Color.white);
            Chip(chips, out textoModo, Panel, Tinta);

            // Aviso temporal
            Image aviso = Caja("Aviso", raiz, Tinta, 12);
            aviso.rectTransform.pivot = new Vector2(.5f, 0);
            aviso.raycastTarget = false;
            Anclar(aviso.rectTransform, 0, .3f, 1, .3f, 16, 0, -16, 44);
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
            etiqueta = Escribir(cabecera, "TU EVENTO", 12, FontStyle.Bold, Apagado);
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
            botonElegir = Boton(acciones, "Elegir evento", Gris, Tinta, AbrirSelector);
            Boton(acciones, "Ver mapa", Tinta, Color.white, AbrirMapa);

            // Todo lo de la cámara queda en una capa que se oculta con el mapa abierto.
            capaCamara = Nodo("Cámara", raiz);
            Estirar(capaCamara);
            while (raiz.childCount > 1) raiz.GetChild(0).SetParent(capaCamara, false);
            ConstruirMapa();
            // El aviso temporal y la banda quedan por encima de las dos vistas.
            aviso.transform.SetParent(raiz, false);
            ConstruirBanda();

            // Lista de eventos
            Image hoja = Caja("Eventos", raiz, Color.white, 18);
            Anclar(hoja.rectTransform, 0, 0, 1, .7f, 0, 0, 0, 0);
            Text encabezado = Escribir(hoja.transform, "Eventos", 16, FontStyle.Bold, Tinta, TextAnchor.MiddleLeft);
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

            ConstruirDialogo();
            ConstruirCarga();
        }

        // Banda de aviso con acción, debajo de la cabecera.
        void ConstruirBanda()
        {
            Image caja = Caja("Banda", raiz, Hex("fff4d6"), 12);
            caja.rectTransform.pivot = new Vector2(.5f, 1);
            Anclar(caja.rectTransform, 0, 1, 1, 1, 10, -114, -10, -114);
            Horizontal(caja.gameObject, 12, 4, 8, 8, 8, TextAnchor.MiddleLeft);
            caja.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            textoBanda = Escribir(caja.transform, "", 13, FontStyle.Normal, Hex("5c4300"), TextAnchor.MiddleLeft);
            textoBanda.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            accionBanda = Boton(caja.transform, "", Ambar, Color.white, () => alPulsarBanda?.Invoke());
            var medida = accionBanda.transform.parent.GetComponent<LayoutElement>();
            medida.flexibleWidth = 0; medida.preferredWidth = 88; medida.preferredHeight = 36;
            Image cerrar = Nodo("Cerrar aviso", caja.transform).gameObject.AddComponent<Image>();
            cerrar.color = Color.clear;
            var lado = cerrar.gameObject.AddComponent<LayoutElement>();
            lado.preferredWidth = lado.preferredHeight = 36;
            cerrar.gameObject.AddComponent<Button>().onClick.AddListener(() => { bandaDescartada = bandaVisible; banda.SetActive(false); });
            Image equis = Nodo("Icono", cerrar.transform).gameObject.AddComponent<Image>();
            equis.sprite = EsanDibujo.Icono("Cerrar", EsanDibujo.Cerrar);
            equis.color = Hex("5c4300");
            equis.raycastTarget = false;
            Anclar(equis.rectTransform, .5f, .5f, .5f, .5f, -9, -9, 9, 9);
            banda = caja.gameObject;
            banda.SetActive(false);
        }

        // Diálogo modal: salir, ayuda. Cubre toda la pantalla, también fuera del área segura.
        void ConstruirDialogo()
        {
            Image velo = Nodo("Diálogo", transform).gameObject.AddComponent<Image>();
            velo.color = new Color(0, 0, 0, .45f);
            Estirar(velo.rectTransform);
            Image caja = Caja("Caja", velo.transform, Color.white, 18);
            caja.rectTransform.pivot = new Vector2(.5f, .5f);
            Anclar(caja.rectTransform, 0, .5f, 1, .5f, 24, 0, -24, 0);
            Vertical(caja.gameObject, 20, 20, 20, 16, 12);
            caja.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tituloDialogo = Escribir(caja.transform, "", 18, FontStyle.Bold, Tinta);
            textoDialogo = Escribir(caja.transform, "", 14, FontStyle.Normal, Apagado);
            textoDialogo.supportRichText = true;
            accionesDialogo = Nodo("Acciones", caja.transform);
            Horizontal(accionesDialogo.gameObject, 0, 0, 6, 0, 8, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            dialogo = velo.gameObject;
            dialogo.SetActive(false);
        }

        // Pantalla de carga del mapa: tapa la cámara y bloquea los toques mientras dura.
        void ConstruirCarga()
        {
            Image fondo = Nodo("Cargando", transform).gameObject.AddComponent<Image>();
            fondo.color = Hex("dbe3de");
            Estirar(fondo.rectTransform);
            Image giro = Nodo("Giro", fondo.transform).gameObject.AddComponent<Image>();
            giro.sprite = EsanDibujo.Icono("carga", EsanDibujo.Carga, 2.8f);
            giro.color = Tinta;
            giro.raycastTarget = false;
            Anclar(giro.rectTransform, .5f, .5f, .5f, .5f, -24, 8, 24, 56);
            giroCarga = giro.rectTransform;
            textoCarga = Escribir(fondo.transform, "", 15, FontStyle.Bold, Tinta, TextAnchor.UpperCenter);
            Anclar(textoCarga.rectTransform, 0, .5f, 1, .5f, 24, -40, -24, -8);
            cargando = fondo.gameObject;
            cargando.SetActive(false);
        }

        void ConstruirMapa()
        {
            capaMapa = Nodo("Mapa", raiz);
            Estirar(capaMapa);

            // Marcas, debajo de los controles. Sin raycast para no robar los gestos del mapa.
            puntoUsuario = Nodo("Tú", capaMapa);
            puntoUsuario.sizeDelta = new Vector2(22, 22);
            Image borde = Circulo(puntoUsuario, Color.white, 22);
            Circulo(borde.transform, AzulUsuario, 14);
            pinEvento = Nodo("Evento", capaMapa);
            pinEvento.sizeDelta = new Vector2(0, 0);
            Image cabeza = Circulo(pinEvento, Color.white, 26);
            cabeza.rectTransform.anchoredPosition = new Vector2(0, 13);
            Circulo(cabeza.transform, Granate, 18);
            Image etiquetaPin = Caja("Nombre", pinEvento, Granate, 10);
            etiquetaPin.raycastTarget = false;
            etiquetaPin.rectTransform.pivot = new Vector2(.5f, 0);
            etiquetaPin.rectTransform.anchoredPosition = new Vector2(0, 30);
            Horizontal(etiquetaPin.gameObject, 10, 10, 5, 5, 0, TextAnchor.MiddleCenter);
            var ajuste = etiquetaPin.gameObject.AddComponent<ContentSizeFitter>();
            ajuste.horizontalFit = ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            textoPin = Escribir(etiquetaPin.transform, "", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            textoPin.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Barra para volver, como la de la ventana del mapa web.
            Image volver = Caja("Volver a la cámara", capaMapa, Tinta, 1);
            Anclar(volver.rectTransform, 0, 1, 1, 1, 0, -48, 0, 0);
            volver.gameObject.AddComponent<Button>().onClick.AddListener(CerrarMapa);
            Image atras = Nodo("Icono", volver.transform).gameObject.AddComponent<Image>();
            atras.sprite = EsanDibujo.Icono("atras", EsanDibujo.Atras);
            atras.raycastTarget = false;
            Anclar(atras.rectTransform, 0, .5f, 0, .5f, 12, -11, 34, 11);
            Text rotuloVolver = Escribir(volver.transform, "Volver a la cámara", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            Anclar(rotuloVolver.rectTransform, 0, 0, 1, 1, 42, 0, -10, 0);

            // Cabecera
            Image cabecera = Caja("Cabecera", capaMapa, Panel, 14);
            Anclar(cabecera.rectTransform, 0, 1, 1, 1, 10, -114, -10, -58);
            Image logo = Caja("Logo", cabecera.transform, Rojo, 6);
            Anclar(logo.rectTransform, 0, .5f, 0, .5f, 10, -15, 68, 15);
            Estirar(Escribir(logo.transform, "esan", 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter).rectTransform);
            Text titulo = Escribir(cabecera.transform, "Mapa del campus", 16, FontStyle.Bold, Tinta, TextAnchor.MiddleLeft);
            Anclar(titulo.rectTransform, 0, 0, 1, 1, 80, 0, -94, 0);
            BotonIcono(cabecera.transform, "Ayuda", EsanDibujo.Ayuda, Tinta, Ayuda, 1, .5f, -94, -22, -50, 22);
            BotonIcono(cabecera.transform, "Mi ubicación", EsanDibujo.Ubicar, Tinta, IrAMiUbicacion, 1, .5f, -50, -22, -6, 22);

            // 3D o plano, y vista general
            Image selectorVista = Caja("Vista", capaMapa, Panel, 14);
            Anclar(selectorVista.rectTransform, 1, 1, 1, 1, -186, -170, -62, -124);
            Horizontal(selectorVista.gameObject, 4, 4, 4, 4, 4, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            rotulo3D = Boton(selectorVista.transform, "3D", Tinta, Color.white, () => CambiarPlano(false));
            fondo3D = rotulo3D.transform.parent.GetComponent<Image>();
            rotuloPlano = Boton(selectorVista.transform, "Plano", Gris, Tinta, () => CambiarPlano(true));
            fondoPlano = rotuloPlano.transform.parent.GetComponent<Image>();
            Image general = Caja("Vista general", capaMapa, Panel, 14);
            Anclar(general.rectTransform, 1, 1, 1, 1, -56, -170, -10, -124);
            BotonIcono(general.transform, "Vista general", EsanDibujo.Encuadre, Tinta, () => { mapa.VistaGeneral(); Mensaje("Vista de todo el campus", 2); }, .5f, .5f, -23, -23, 23, 23);

            // Zoom
            Image zoom = Caja("Zoom", capaMapa, Panel, 14);
            Anclar(zoom.rectTransform, 1, .5f, 1, .5f, -56, -46, -10, 46);
            BotonIcono(zoom.transform, "Acercar", EsanDibujo.Mas, Tinta, () => mapa.Acercar(.7f), .5f, 1, -23, -46, 23, 0);
            BotonIcono(zoom.transform, "Alejar", EsanDibujo.Menos, Tinta, () => mapa.Acercar(1 / .7f), .5f, 0, -23, 0, 23, 46);

            // Tarjeta del evento
            Image tarjeta = Caja("Tarjeta del evento", capaMapa, new Color(1, 1, 1, .96f), 16);
            tarjeta.rectTransform.pivot = new Vector2(.5f, 0);
            Anclar(tarjeta.rectTransform, 0, 0, 1, 0, 10, 10, -10, 110);
            Vertical(tarjeta.gameObject, 14, 14, 12, 12, 10);
            tarjeta.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform datos = Nodo("Datos", tarjeta.transform);
            Vertical(datos.gameObject, 0, 0, 0, 0, 2);
            Escribir(datos, "EVENTO", 12, FontStyle.Bold, Apagado);
            mapaNombre = Escribir(datos, "", 20, FontStyle.Bold, Tinta);
            mapaLinea = Escribir(datos, "", 13, FontStyle.Normal, Apagado);
            RectTransform acciones = Nodo("Acciones", tarjeta.transform);
            Horizontal(acciones.gameObject, 0, 0, 0, 0, 8, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            botonEvento = Boton(acciones, "Elegir evento", Gris, Tinta, AbrirSelector);
            Boton(acciones, "Ir con la cámara", Tinta, Color.white, CerrarMapa);

            capaMapa.gameObject.SetActive(false);
        }

        static Image Circulo(Transform padre, Color color, float diametro)
        {
            var imagen = Nodo("Círculo", padre).gameObject.AddComponent<Image>();
            imagen.sprite = EsanDibujo.Redondeado(12);
            imagen.color = color;
            imagen.raycastTarget = false;
            imagen.rectTransform.sizeDelta = new Vector2(diametro, diametro);
            return imagen;
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
