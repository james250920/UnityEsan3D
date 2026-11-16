# ESAN en Unity

Migración de la experiencia inmersiva del mapa web (`esan-mapa`) a Unity con Vuforia, para Android.
El mapa 3D del campus sigue siendo la web.

## Qué hay

| Carpeta | Contenido |
| --- | --- |
| `Resources/Datos/` | Los mismos JSON del mapa web: grafo de caminos, lugares y perímetro (`esan-boundary.json` es `esan-boundary.geojson` con otra extensión). |
| `Resources/Marcadores/` | Fotos de la pileta, `M1` y `M2`. Son JPEG con extensión `.bytes` para que Unity las entregue sin procesar. |
| `Resources/Modelos/chuwi.obj` | Chuwi, convertido de `chuwi.glb`. |
| `Scripts/Core/` | Coordenadas, rutas con A\* y progreso sobre la ruta, portados del JavaScript. No dependen de Unity. |
| `Scripts/EsanDatos.cs` | Lee los JSON y entrega el grafo, los lugares y el perímetro. |
| `Scripts/EsanMarcadores.cs` | Prueba de marcadores: muestra a Chuwi al reconocer la pileta. |
| `Scripts/EsanInterfaz.cs` | La interfaz de la web, construida por código: barra, estado, tarjeta de destino, indicación de giro y lista de lugares. |
| `Scripts/EsanUbicacion.cs` | GPS del celular y ubicación simulada. |
| `Scripts/EsanOrientacion.cs` | Hacia dónde mira la cámara, con los sensores de rotación de Android. Funciona sin giroscopio. |
| `Scripts/EsanGuia.cs` y `Resources/EsanGuia.shader` | Guía azul sobre el suelo y cálculo de la flecha de borde. |
| `Scripts/EsanDibujo.cs` | Paneles redondeados e iconos generados por código. |
| `../Plugins/Android/EsanMapaWeb.java` | Ventana con el mapa web dentro de la app. |
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

## Mapa dentro de la app

«Volver al mapa» abre el mapa web en una ventana de Android encima de la cámara, sin salir de la app
(`Assets/Plugins/Android/EsanMapaWeb.java`). Solo deja navegar por el dominio del mapa, le concede la ubicación si la
app ya la tiene, oculta la entrada inmersiva de la web y pausa Vuforia mientras está abierta. Se cierra con la barra
«Volver a la cámara» o con el botón Atrás.

## Orientación y guía

La guía se dibuja en metros reales y se coloca delante de la cámara con el GPS y la orientación del celular, como
en la web; no usa el seguimiento de Vuforia. La orientación sale del vector de rotación de Android y, en celulares
sin giroscopio como el Galaxy A15, del vector de rotación geomagnético (acelerómetro y magnetómetro), que tiembla
más y por eso se suaviza. La brújula plana de Unity no sirve: falla con el celular en vertical.

## Pendiente

- Probar la guía en el campus: alineación con el camino, temblor del rumbo y efecto de metales cercanos.
- Etiquetas de los lugares en la cámara y modelo anclado a una coordenada.
- Reconocer pasadizos y entradas con Area Targets de Vuforia, que requieren escanear el lugar.
