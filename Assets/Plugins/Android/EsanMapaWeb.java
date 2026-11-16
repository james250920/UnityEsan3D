// Muestra el mapa web del campus dentro de la app, en una ventana propia encima de la cámara.
// Solo deja navegar por el dominio del mapa. La usa EsanInterfaz.cs.
package pe.esan.mapa;

import android.Manifest;
import android.app.Activity;
import android.app.Dialog;
import android.content.DialogInterface;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.net.Uri;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.webkit.GeolocationPermissions;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.LinearLayout;
import android.widget.TextView;

import com.unity3d.player.UnityPlayer;

public final class EsanMapaWeb {
    private static Dialog dialogo;

    private EsanMapaWeb() {}

    // Al cerrarse llama al método MapaCerrado del objeto de Unity indicado.
    public static void abrir(final Activity actividad, final String url, final String dominio, final String objetoUnity) {
        actividad.runOnUiThread(new Runnable() {
            @Override public void run() {
                if (dialogo != null) return;
                final WebView web = new WebView(actividad);
                WebSettings ajustes = web.getSettings();
                ajustes.setJavaScriptEnabled(true);
                ajustes.setDomStorageEnabled(true);
                ajustes.setGeolocationEnabled(true);
                web.setWebViewClient(new WebViewClient() {
                    @Override public boolean shouldOverrideUrlLoading(WebView vista, WebResourceRequest peticion) {
                        // Cualquier enlace fuera del mapa se ignora.
                        return !dominio.equalsIgnoreCase(peticion.getUrl().getHost());
                    }
                    @Override public void onPageFinished(WebView vista, String direccion) {
                        // La experiencia inmersiva ya es esta app: se oculta la entrada de la versión web.
                        vista.evaluateJavascript("(function(){var s=document.createElement('style');s.textContent='.immersive-cta{display:none!important}';document.head.appendChild(s)})()", null);
                    }
                });
                web.setWebChromeClient(new WebChromeClient() {
                    @Override public void onGeolocationPermissionsShowPrompt(String origen, GeolocationPermissions.Callback respuesta) {
                        boolean permitido = dominio.equalsIgnoreCase(Uri.parse(origen).getHost())
                            && actividad.checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED;
                        respuesta.invoke(origen, permitido, false);
                    }
                    @Override public void onPermissionRequest(PermissionRequest peticion) {
                        peticion.deny();  // Cámara y micrófono: el mapa no los necesita.
                    }
                });

                final float dp = actividad.getResources().getDisplayMetrics().density;
                final TextView volver = new TextView(actividad);
                volver.setText("‹  Volver a la cámara");
                volver.setTextColor(Color.WHITE);
                volver.setBackgroundColor(Color.rgb(0x25, 0x3d, 0x32));
                volver.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
                volver.setGravity(Gravity.CENTER_VERTICAL);
                volver.setPadding((int) (16 * dp), 0, (int) (16 * dp), 0);
                volver.setMinHeight((int) (48 * dp));
                volver.setOnClickListener(new View.OnClickListener() {
                    @Override public void onClick(View vista) { cerrar(); }
                });

                final LinearLayout columna = new LinearLayout(actividad);
                columna.setOrientation(LinearLayout.VERTICAL);
                columna.setBackgroundColor(Color.WHITE);
                columna.addView(volver, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
                columna.addView(web, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));

                // La ventana ocupa también la zona de la barra de estado: la barra propia baja lo que esta mide.
                columna.setOnApplyWindowInsetsListener(new View.OnApplyWindowInsetsListener() {
                    @SuppressWarnings("deprecation")
                    @Override public WindowInsets onApplyWindowInsets(View vista, WindowInsets margenes) {
                        int arriba = margenes.getSystemWindowInsetTop();
                        volver.setPadding((int) (16 * dp), arriba, (int) (16 * dp), 0);
                        volver.setMinHeight((int) (48 * dp) + arriba);
                        return margenes;
                    }
                });

                final Dialog ventana = new Dialog(actividad, android.R.style.Theme_DeviceDefault_Light_NoActionBar);
                ventana.setContentView(columna);
                Window marco = ventana.getWindow();
                marco.setLayout(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT);
                // El mapa 3D necesita aceleración gráfica, que la actividad de Unity puede traer desactivada.
                marco.setFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED, WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
                ventana.setOnKeyListener(new DialogInterface.OnKeyListener() {
                    @Override public boolean onKey(DialogInterface origen, int tecla, KeyEvent evento) {
                        if (tecla != KeyEvent.KEYCODE_BACK) return false;
                        if (evento.getAction() == KeyEvent.ACTION_UP) {
                            if (web.canGoBack()) web.goBack(); else ventana.dismiss();
                        }
                        return true;
                    }
                });
                ventana.setOnDismissListener(new DialogInterface.OnDismissListener() {
                    @Override public void onDismiss(DialogInterface origen) {
                        columna.removeView(web);
                        web.destroy();
                        dialogo = null;
                        UnityPlayer.UnitySendMessage(objetoUnity, "MapaCerrado", "");
                    }
                });
                dialogo = ventana;
                ventana.show();
                web.loadUrl(url);
            }
        });
    }

    public static void cerrar() {
        final Dialog ventana = dialogo;
        if (ventana != null) ventana.dismiss();
    }
}
