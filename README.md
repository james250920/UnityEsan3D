# UnityEsan3D

Guía en realidad aumentada del campus de ESAN para Android, hecha con Unity y Vuforia.
La app lleva hasta el evento de ESAN: muestra sobre la cámara el camino hacia el evento, reconoce marcadores (la
pileta) para mostrar a Chuwi e incluye el mapa 3D del campus hecho en Unity, con la ruta, el evento y tu ubicación.
No depende del mapa web.

De qué trata el proyecto, cómo se usa y en qué estado está: [`docs/PROYECTO.md`](docs/PROYECTO.md).

## Requisitos

- Unity `6000.6.0f1` con el módulo de Android.
- Vuforia Engine `11.4.4`.
- Un celular con Android 10 o superior y la depuración USB activada.
- .NET 8 para las pruebas, y Node.js solo si hay que regenerar los resultados esperados.

## Puesta en marcha

1. Clona el repositorio:

   ```
   git clone https://github.com/james250920/UnityEsan3D.git
   ```

2. Descarga Vuforia Engine 11.4.4 desde [developer.vuforia.com](https://developer.vuforia.com/downloads/sdk) y
   copia `com.ptc.vuforia.engine-11.4.4.tgz` en `Packages/`. No está en el repositorio porque pesa 131 MB y GitHub
   no admite archivos de más de 100 MB.

3. Abre la carpeta del proyecto con Unity Hub.

## Compilar e instalar en Android

El modo Play de Vuforia no funciona en el editor de Linux: todo se prueba en el celular.

- Con Unity abierto: menú `ESAN > Compilar y ejecutar en Android`.
- Con Unity cerrado:

  ```
  Unity -batchmode -projectPath <proyecto> -buildTarget Android -executeMethod Esan.Editor.EsanAndroid.CompilarYEjecutar -logFile <registro>
  ```

El menú prepara `Main scene`, compila en modo desarrollo e instala `Builds/ProyectoEsan.apk` en el celular
conectado. La app se instala como `com.esan.campusra`.

### Simular una ubicación

Solo en compilaciones de desarrollo, para probar sin estar en el campus:

```
adb shell am force-stop com.esan.campusra
adb shell am start -n com.esan.campusra/com.unity3d.player.UnityPlayerGameActivity -e esan_sim "-76.9612,-12.1050,0"
```

Los valores son longitud, latitud y rumbo (0 = norte).

## Estructura

| Ruta | Contenido |
| --- | --- |
| `Assets/Esan/Scripts/Core/` | Coordenadas, rutas con A\* y progreso sobre la ruta. No dependen de Unity. |
| `Assets/Esan/Scripts/` | Datos, interfaz, GPS, orientación, guía sobre el suelo y marcadores. |
| `Assets/Esan/Resources/` | Grafo de caminos, eventos, perímetro y calibración del campus, fotos de los marcadores y modelos de Chuwi y del campus. |
| `Assets/Esan/Editor/` | Menú de compilación para Android. |
| `Assets/Scenes/Main scene.unity` | La única escena. |
| `PruebasCore/` | Pruebas del código de `Core` fuera de Unity. |

El detalle de cada script, la orientación, la guía y lo que queda pendiente está en
[`Assets/Esan/LEEME.md`](Assets/Esan/LEEME.md).

## Pruebas

`PruebasCore` compara el código C# de `Core` con los resultados del mapa web guardados en `esperado.json`:

```
cd PruebasCore
dotnet run
```

Si cambia el grafo o la lógica en la web, copia los JSON a `Assets/Esan/Resources/Datos/` y regenera lo esperado:

```
node generar-esperado.mjs "<ruta a esan-mapa/dist>"
```
