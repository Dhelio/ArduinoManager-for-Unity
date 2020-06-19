using System.IO.Ports; //This library requires .NET 4.x, not its subset.
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

namespace HARDWARE.IO {

    public class ArduinoManager : MonoBehaviour {

       //---------------------------------------------------------------------------------- PRIVATE VARIABLES

        private ConcurrentQueue<string> queue = new ConcurrentQueue<string>(); //Thread safe Q for the production and consumption of messages
        private SerialPort seriale;
        private Thread connessione = null; //Producer thread
        private Thread inputParser = null; //Consumer thread
        private readonly int baudrate = 9600;
        private readonly int timeout = -1;

        private string currentCommand = "";
        private string outgoing = null;

        //Control Variables
		private volatile bool pin0 = false;
		private volatile bool pin1 = false;
		private volatile bool pin2 = false;
		private volatile bool pin3 = false;
		private volatile bool pin4 = false;
		private volatile bool pin5 = false;
		private volatile bool pin6 = false;
		private volatile bool pin7 = false;
		private volatile bool pin8 = false;
        private volatile uint missedInputs = 0;

#if UNITY_EDITOR 
        [Header("Editor Only Fields")]
        [Tooltip("Just for testing purposes. String should be the COM port where arduino is connected. Default value is COM11 if empty")]
        [SerializeField] private string portaArduino = "";
#endif

        //---------------------------------------------------------------------------------- PUBLIC VARIABLES

        public static ArduinoManager Instance; //Singleton instance

        //---------------------------------------------------------------------------------- FUNZIONI PRIVATE

        /// <summary>
        /// Multithreaded connection phase
        /// </summary>
        private void MultithreadedConnection() {
            bool has_Connected = false;
            do {
                string[] porte = SerialPort.GetPortNames(); //Obtains the ports list from OS
                for (int i = 0; i < porte.Length; i++) { //Go through the port list
#if !UNITY_EDITOR
                    if (porte[i].Contains("ACM")) { //Arduino uses the abstract control model (ACM), so for linux based os ports start with ACM, not COM.
#else
                    if (portaArduino == "") portaArduino = "COM11";
                    if (porte[i].Contains(portaArduino)) { 
#endif
                        seriale = new SerialPort(porte[i], baudrate);
                        seriale.ReadTimeout = timeout;
                        seriale.Handshake = Handshake.None; 
                        seriale.DtrEnable = true; //Enable the data terminal ready (DTR) to signal that the device is ready to communicate.
                        if (seriale.IsOpen) {
                            //@IMPLEMENT exception
                        }
                        else {
                            try {
                                seriale.Open(); 
                                char c = 'R'; //A start char that I send to my arduino to start the loop
                                seriale.Write(c.ToString()); //Write to arduino
                                string ack = seriale.ReadLine(); //Reads from arduino. This call is blocking (thus the thread)
                                if (ack.Contains("ACK")) { //If arduino answers with ACK, then we're talking to the right arduino.
                                    has_Connected = true;
                                    i = porte.Length;
                                }
                            }
                            catch (System.UnauthorizedAccessException uae) {
                                Debug.LogError("UnauthorizedAccessException ["+porte[i]+"] -> [" + uae + "]");
                            }
                            catch (System.ArgumentOutOfRangeException aoore) {
                                Debug.LogError("ArgumentOutOfRangeException [" + porte[i] + "] -> [" + aoore + "]");
                            }
                            catch (System.IO.IOException ioe) {
                                Debug.LogError("IOException [" + porte[i] + "] -> [" + ioe+"]");
                            }
                        }
                    }
                }
            } while (!has_Connected);
            LoopReadWrite();
        }

        /// <summary>
        /// Loop for the reading and writing of data. Multithreaded.
        /// </summary>
        private void LoopReadWrite() {
            for (; ; ) { //ciclo infinito
                currentCommand = seriale.ReadLine(); //Reads from arduino
                if (currentCommand != null) //If command's null then there's something wrong with the connection
                    queue.Enqueue(currentCommand); //--
                if (outgoing != null) { //If there are commands to send...
                    seriale.Write(outgoing); //...do it
                    outgoing = null; //null, just to avoid sending the same data.
                }
                Thread.Sleep(5); //As to not make the slower devices implode
            }
        }

        /// <summary>
        /// Extrapolation function. Multithreaded.
        /// </summary>
        private void MultithreadedParseInput() {
            for (; ; ) {
                string s = null; 
                if (queue.TryDequeue(out s) && s != null) { //we try to dequeue a command, hoping it's not null
                    string[] splittedData = s.Split("|".ToCharArray()); //Splits the incoming string from arduino
                    /* 
						So there are a lot of ways we can structure data from arduino to Unity.
						In this case, I make a simple string containing the boolean values of the pins. Example:
						0|1|0|0|0|1|.....
						Where
						pin0|pin1|pin2|.....
						the char | acts as a separator. Not the best way, obvs, but it's just for showing. You can optimize this to your liking.
					*/

                    //Calcolo dell'ascissa del mirino
                    string[] splittedXData = splittedData[0].Split('.');
                    if (splittedXData.Length > 1) { //Necessario perché nei primi comandi a volte può ritornare NULL prima o dopo la virgola, non so perché
                        float.TryParse(splittedXData[0], out x);
                        float xVirgola = .0f;
                        float.TryParse(splittedXData[1], out (xVirgola));
                        x += xVirgola / 100;
                    }

                    //Calcolo dell'ordinata del mirino
                    string[] splittedYData = splittedData[1].Split('.');
                    if (splittedYData.Length > 1) { //Necessario perché nei primi comandi a volte può ritornare NULL prima o dopo la virgola, non so perché
                        float.TryParse(splittedYData[0], out y);
                        float yVirgola = .0f;
                        float.TryParse(splittedYData[1], out (yVirgola));
                        y += yVirgola / 100;
                    }

                    pin0 = (splittedData[0]).Contains("1")) ? true : false;
					pin1 = (splittedData[1]).Contains("1")) ? true : false;
					pin2 = (splittedData[2]).Contains("1")) ? true : false;
					pin3 = (splittedData[3]).Contains("1")) ? true : false;
					//....so on and so forth.

                    missedInputs = 0;
                } else if (s == null) {
                    missedInputs++;
                    if (missedInputs > 1000) {
                        Debug.Log("-ArduinoManager- Arduino has become unreachable. Reconnecting now");
                        //TODO
                    }
                }
                Thread.Sleep(5); //Same reasoning as before
            }
        }

        //---------------------------------------------------------------------------------- FUNZIONI PUBBLICHE

        /// <summary>
        /// Obtains current command
        /// </summary>
        public string getCommand() { return currentCommand; }

        /// <summary>
        /// get for the pin
        /// </summary>
        public bool getPin0() { return pin0; }

        /// <summary>
        /// Sets the string to send to Arduino.
        /// </summary>
        public void Send(string Value) { outgoing = Value; }

        //---------------------------------------------------------------------------------- FUNZIONI DI UNITY

        private void Awake() {

            //Singleton check
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(this);

            //Persists between scenes
            DontDestroyOnLoad(this);

            connessione = new Thread(new ThreadStart(MultithreadedConnection));
            connessione.Start();
            inputParser = new Thread(new ThreadStart(MultithreadedParseInput));
            inputParser.Start();
        }

        private void OnApplicationQuit() {

            if (connessione != null || connessione.ThreadState.Equals(ThreadState.Running)) {
                Debug.LogWarning("Annullamento thread [" + connessione.ManagedThreadId + "]");
                connessione.Abort();
            }

            Debug.LogWarning("Annullamento thread [" + inputParser.ManagedThreadId + "]");
            inputParser.Abort();

            //Invio il segnale ad arduino per resettarsi (usato molto raramente, serve giusto per assicurarsi che arduino rifaccia l'handshake, visto che l'app si sta chiudendo).
            char c = 'R';
            seriale.Write(c.ToString());
            seriale.Close();
        }
    }

}