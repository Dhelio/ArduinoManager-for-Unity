using System;
using System.IO;
using System.IO.Ports; //Questa libreria di .NET richiede di utilizzare la versione 4.x di .NET in Unity, e non il suo subset 2.x!
using System.Threading;

/// <summary>
/// Classe per la gestione della comunicazione di Unity con Arduino.
/// </summary>

namespace REPLAYSRL.IO {

    public static class ArduinoManager {

        //---------------------------------------------------------------------------------- CLASSI

        /// <summary>
        /// Personalizzazione della classe serial port.
        /// </summary>
        /// <description>
        /// Siccome la classe SerialPort risale all'età della pietra, quando le porte seriali erano fissate sui computer e non si potevano disconnettere, 
        /// non esiste un modo per capire se il dispositivo dall'altra parte è stato disconnesso all'improvviso.
        /// Il modo in cui SerialPort funziona è creando, quando si fa Open, un worker thread che controlla diversi eventi (DataReceived, ErrorReceived,...)
        /// Se il dispositivo dall'altra parte viene disconnesso o muore questo ha il risultato di far venire un coccolone al thread (che crasha), e siccome
        /// è un thread non si può chiudere dentro un try/catch.
        /// Obbligando il Garbage collector a NON utilizzare il finalizzatore (l'analogo in C# del distruttore di C++) quando si apre la porta ed
        /// a registrarlo quando si chiude la porta, si può obbligare lo stesso a lanciare una eccezione quando non vede più l'accesso a BaseStream.
        /// Quindi quando si prende questa eccezione, IN TEORIA, basterebbe chiudere la porta, farne il Dispose, settare a null e tentare disperatamente
        /// di riaprire la connessione.
        /// </description>
        class BetterSerialPort : SerialPort {

            private Stream baseStream;

            /// <summary>
            /// Crea una nuova istanza di BetterSerialPort
            /// </summary>
            /// <param name="PortName">Il nome della porta (COM su Windows, ttyACM su Android & Ubuntu)</param>
            /// <param name="Baudrate">Il baudrate a cui inizializzare la comunicazione</param>
            public BetterSerialPort(string PortName, int Baudrate) : base(PortName, Baudrate) { }

            /// <summary>
            /// Apre una connessione sulla porta corrente senza inizializzarne il finalizzatore
            /// </summary>
            public new void Open() {
                try {
                    if (!base.IsOpen) {
                        base.Open();
                        baseStream = BaseStream;
                        GC.SuppressFinalize(this.BaseStream);
                    }
                } catch (Exception e) {
                    Utilities.LogD(TAG,"Errore durante l'apertura della porta [" + e + "]");
                }
            }

            /// <summary>
            /// Flush dello BaseStream per eventuali \n rimasti in memoria
            /// </summary>
            public void Clear() {
                try {
                    if (base.IsOpen) {
                        baseStream.Flush();
                    }
                } catch (Exception e) {
                    Utilities.LogE(TAG,"Clear error ["+e+"]");
                }
            }

            /// <summary>
            /// Chiude la porta ed avvia il garbage collector sullo stream
            /// </summary>
            public new void Close() {
                if (base.IsOpen) {
                    GC.ReRegisterForFinalize(this.BaseStream);
                    base.Close();
                }
            }

            /// <summary>
            /// Si libera della porta
            /// </summary>
            public new void Dispose() {
                Dispose(true);
            }

            protected override void Dispose(bool disposing) {

                //Chiama il metodo base di Dispose per il Container
                if (disposing && (base.Container != null)) {
                    base.Container.Dispose();
                }
                //L'atto di chiudere lo stream per una porta già chiusa lancia una eccezione
                try {
                    if (baseStream != null && baseStream.CanRead) {
                        baseStream.Close();
                        GC.ReRegisterForFinalize(baseStream);
                    }
                } catch (NullReferenceException e) {
                    Utilities.LogD(TAG,"Bug con la chiusura della porta [" + e + "]");
                } catch (Exception e) {
                    Utilities.LogD(TAG,"Bug con la chiusura della porta [" + e + "]");
                }
                base.Dispose(disposing);
            }
        }

        //---------------------------------------------------------------------------------- VARIABILI PRIVATE

        private const string TAG = "ArduinoManager";

        private static readonly int baudrate = 9600;
        private static readonly int timeout = -1;

        private static BetterSerialPort seriale;
        private static System.Collections.Concurrent.ConcurrentQueue<string> outgoingQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private static Thread threadedConnection;

        //Variabili di controllo
        private static volatile uint coins;

        //---------------------------------------------------------------------------------- VARIABILI PUBBLICHE

        /// <summary>
        /// Se ha avviato o no il thread per la connessione
        /// </summary>
        public static bool has_Started { get; private set; }
        /// <summary>
        /// Se ha correntemente dei gettoni in memoria
        /// </summary>
        public static bool has_Coins { get; private set; }
        /// <summary>
        /// Se il thread è connesso correntemente con Arduino
        /// </summary>
        public static bool has_Connected { get; private set; }

        //---------------------------------------------------------------------------------- FUNZIONI PRIVATE

        /// <summary>
        /// DEPRECATA. Funzione che viene chiamata dall'evento SerialDataReceivedEvent. NON FUNZIONA CON UNITY PER UN BUG NOTO LORO
        /// </summary>
        private static void ParseData(object sender, SerialDataReceivedEventArgs e) {
            string data = ((BetterSerialPort)sender).ReadExisting();
            Utilities.LogD(TAG,"Data Received: [" + data + "]");
            if (data.Contains("g")) {
                coins++;
            }
            
        }

        /// <summary>
        /// Fase di connessione.
        /// </summary>
        private static void ThreadedConnection() {
            Utilities.LogD(TAG,"In attesa di connessione...");
            do {
                has_Connected = false;
                while (!has_Connected) {
                    string[] porte = SerialPort.GetPortNames(); //Ottiene la lista dei nomi delle porte correntemente utilizzate dal sistema operativo
                    for (int i = 0; i < porte.Length; i++) {
                        Utilities.LogD(TAG, "Presente porta: " + porte[i]);
                    }
                    for (int i = 0; i < porte.Length; i++) { //Scorre le porte alla ricerca di quella giusta
                        if (porte != null)
                            if (porte[i].Contains("ACM") || porte[i].Contains("COM")) { //Arduino usa l'abstract control model (ACM), per cui in linux based os le porte di arduino iniziano sempre per ACM, non per COM.
                                seriale = new BetterSerialPort(porte[i], baudrate); //Inizializza la porta scelta al baudrate scelto
                                seriale.ReadTimeout = timeout; //setta il timeout, cioé la quantità di tempo dopo il quale, se non si riceve risposta, la connessione al dispositivo viene annullata.
                                seriale.Handshake = Handshake.None; //Nessaun handshaking col dispositivo perché ne faccio uno manuale.
                                seriale.DtrEnable = true; //Abilito il data terminal ready (DTR) per segnalare ad arduino che il PC è pronto a comunicare.
                                seriale.Encoding = System.Text.Encoding.ASCII;
                                //seriale.DataReceived += new SerialDataReceivedEventHandler(ParseData); //Event listener NON FUNZIONA CON UNITY (BUG NOTO)
                                if (seriale.IsOpen) {
                                    Utilities.LogD(TAG, "Impossibile aprire arduino: seriale già aperto!");
                                    seriale.Close();
                                } else {
                                    try {
                                        seriale.Open(); //Apro la comunicazione col seriale
                                        Utilities.LogD(TAG, "Connesso alla porta [" + porte[i] + "]");
                                        i = porte.Length;
                                        has_Connected = true;
                                    } catch (UnauthorizedAccessException uae) {
                                        Utilities.LogD(TAG, "Accesso non autorizzato alla porta [" + porte[i] + "] -> [" + uae + "]");
                                    } catch (ArgumentOutOfRangeException aoore) {
                                        Utilities.LogD(TAG, "Argument out of range alla porta [" + porte[i] + "] -> [" + aoore + "]");
                                    } catch (IOException ioe) {
                                        Utilities.LogD(TAG, "IO exception alla porta [" + porte[i] + "] -> [" + ioe + "]");
                                    } catch (IndexOutOfRangeException ioore) {
                                        Utilities.LogD(TAG, "Index out of range -> [" + ioore + "]");
                                    }
                                }
                            }
                        Thread.Sleep(100);
                    }
                }
                while (seriale != null && seriale.IsOpen) {
                    //IN
                    string incoming = seriale.ReadLine();
                    if (incoming.Length > 0 && incoming != null) {
                        Utilities.LogD(TAG, "Ricevuto messaggio da arduino ["+incoming+"]");
                        if (incoming.Contains("g")) {
                            coins++;
                            has_Coins = true;
                        }
                    }
                    //OUT ASYNC
                    if (outgoingQueue.TryDequeue(out string msg))
                        if (msg != "" && msg != null) {
                            Utilities.LogD(TAG, "Invio messaggio ad arduino [" + msg + "]");
                            try {
                                seriale.Write(msg);
                                seriale.Clear();
                            } catch (InvalidOperationException e) {
                                Utilities.LogE(TAG, "Operazione non valida [" + e + "]");
                            } catch (ArgumentNullException e) {
                                Utilities.LogE(TAG, "Argomento nullo [" + e + "]");
                            } catch (TimeoutException e) {
                                Utilities.LogE(TAG, "Timeout [" + e + "]");
                            }
                        }
                    Thread.Sleep(5); //Giusto per non schiattare la board. Tanto 5 ms non si sentono.
                }
                //seriale.Close();
                //seriale.Dispose();
                has_Connected = false;
                Utilities.LogD(TAG,"arduino disconesso, tento la riconnessione...");
            } while (true);
        }

        //---------------------------------------------------------------------------------- FUNZIONI PUBBLICHE

        /// <summary>
        /// Invia dal thread corrente un comando ad Arduino
        /// </summary>
        /// <param name="Value">Il comando da inviare</param>
        public static void Send(string Value) {
            if (!has_Started || !has_Connected) {
                Utilities.LogE(TAG, "Errore: tentato di inviare il comando [" + Value + "] ad arduino senza avere la connessione.");
                return;
            }
            seriale.Write(Value);
        }

        /// <summary>
        /// Invia dal thread corrente un comando ad Arduino
        /// </summary>
        /// <param name="Value">Il comando da inviare</param>
        public static void Send(int Value) {
            if (!has_Started || !has_Connected) {
                Utilities.LogE(TAG, "Errore: tentato di inviare il comando [" + Value + "] ad arduino senza avere la connessione.");
                return;
            }
            seriale.Write(Value.ToString());
        }

        /// <summary>
        /// Invia dal thread dell'ArduinoManager un comando ad Arduino in asincrono.
        /// </summary>
        /// <param name="Value">Il comando da inviare</param>
        public static void SendAsync(string Value) { outgoingQueue.Enqueue(Value); }

        /// <summary>
        /// Invia dal thread dell'ArduinoManager un comando ad Arduino in asincrono.
        /// </summary>
        /// <param name="Value">Il comando da inviare</param>
        public static void SendAsync(int Value) { outgoingQueue.Enqueue(Value.ToString()); }


        /// <summary>
        /// Avvia la connessione ad Arduino tramite thread.
        /// </summary>
        public static void Start() {
            threadedConnection = new Thread(new ThreadStart(ThreadedConnection));
            threadedConnection.Start();
            has_Started = true;
        }

        /// <summary>
        /// Ferma la connessione ad Arduino.
        /// </summary>
        public static void Stop() {
            threadedConnection.Abort();
            seriale.Close();
            has_Started = false;
        }
    }

}
