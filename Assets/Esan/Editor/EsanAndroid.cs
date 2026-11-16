// Menú ESAN > Compilar y ejecutar en Android: deja la escena lista, compila e instala en el celular conectado.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace Esan.Editor
{
    public static class EsanAndroid
    {
        const string Escena = "Assets/Scenes/Main scene.unity";
        const string Apk = "Builds/ProyectoEsan.apk";
        const string Identificador = "com.esan.campusra";

        [MenuItem("ESAN/Compilar y ejecutar en Android")]
        public static void CompilarYEjecutar()
        {
            bool listo = Compilar();
            // También se puede lanzar sin abrir el editor: Unity -batchmode -executeMethod Esan.Editor.EsanAndroid.CompilarYEjecutar
            if (Application.isBatchMode) EditorApplication.Exit(listo ? 0 : 1);
        }

        static bool Compilar()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("ESAN", "Falta el módulo de Android. Instálalo desde Unity Hub.", "Entendido");
                return false;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            var escena = EditorSceneManager.OpenScene(Escena);

            // Vuforia usa su ARCamera: una segunda cámara taparía el vídeo.
            var camaras = new List<Camera>();
            foreach (GameObject raiz in escena.GetRootGameObjects()) camaras.AddRange(raiz.GetComponentsInChildren<Camera>(true));
            if (!camaras.Exists(camara => camara.GetComponent<VuforiaBehaviour>() != null))
            {
                EditorUtility.DisplayDialog("ESAN", "La escena no tiene la ARCamera de Vuforia. Añádela con GameObject > Vuforia Engine > AR Camera.", "Entendido");
                return false;
            }
            foreach (Camera camara in camaras)
                if (camara.GetComponent<VuforiaBehaviour>() == null)
                {
                    Debug.Log("ESAN: se quitó la cámara sobrante «" + camara.name + "».");
                    Object.DestroyImmediate(camara.gameObject);
                    EditorSceneManager.MarkSceneDirty(escena);
                }
            EditorSceneManager.SaveScene(escena);

            // El identificador de la plantilla no sirve para instalar una app propia.
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android).Contains("template"))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, Identificador);

            // Vuforia 11 no compila con menos de Android 10.
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

            BuildReport informe = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Escena },
                locationPathName = Apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AutoRunPlayer
            });
            if (informe.summary.result == BuildResult.Succeeded) Debug.Log($"ESAN: instalado en el celular. APK en {Apk}, {informe.summary.totalSize / 1048576} MB.");
            else Debug.LogError($"ESAN: la compilación terminó con {informe.summary.result} y {informe.summary.totalErrors} errores. Revisa la consola.");
            return informe.summary.result == BuildResult.Succeeded;
        }
    }
}
