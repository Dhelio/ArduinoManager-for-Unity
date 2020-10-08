//#define IS_HOME //Decommentare questa stringa se si sta programmando da casa

using UnityEngine;
using System.IO;
using System.Threading;
using System;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Cysharp.Threading.Tasks;

/// <summary>
/// Classe che contiene un po' di utilità utilizzate in tutti i progetti, come percorso base per l'applicazione, funzioni da unityutils, etc.
/// </summary>

public class Utilities : Application {

    //Costanti
    private static readonly AndroidJavaObject ajo = new AndroidJavaObject("biz.replay.unityutils.UnityUtils");
    public static volatile bool isDebug = (Debug.isDebugBuild || isEditor);

    public const string AppName = "AmazingApp";

    //Percorsi
    private const string androidBaseApplicationDir = @"sdcard/REPLAYSRL/" + AppName + "/";
    private const string officeBaseApplicationDir = @"D:/ProgettiAlessandro/tmp/REPLAYSRL/" + AppName + "/";
    private const string homeBaseApplicationDir = @"Q:/tmp/REPLAYSRL/" + AppName + "/";
#if UNITY_EDITOR && IS_HOME
    public const string BaseApplicationDir = homeBaseApplicationDir;
#elif UNITY_EDITOR && !IS_HOME
    public const string BaseApplicationDir = officeBaseApplicationDir;
#else
    public const string BaseApplicationDir = androidBaseApplicationDir;
#endif
    public const string BaseApplicationTempDir = BaseApplicationDir + "Temp/";

    /// <summary>
    /// Thread safe Log warning
    /// </summary>
    public static void LogW(string tag, string log) {
        if (!isEditor)
            Console.WriteLine("-" + tag + "- [WARNING] " + log);
        else
            Debug.LogWarning("-" + tag + "- [WARNING] " + log);
    }

    /// <summary>
    /// Thread safe Log debug
    /// </summary>
    public static void LogD(string tag, string log) {
        if (isDebug) {
            if (!isEditor)
                Console.WriteLine("-" + tag + "- [DEBUG]" + log);
            else
                Debug.Log("-" + tag + "- [DEBUG]" + log);
        }
    }

    /// <summary>
    /// Thread safe Log Error
    /// </summary>
    public static void LogE(string tag, string log) {
        if (!isEditor)
            Console.WriteLine("-" + tag + "- [ERROR]" + log);
        else
            Debug.LogError("-" + tag + "- [ERROR]" + log);
    }

    /// <summary>
    /// Thread safe Log
    /// </summary>
    public static void Log(string tag, string log) {
        if (!isEditor)
            Console.WriteLine("-" + tag + "- " + log);
        else
            Debug.Log("-" + tag + "- " + log);
    }

    /// <summary>
    /// Chiama la funzione dalla libreria aar "unityutils" per riavviare il dispositivo (necessita del root)
    /// </summary>
    public static void Reboot() {
        if (!isEditor)
            ajo.Call("Reboot");
    }

    /// <summary>
    /// Chiama la funzione dalla libreria aar "unityutils" per riavviare il dispositivo (necessita del root)
    /// </summary>
    /// <param name="Delay">dopo quanti millisecondi deve essere riavviato il dispositivo</param>
    public static void Reboot(int Delay) {
        if (!isEditor) {
            new System.Threading.Thread(() => {
                AndroidJNI.AttachCurrentThread();
                Thread.Sleep(Delay);
                ajo.Call("Reboot");
            }).Start();
        }
    }

    /// <summary>
    /// Lancia un'altra app installata sul dispositivo.
    /// </summary>
    /// <param name="PackageName">Il nome del pacchetto (es. biz.replay.kikilauncher)</param>
    public static void LaunchApp(string PackageName) {
        if (!isEditor) {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject androidPackageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager");
            AndroidJavaObject launchIntent = androidPackageManager.Call<AndroidJavaObject>("getLaunchIntentForPackage", PackageName);
            currentActivity.Call("startActivity", launchIntent);
            unityPlayer.Dispose();
            currentActivity.Dispose();
            androidPackageManager.Dispose();
            launchIntent.Dispose();
        }
    }

    /// <summary>
    /// Ottiene l'attività dello UnityPlayer sotto forma di Java Object
    /// </summary>
    public static AndroidJavaObject getUnityActivity() {
        if (!isEditor) {
            AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
            return unityActivity;
        }
        return null;
    }

    /// <summary>
    /// Ottiene il mac address dell'interfaccia WLAN0-UP di android.
    /// </summary>
    public static string getWiFiMacAddress() {
        if (!isEditor)
            return ajo.Call<string>("getWiFiMacAddress", Utilities.getUnityActivity());
        return null;
    }

    /// <summary>
    /// Apre le impostazioni di Android
    /// </summary>
    public static void OpenAndroidSettings() {
        if (!isEditor)
            ajo.Call("OpenSettings", Utilities.getUnityActivity());
    }

    /// <summary>
    /// Apre le impostazioni del wifi di android
    /// </summary>
    public static void OpenAndroidWiFiSettings() {
        if (!isEditor)
            ajo.Call("OpenWiFiSettings", Utilities.getUnityActivity());
    }

    /// <summary>
    /// Ottiene il package name dell'applicazione in esecuzione davanti a tutte le altre.
    /// </summary>
    public static string getForegroundPkgName() {
        if (!isEditor)
            return ajo.Call<string>("getForegroundPkgName", Utilities.getUnityActivity());
        return null;
    }

    /// <summary>
    /// Esce dalla applicazione correntemente in esecuzione davanti a tutte le altre
    /// </summary>
    public static void QuitForegroundApp() {
        if (!isEditor)
            ajo.Call("QuitForegroundApp", Utilities.getUnityActivity());
    }

    /// <summary>
    /// Mostra un toast di breve durata.
    /// </summary>
    /// <param name="Content">Il testo da mostrare</param>
    public static void ShowShortToast(string Content) {
        if (!isEditor)
            ajo.Call("ShowShortToast", new object[] { Utilities.getUnityActivity(), Content });
    }

    /// <summary>
    /// Mostra un toast di lunga durata.
    /// </summary>
    /// <param name="Content">Il testo da mostrare</param>
    public static void ShowLongToast(string Content) {
        if (!isEditor)
            ajo.Call("ShowLongToast", new object[] { Utilities.getUnityActivity(), Content });
    }

    /// <summary>
    /// Imposta la visibilità dei tocchi (l'opzione developer)
    /// </summary>
    public static void SetShowTouches(bool Value) {
        if (!isEditor)
            ajo.Call("SetShowTouches", new object[] { Value, Utilities.getUnityActivity() });
    }

    /// <summary>
    /// Converte un file immagine a texture2d di unity
    /// </summary>
    /// <param name="FilePath">Il percorso verso il file</param>
    public static Texture2D LoadPNG(string FilePath) {
        if (File.Exists(FilePath)) {
            Texture2D texture = null;
            byte[] fileData = File.ReadAllBytes(FilePath);
            texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }
        return null;
    }

    public static bool InstallPkg(string Source) {
        if (!isEditor)
            return ajo.Call<bool>("InstallPkg", Source);
        else
            LogD("Utilities", "Installato pacchetto "+Source);
        return false;
    }

    public static void UninstallPkg(string PkgName) {
        if (!isEditor)
            ajo.Call("UninstallPkg", PkgName);
    }

    /// <summary>
    /// Scarica un file.
    /// </summary>
    /// <param name="Url">L'Url da dove prelevare il file</param>
    /// <param name="Destination">La destinazione locale dove salvarlo</param>
    /// <param name="WriteOnBuffer">Se deve scaricare i dati (alcune estensioni hanno problemi con downloadfile)</param>
    public static void DownloadFile(string Url, string Destination, bool WriteOnBuffer = false) {
        try {
            LogD("Utils", "Url passato[" + Url + "], Destinazione [" + Destination + "]");
            if (WriteOnBuffer) {
                System.Net.WebClient webClient = new System.Net.WebClient();
                byte[] buffer = webClient.DownloadData(Url);
                File.WriteAllBytes(Destination, buffer);
                webClient.Dispose();
            } else {
                System.Net.WebClient webClient = new System.Net.WebClient();
                webClient.DownloadFile(Url, Destination);
                webClient.Dispose();
            }
        } catch (Exception e) {
            LogE("Utilities", "Errore DownloadFile: [" + e + "]");
        }
    }

    /// <summary>
    /// Scarica un file in asincrono.
    /// </summary>
    /// <param name="Url">L'Url da dove prelevare il file</param>
    /// <param name="Destination">La destinazione locale dove salvarlo</param>
    /// <param name="WriteOnBuffer">Se deve scaricare i dati (alcune estensioni hanno problemi con downloadfile)</param>
    public static async UniTask<bool> DownloadFileAsync(string Url, string Destination, bool WriteOnBuffer = false) {
        try {
            if (WriteOnBuffer) {
                System.Net.WebClient webClient = new System.Net.WebClient();
                byte[] buffer = await webClient.DownloadDataTaskAsync(Url);
                File.WriteAllBytes(Destination, buffer);
                webClient.Dispose();
            } else {
                System.Net.WebClient webClient = new System.Net.WebClient();
                await webClient.DownloadFileTaskAsync(Url, Destination);
                webClient.Dispose();
            }
        } catch (Exception e) {
            LogE("Utilities", "Errore DownloadFile: [" + e + "]");
            return false;
        }
        return true;
    }

    public static void Extract(string Source, string Destination) {
        if (Source.EndsWith(".zip")) {
            LogD("Utilities","Estrazione dell'archivio ["+Source+"] verso ["+Destination+"] in corso...");
            try {
                using (FileStream fs = new FileStream(Source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    ZipFile zf = new ZipFile(fs);
                    foreach (ZipEntry ze in zf) {
                        if (!ze.IsDirectory) {
                            using (FileStream ffs = File.Create(Destination + ze.Name)) {
                                StreamUtils.Copy(zf.GetInputStream(ze), ffs, new byte[4096]);
                                ffs.Close();
                                LogD("Utilities", "Estratto [" + ze.Name + "]");
                            }
                        }
                    }
                    zf.Close();
                    fs.Close();
                }
            } catch (Exception e) {
                LogE("Utilities", "Errore Extract: errore fatale durante l'estrazione. [" + e + "]");
            }
        } else {
            LogE("Utilities", "Errore Extract: estensione sconosciuta.");
        }
    }

    public static async UniTask<bool> ExtractAsync(string Source, string Destination) {
        await System.Threading.Tasks.Task.Run(() => { Extract(Source, Destination); });
        return true;
    }

    public static void Compress(string Source, string Destination) {
        throw new NotImplementedException();
    }
}
