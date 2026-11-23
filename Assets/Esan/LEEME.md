# ESAN en Unity

App para llegar al evento de ESAN: guía en realidad aumentada con Vuforia y mapa 3D del campus, todo en Unity,
para Android. Ya no abre el mapa web.

## Qué hay

| Carpeta | Contenido |
| --- | --- |
| `Resources/Datos/` | Los mismos JSON del mapa web: grafo de caminos, lugares (ya no se usan) y perímetro (`esan-boundary.json` es `esan-boundary.geojson` con otra extensión). |
| `Resources/Datos/eventos.json` | Eventos que aparecen en la lista de destinos. Los nombres de lugares ya no se muestran. |
| `Resources/Marcadores/` | Fotos de la pileta, `M1` y `M2`. Son JPEG con extensión `.bytes` para que Unity las entregue sin procesar. |
| `Resources/Modelos/chuwi.obj` | Chuwi, convertido de `chuwi.glb`. |
| `Resources/Modelos/campus.fbx` | Campus 3D del mapa web, convertido con Blender del `campus.glb` de `esan-godot`. |
| `Resources/Datos/calibracion.json` | Encaje del modelo con las coordenadas geográficas (el `calibration.json` de `esan-godot`). |
| `Scripts/Core/Calibracion.cs` | Pasa una coordenada al modelo 3D. |
| `Scripts/EsanMapa.cs` | Mapa 3D: modelo, cámara orbital con gestos, ruta y proyección del pin y del usuario. |
| `Scripts/Core/` | Coordenadas, rutas con A\* y progreso sobre la ruta, portados del JavaScript. No dependen de Unity. |
| `Scripts/EsanDatos.cs` | Lee los JSON y entrega el grafo, los eventos, el perímetro y la calibración. |
| `Scripts/EsanMarcadores.cs` | Prueba de marcadores: muestra a Chuwi al reconocer la pileta. |
| `Scripts/EsanInterfaz.cs` | La interfaz de la web, construida por código: barra, estado, tarjeta de destino, indicación de giro y lista de eventos. |
| `Scripts/EsanUbicacion.cs` | GPS del celular y ubicación simulada. |
| `Scripts/EsanOrientacion.cs` | Hacia dónde mira la cámara, con los sensores de rotación de Android. Funciona sin giroscopio. |
| `Scripts/EsanGuia.cs` y `Resources/EsanGuia.shader` | Guía azul sobre el suelo y cálculo de la flecha de borde. |
| `Scripts/EsanDibujo.cs` | Paneles redondeados e iconos generados por código. |
| `Editor/EsanAndroid.cs` | Menú `ESAN > Compilar y ejecutar en Android`. |

## Compilar e instalar en Android

El modo Play de Vuforia no funciona en el editor de Linux: todo se prueba en el celular.

- Con Unity abierto: menú `ESAN > Compilar y ejecutar en Android`.
- Con Unity cerrado:
  `Unity -batchmode -projectPath <proyecto> -buildTarget Android -executeMethod Esan.Editor.EsanAndroid.CompilarYEjecutar -logFile <registro>`

El menú quita cámaras sobrantes de `Main scene`, exige Android 10 (lo pide Vuforia 11), compila en modo desarrollo
e instala `Builds/ProyectoEsan.apk`. El error `DllNotFoundException: VuforiaEngine.dll` del registro es normal en
Linux y no detiene la compilación.

Los scripts `EsanMarcadores` y `EsanInterfaz` se crean solos al iniciar: no hay que añadir nada a la escena.
Los marcadores se crean desde las fotos al arrancar, sin el Target Manager de Vuforia. Para ajustar tamaño y
posición de Chuwi, edita `Lista` al inicio de `EsanMarcadores.cs`.

### Simular una ubicación

Solo en compilaciones de desarrollo, para probar sin estar en el campus:

```
adb shell am force-stop com.esan.campusra
adb shell am start -n com.esan.campusra/com.unity3d.player.UnityPlayerGameActivity -e esan_sim "-76.9612,-12.1050,0"
```

Los valores son longitud, latitud y rumbo (0 = norte). Con rumbo, la cámara simulada mira hacia allí, 10° hacia
abajo; sin rumbo se usa el sensor real del celular. Al abrir la app de forma normal usa el GPS real.

## Pruebas del código portado

`PruebasCore/`, junto a `Assets`, compara el C# con los resultados del mapa web:

```
cd PruebasCore
dotnet run
```

Si cambia el grafo o la lógica en la web, copia los JSON a `Resources/Datos/` y regenera lo esperado con
`node generar-esperado.mjs "<ruta a esan-mapa/dist>"`.

## Eventos

La lista de destinos muestra solo eventos, leídos de `Resources/Datos/eventos.json`. La ruta se sigue calculando
con el grafo de caminos. Cada evento necesita `nombre` y `coordenadas` (longitud, latitud); `fecha` es opcional y se
muestra como texto:

```json
{
  "eventos": [
    { "nombre": "Feria de proyectos", "fecha": "28 nov, 10:00", "coordenadas": [-76.96101, -12.10543] }
  ]
}
```

## Mapa del campus

«Ver mapa» abre el campus en 3D dentro de Unity, sin internet. Muestra el evento con un pin, tu ubicación con un
punto azul y la ruta del grafo de caminos hasta el evento, con la distancia y el tiempo a pie. Un dedo gira la vista,
dos dedos la desplazan y acercan; hay botones de 3D/Plano, vista general, mi ubicación y zoom. Se cierra con
«Volver a la cámara», «Ir con la cámara» o el botón Atrás. Mientras está abierto, Vuforia queda en pausa.

El modelo y la calibración vienen de `esan-godot` (`assets/campus.glb` y `data/calibration.json`). Si cambia el
modelo, conviértelo de nuevo a FBX con Blender (exportador FBX, ejes `-Z` adelante e `Y` arriba, texturas
incrustadas). Al importarlo, Unity invierte el eje X del glTF; `EsanMapa.Superficie` lo tiene en cuenta. El encaje
es visual y aproximado, igual que en la web.

## Heurísticas de usabilidad

La interfaz sigue las 10 heurísticas de Nielsen
([resumen en uiFromMars](https://uifrommars.com/10-reglas-heuristicas-como-aplicarlas/)):

| Heurística | En la app |
| --- | --- |
| 1. Visibilidad del estado | Pantalla de carga con progreso la primera vez que se abre el mapa. Los indicadores de estado dicen qué pasa con la ubicación (buscando, apagada, sin permiso, señal débil) y con la cámara. |
| 2. Lenguaje del usuario | Textos sin jerga: «Guía lista» en vez de «RA activa», «Buscando tu ubicación…» en vez de «Ubicación por confirmar». |
| 3. Control y libertad | La X y el botón Atrás piden confirmación antes de salir. Atrás cierra primero el diálogo, la lista o el mapa. Los avisos se pueden cerrar. |
| 4. Consistencia | «Evento» en las dos vistas, los mismos botones y colores; el botón de elegir evento solo aparece si hay más de uno. |
| 5. Prevención de errores | Aviso de señal GPS débil antes de seguir una guía imprecisa y aviso al estar fuera del campus. |
| 6. Reconocer antes que recordar | El pin y el punto azul siempre visibles; los botones de icono confirman su acción con un mensaje breve. |
| 7. Flexibilidad | Gestos del mapa (girar, mover, pellizcar) y doble toque para acercar, además de los botones. |
| 8. Diseño minimalista | Un solo aviso a la vez, el más importante. |
| 9. Recuperarse de errores | Cada aviso trae la acción que lo resuelve: «Permitir» (vuelve a pedir el permiso o abre los ajustes de la app), «Activar» (ajustes de ubicación), «Ver mapa» si falla la cámara. Al volver de los ajustes, la app retoma la ubicación sola. |
| 10. Ayuda | Botón «?» en la cámara y en el mapa con instrucciones breves. |

## Orientación y guía

La guía se dibuja en metros reales y se coloca delante de la cámara con el GPS y la orientación del celular, como
en la web; no usa el seguimiento de Vuforia. La orientación sale del vector de rotación de Android y, en celulares
sin giroscopio como el Galaxy A15, del vector de rotación geomagnético (acelerómetro y magnetómetro), que tiembla
más y por eso se suaviza. La brújula plana de Unity no sirve: falla con el celular en vertical.

## Pendiente

- Probar la guía en el campus: alineación con el camino, temblor del rumbo y efecto de metales cercanos.
- Etiquetas de los lugares en la cámara y modelo anclado a una coordenada.
- Reconocer pasadizos y entradas con Area Targets de Vuforia, que requieren escanear el lugar.
