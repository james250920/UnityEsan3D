// Sprites generados por código para la interfaz: paneles redondeados e iconos de trazo.
using System.Collections.Generic;
using UnityEngine;

namespace Esan
{
    public static class EsanDibujo
    {
        const int Escala = 3;  // Píxeles de textura por unidad de la interfaz.
        static readonly Dictionary<int, Sprite> redondeados = new Dictionary<int, Sprite>();
        static readonly Dictionary<string, Sprite> iconos = new Dictionary<string, Sprite>();

        // Trazos en una cuadrícula de 24, con el origen arriba a la izquierda como en los SVG de la web.
        public static readonly float[] Flecha = { 12, 20, 12, 4, 6, 10, 12, 4, 12, 4, 18, 10 };
        public static readonly float[] Cerrar = { 6, 6, 18, 18, 18, 6, 6, 18 };
        public static readonly float[] Visto = { 5, 12.5f, 9.5f, 17, 9.5f, 17, 19, 7.5f };

        /// <summary>Rectángulo blanco de esquinas redondeadas, para Image.Type.Sliced.</summary>
        public static Sprite Redondeado(int radio)
        {
            if (redondeados.TryGetValue(radio, out Sprite listo)) return listo;
            int r = radio * Escala, lado = 2 * r + 2;
            var pixeles = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - .5f, x + .5f - (lado - r))), dy = Mathf.Max(0, Mathf.Max(r - y - .5f, y + .5f - (lado - r)));
                    float alfa = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                    pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
                }
            Sprite sprite = Sprite.Create(Textura(lado, pixeles), new Rect(0, 0, lado, lado), new Vector2(.5f, .5f), 100 * Escala, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return redondeados[radio] = sprite;
        }

        /// <summary>Icono blanco de trazo redondeado. trazos: x1, y1, x2, y2 por cada segmento.</summary>
        public static Sprite Icono(string nombre, float[] trazos, float grosor = 2.4f)
        {
            if (iconos.TryGetValue(nombre, out Sprite listo)) return listo;
            const int lado = 96;
            float unidad = lado / 24f;
            var pixeles = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float px = (x + .5f) / unidad, py = 24 - (y + .5f) / unidad, distancia = float.MaxValue;
                    for (int i = 0; i < trazos.Length; i += 4)
                    {
                        float ax = trazos[i], ay = trazos[i + 1], bx = trazos[i + 2] - ax, by = trazos[i + 3] - ay;
                        float t = Mathf.Clamp01(((px - ax) * bx + (py - ay) * by) / (bx * bx + by * by));
                        distancia = Mathf.Min(distancia, Mathf.Sqrt((px - ax - t * bx) * (px - ax - t * bx) + (py - ay - t * by) * (py - ay - t * by)));
                    }
                    float alfa = Mathf.Clamp01((grosor / 2 - distancia) * unidad + .5f);
                    pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
                }
            return iconos[nombre] = Sprite.Create(Textura(lado, pixeles), new Rect(0, 0, lado, lado), new Vector2(.5f, .5f), 100);
        }

        static Texture2D Textura(int lado, Color32[] pixeles)
        {
            var textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            textura.SetPixels32(pixeles);
            textura.Apply(false, true);
            return textura;
        }
    }
}
