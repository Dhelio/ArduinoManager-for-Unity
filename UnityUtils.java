package somepackage.unityutils;

import android.app.Activity;
import android.app.ActivityManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.wifi.WifiManager;
import android.os.Debug;
import android.provider.Settings;
import android.util.Log;
import android.widget.Toast;

import java.io.DataOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.List;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

import static android.content.Context.ACTIVITY_SERVICE;

public class UnityUtils {

    private static String TAG = "UnityUtilsJava";

    //---------------------- GENERAL

    public void Reboot() {
        try {
            Runtime.getRuntime().exec(new String[]{"/system/bin/su","-c","reboot now"});
        } catch (IOException e) {
            Log.e(TAG,"Errore durante l'esecuzione del comando di riavvio: ["+e+"]");
        }
    }

    public void OpenSettings(Activity UnityActivity) {
        Intent intent = new Intent(android.provider.Settings.ACTION_SETTINGS);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        UnityActivity.startActivity(intent);
    }

    public void OpenWiFiSettings(Activity UnityActivity) {
        Intent intent = new Intent(Settings.ACTION_WIFI_SETTINGS);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        UnityActivity.startActivity(intent);
    }

    public String getWiFiMacAddress(Activity UnityActivity) {
        WifiManager wifiManager = (WifiManager) UnityActivity.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
        return wifiManager.getConnectionInfo().getMacAddress();
    }

    public void QuitForegroundApp(Activity UnityActivity) {
        //Ottiene il nome del pkg in foreground
        ActivityManager am = (ActivityManager) UnityActivity.getSystemService(ACTIVITY_SERVICE);
        List<ActivityManager.RunningTaskInfo> taskInfo = am.getRunningTasks(1);
        ComponentName componentInfo = taskInfo.get(0).topActivity;
        Log.i(TAG,"Tento la chiusura di ["+componentInfo.getPackageName()+"]");

        //Chiude l'app in foreground
        String cmd = "am force-stop " + componentInfo.getPackageName();
        Process p;
        try {
            p = Runtime.getRuntime().exec("su");
            DataOutputStream os = new DataOutputStream(p.getOutputStream());
            os.writeBytes(cmd + "\n");
            os.writeBytes("exit\n");
            os.flush();
        } catch (IOException e) {
            Log.e(TAG,"Errore durante la chiusura dell'app in foreground: ["+e+"]");
        }
    }

    public String getForegroundPkgName (Activity UnityActivity) {
        //Ottiene il nome del pkg in foreground
        ActivityManager am = (ActivityManager) UnityActivity.getSystemService(ACTIVITY_SERVICE);
        List<ActivityManager.RunningTaskInfo> taskInfo = am.getRunningTasks(1);
        ComponentName componentInfo = taskInfo.get(0).topActivity;
        return componentInfo.getPackageName();
    }

    public void LaunchApp (Activity UnityActivity, String PackageName) {
        UnityActivity.startActivity(new Intent(UnityActivity.getPackageManager().getLaunchIntentForPackage(PackageName)));
    }

    public void ShowShortToast (Activity UnityActivity, String Content) {
        Toast.makeText(UnityActivity, Content, Toast.LENGTH_SHORT).show();
    }

    public void ShowLongToast (Activity UnityActivity, String Content) {
        Toast.makeText(UnityActivity, Content, Toast.LENGTH_LONG).show();
    }

    public void SetShowTouches(boolean Value, Activity UnityActivity) {
        if (Value) {
            Settings.System.putInt(UnityActivity.getApplicationContext().getContentResolver(), "show_touches",1);
        } else {
            Settings.System.putInt(UnityActivity.getApplicationContext().getContentResolver(), "show_touches",0);
        }
    }

    public void Unzip (String Source, String Destination) {
        File dir = new File(Destination);
        //crea la directory se non esiste
        if(!dir.exists()) dir.mkdirs();
        FileInputStream fis;
        //Buffer dove leggere i byte
        byte[] buffer = new byte[1024];
        try {
            fis = new FileInputStream(Source);
            ZipInputStream zis = new ZipInputStream(fis);
            ZipEntry ze = zis.getNextEntry();
            while(ze != null){
                File newFile = new File(Destination);
                //Unizippo verso la directory
                //Crea eventuali subdirectory necessarie
                new File(newFile.getParent()).mkdirs();
                FileOutputStream fos = new FileOutputStream(newFile);
                int len;
                while ((len = zis.read(buffer)) > 0) {
                    fos.write(buffer, 0, len);
                }
                fos.close();
                //Chiude zipentry
                zis.closeEntry();
                ze = zis.getNextEntry();
            }
            //Chiude l'ultima zipentry
            zis.closeEntry();
            zis.close();
            fis.close();
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public void UninstallPkg(String PkgName) {
        String tag = "UU>Uninstall";
        Process p;
        try {
            p = Runtime.getRuntime().exec(new String[] {"su","-c","pm uninstall "+PkgName});
            p.waitFor();
        } catch (IOException e) {
            Log.e(tag,"Errore fatale durante l'esecuzione dell'exec uninstall ["+e+"]");
        } catch (InterruptedException ie) {
            Log.e(tag,"Errore fatale durante il waitfor o lo sleep uninstall ["+ie+"]");
        }
    }

    public boolean InstallPkg(String Source) {
        String tag = "UU>Install";
        Process p;
        try {
            p = Runtime.getRuntime().exec(new String[] {"su","-c","pm install -r "+Source});
            p.waitFor();
        } catch (IOException e) {
            Log.e(tag,"Errore fatale durante l'esecuzione dell'exec install ["+e+"]");
            return false;
        } catch (InterruptedException ie) {
            Log.e(tag,"Errore fatale durante il waitfor o lo sleep install ["+ie+"]");
            return false;
        }
        return true;
    }

}
