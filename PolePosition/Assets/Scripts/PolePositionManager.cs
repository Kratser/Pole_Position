using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using Mirror.Examples.Basic;

//overheadlights

public class PolePositionManager : NetworkBehaviour
{

    #region Variables

    public PolePositionNetworkManager networkManager;
    public UIManager uiManager;

    public delegate void OnUIChangeEvent(string newOrder);
    public OnUIChangeEvent OnOrderChangeDelegate;
    public OnUIChangeEvent OnCountDownDelegate;
    public OnUIChangeEvent OnUpdateLapDelegate;
    public OnUIChangeEvent OnWrongDirectionDelegate;
    public OnUIChangeEvent OnLapTimeDelegate;

    // Booleanos que indican si un jugador está conectado o no (para las posiciones iniciales)
    public bool[] playersConnected = new bool[4];
    public int numPlayers;
    public int minPlayers = 2;
    public int maxLaps = 3;

    public List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    public CircuitController m_CircuitController;
    public GameObject[] m_DebuggingSpheres;

    [SyncVar] public int numPlayersReady;

    // "true" si ya ha comezado la vuelta atrás, "false" en caso contrario
    [SyncVar] public bool gameStarted = false;

    // Espera a que todos los jugadores (menos el últimmo) hayan terminado la carrera para poner el ranking
    public int numPlayersFinished;
    public List<PlayerInfo> finishOrder = new List<PlayerInfo>();

    // Timer que se encarga de controlar la cuenta atrás
    public delegate void CheckTimerEvent(float s);
    public CheckTimerEvent CheckTimerDelegate;
    public List<float> timersStartTime = new List<float>();

    public Mutex mutex = new Mutex();

    #endregion Variables

    #region Start And Update

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<PolePositionNetworkManager>();
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();

        //Delegados UI
        OnOrderChangeDelegate += uiManager.ChangeOrder;
        OnOrderChangeDelegate += uiManager.ChangeOrderServer;
        OnCountDownDelegate += uiManager.CountDown;
        OnUpdateLapDelegate += uiManager.UpdateLap;
        OnWrongDirectionDelegate += uiManager.WrongDirection;
        OnLapTimeDelegate += uiManager.UpdateTimeLap;

        m_DebuggingSpheres = new GameObject[networkManager.maxConnections];
        for (int i = 0; i < networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
            m_DebuggingSpheres[i].GetComponent<MeshRenderer>().enabled = false;
        }
        for (int i = 0; i < playersConnected.Length; i++)
        {
            playersConnected[i] = false;
        }
        gameStarted = false;
    }

    private void Update()
    {
        if (m_Players.Count == 0)
            return;

        // Se comprueba el timer para actualizar la cuenta atrás
        if (CheckTimerDelegate != null)
        {
            CheckTimerDelegate(Time.time);
        }

        UpdateRaceProgress();
    }

    #endregion Start And Update

    #region Add and Remove Players

    /// <summary>
    /// Método que añade un jugador a la lista m_Players y aumenta el contador de jugadores
    /// </summary>
    /// <param name="player">Información del jugador a añadir</param>
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
        // Inicializamos el segmento del jugador al segmento donde se hace el spawn del jugador
        // para controlar las vueltas
        m_Players[m_Players.Count - 1].CurrentSegment = ComputeCarArcLength(m_Players.Count - 1, false);
        playersConnected[player.ID] = true;
        numPlayers++;
    }

    /// <summary>
    /// Método que elimina a un jugador de la lista m_Players
    /// y comprueba si hay que acabar la partida
    /// </summary>
    /// <param name="player">Información del jugador a eliminar</param>
    public void RemovePlayer(PlayerInfo player)
    {
        bool playerRemoved = m_Players.Remove(player);

        // Si se ha conseguido eliminar de la lista:
        if (playerRemoved)
        {
            Debug.LogWarning("Jugador eliminado");
            CheckPlayersRemoved(player);
        }
    }

    #endregion Add and Remove Players

    #region Players Exit Management

    /// <summary>
    /// Método que comprueba si hay que terminar la partida en caso 
    /// de falta de jugadores
    /// </summary>
    /// <param name="player">Jugador que ha sido eliminado</param>
    public void CheckPlayersRemoved(PlayerInfo player)
    {
        numPlayers--;
        playersConnected[player.ID] = false;
        // Si me quedo solo en la partida
        if (numPlayers <= 1)
        {
            try
            {
                uiManager.StartErrorMenu("The other players have left the game");
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
        else
        {
            //Eliminar jugador
            if (player.IsReadyToStart)
            {
                player.IsReadyToStart = false;
                numPlayersReady--;
            }
            if (player.CurrentLap == maxLaps)
            {
                player.CurrentLap = 0;
                numPlayersFinished--;
            }
        }
    }

    /// <summary>
    /// Método que reinicia el juego, cerrando las conexiones
    /// y reiniciando las variables
    /// </summary>
    public void ResetGame()
    {
        try
        {
            if (isServer)
            {
                if (isServerOnly)
                {
                    NetworkManager.singleton.StopServer();
                    Debug.LogWarning("Se ha quedado el servidor solo");
                }
                else
                {
                    NetworkManager.singleton.StopHost();
                    Debug.LogWarning("Se ha quedado el host solo");
                }
            }
            else
            {
                NetworkManager.singleton.StopClient();
                Debug.LogWarning("Se ha cerrado el servidor");
            }

            m_Players.Clear();
            numPlayers = 0;
            numPlayersReady = 0;
            numPlayersFinished = 0;
            finishOrder.Clear();
            // Se reinicia la Interfaz
            OnOrderChangeDelegate("");
            OnCountDownDelegate("");
            OnUpdateLapDelegate("LAP: 0/" + (maxLaps - 1));
            OnWrongDirectionDelegate("");
            OnLapTimeDelegate("--:--:--");
            uiManager.ResetRankingHUD();
            gameStarted = false;
            for (int i = 0; i < playersConnected.Length; i++)
            {
                playersConnected[i] = false;
            }
            Camera.main.gameObject.GetComponent<CameraController>().m_Focus = null;
            Camera.main.gameObject.GetComponent<CameraController>().ResetCamera();
            uiManager.ActivateMainMenu();
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
    }

    #endregion Players Exit Management

    #region Methods

    #region Players are ready --> CountDown

    /// <summary>
    /// Método que se ejecuta en el servidor y es llamado por el delgado "CheckTimersDelegate"
    /// que actualiza la cuenta atrás en los clientes
    /// </summary>
    /// <param name="s">Tiempo actual de juego (Time.time)</param>
    public void TimerCountDown(float s)
    {
        float elapsedTime = s - timersStartTime[0];
        if (elapsedTime >= 0 && elapsedTime < 1)
        {
            RpcStartCountDown("3!", timersStartTime[0]);
        }
        else if (elapsedTime >= 1 && elapsedTime < 2)
        {
            RpcStartCountDown("2!", timersStartTime[0]);
        }
        else if (elapsedTime >= 2 && elapsedTime < 3)
        {
            RpcStartCountDown("1!", timersStartTime[0]);
        }
        else if (elapsedTime >= 3 && elapsedTime < 4)
        {
            // Cuando acaba la cuenta atrás se inician los tiempos de los jugadores y se les da el control
            RpcStartCountDown("GO!", timersStartTime[0]);
            if (isServerOnly)
            {
                for (int i = 0; i < m_Players.Count; i++)
                {
                    m_Players[i].StartTime = timersStartTime[0] + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                    m_Players[i].LapTime = timersStartTime[0] + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                    m_Players[i].FinishTime = timersStartTime[0] + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                    m_Players[i].gameObject.GetComponent<SetupPlayer>().StartPlayer();
                }
            }
        }
        else
        {
            // Cuando acaba la cuenta atrás, se oculta el texto y se elimina la referencia del delegado,
            // para no seguir comprobando el temporizador
            RpcStartCountDown("", timersStartTime[0]);
            CheckTimerDelegate -= TimerCountDown;
            timersStartTime.RemoveAt(0);
        }
    }

    #endregion Players are ready --> CountDown

    /// <summary>
    /// Método que comprueba qué jugador va delante, y que ordena la lista m_Players en función
    /// del tamaño del circuito, la vuelta actual de los jugadores y su distancia recorrida en la vuelta
    /// </summary>
    /// <param name="x">Uno de los jugadores que se va a comparar</param>
    /// <param name="y">Uno de los jugadores que se va a comparar</param>
    /// <returns>Devuelve 1 si el jugador "y" va delante del jugador "x"</returns>
    public int ComparePlayers(PlayerInfo x, PlayerInfo y)
    {
        if ((m_CircuitController.CircuitLength * (x.CurrentLap - 1)) + x.TotalDistance
            < (m_CircuitController.CircuitLength * (y.CurrentLap - 1)) + y.TotalDistance)
            return 1;
        else return -1;
    }

    /// <summary>
    /// Método que actualiza las posiciones de la carrera y la interfaz
    /// </summary>
    public void UpdateRaceProgress()
    {
        for (int i = 0; i < m_Players.Count; ++i)
        {
            ComputeCarArcLength(i, true);
        }

        m_Players.Sort((P1, P2) => ComparePlayers(P1, P2));

        string myRaceOrder = "";
        for (int i = 0; i < m_Players.Count; i++)
        {
            // Si algún jugador ha terminado, se añade primero al orden de carrera, independientemente
            // de en qué lugar del circuito se encuentre
            if (finishOrder.Count >= i + 1)
            {
                myRaceOrder += finishOrder[i].Name + ",";
            }
            else
            {
                myRaceOrder += m_Players[i].Name + ",";
            }
        }
        OnOrderChangeDelegate(myRaceOrder);
         
        // Cuando han terminado todos los jugadores menos el último activamos el HUD
        // con el Ranking y mostramos las posiciones en las que han quedado y cuánto han tardado
        if (numPlayersFinished == numPlayers - 1 && numPlayers > 1)
        {
            numPlayersFinished++;
            Debug.LogWarning("Se acabó la partida");
            // Añadimos a la lista de las posiciones finales el jugador que no ha conseguido pasar la meta
            for (int i = 0; i < m_Players.Count; i++)
            {
                if (m_Players[i].CurrentLap != maxLaps)
                {
                    Debug.LogWarning("Encontrado último jugador " + m_Players[i].Name);
                    finishOrder.Add(m_Players[i]);
                    break;
                }
            }

            // Cambiar HUD
            string[] positions = new string[numPlayers];
            string[] times = new string[numPlayers];
            for (int i = 0; i < m_Players.Count; i++)
            {
                positions[i] = finishOrder[i].Name;
                times[i] = FloatToTime(finishOrder[i].FinishTime);
                // El jugador que no consigue llegar a la meta no tiene tiempo
                times[times.Length - 1] = "--:--:--";
            }
            if (isClient)
            {
                uiManager.ActivateRankingHUD();
            }
            uiManager.ChangeRankingHUD(positions, times);
        }
    }

    /// <summary>
    /// Método que calcula a qué distancia se encuentra el jugador de la meta, en qué segmento del circuito
    /// se encuentra, comrpueba si ha pasado la meta y si va en dirección contraria
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    /// <param name="check">Booleano que determina si hay que realizar comprobaciones de paso por la meta y dirección contraria</param>
    /// <returns>Índice del segmento en el que se encuetra el jugador</returns>
    int ComputeCarArcLength(int ID, bool check)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;

        // Segmento de la línea del circuito en la que se encuentra el jugador "ID"
        int segIdx;
        // Distancia que hay desde el jugador "ID" hasta su proyección sobre el segmento más cercano
        float carDist;
        // Proyección de la posición del jugador "ID" sobre el segmento más cercano
        Vector3 carProj;

        float minArcL = this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        this.m_DebuggingSpheres[ID].transform.position = carProj;

        // Se realizan las comprobaciones de cambio de vuelta y de dirección equivocada
        if (check)
        {
            CheckLaps(ID, minArcL, segIdx);

            CheckDirection(ID, segIdx);
        }

        // Se actualiza la posición recorrida por el jugador en esa vuelta, para ordenarlo en la lista m_Players
        m_Players[ID].TotalDistance = minArcL;

        return segIdx;
    }

    /// <summary>
    /// Método que comrpueba si el jugador pasa por la meta
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    /// <param name="minArcL">Distancia de la meta al jugador</param>
    public void CheckLaps(int ID, float minArcL, int segIdx)
    {
        // Vuelta hacia atrás
        if (m_Players[ID].CurrentSegment - segIdx == -(m_CircuitController.m_CircuitPath.positionCount - 2))
        {
            // Se realizan las comprobaciones en el servidor y se notifican a los clientes
            if (isServer)
            {
                m_Players[ID].CurrentLap--;
                // Se notifica a los jugadores del cambio de vuelta
                RpcNewLap(m_Players[ID].CurrentLap, m_Players[ID].gameObject);

                // Para que no empiece en una vuelta menor que 0, y que no se puedan acumular vueltas negativas
                if (m_Players[ID].CurrentLap < 0)
                {
                    m_Players[ID].CurrentLap = 0;
                    // Se notifica a los jugadores del cambio de vuelta
                    RpcNewLap(m_Players[ID].CurrentLap, m_Players[ID].gameObject);
                }
                if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
                {
                    OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps - 1));
                }
            }
            //m_Players[ID].TotalDistance = minArcL;
        }
        // Vuelta hacia delante
        else if (m_Players[ID].CurrentSegment - segIdx == (m_CircuitController.m_CircuitPath.positionCount - 2)/*distance < -300*/)
        {
            if (isServer)
            {
                m_Players[ID].CurrentLap++;
                // Se notifica a los clientes del cambio de vuelta
                RpcNewLap(m_Players[ID].CurrentLap, m_Players[ID].gameObject);

                if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
                {
                    OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps - 1));
                }

                m_Players[ID].LapTime = Time.time - m_Players[ID].FinishTime;
                m_Players[ID].FinishTime = Time.time;
                // Se notifica a los clientes del nuevo tiempo / vuelta
                RpcNewLapTime(m_Players[ID].LapTime, m_Players[ID].gameObject);

                //Acabar partida para cada jugador
                if (m_Players[ID].CurrentLap == maxLaps)
                {
                    m_Players[ID].FinishTime = Time.time - m_Players[ID].StartTime;
                    // Se notifica a los clientes que un jugador ha terminado la carrera
                    RpcEndRace(m_Players[ID].FinishTime, numPlayersFinished + 1, m_Players[ID].gameObject);
                    // Evitamos errores de HOST
                    if (isServerOnly)
                    {
                        numPlayersFinished++;
                        finishOrder.Add(m_Players[ID]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Método que comprueba si el jugador va en dirección correcta
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    /// <param name="segIdx">Id del segmento en el que se encuentra el jugador</param>
    public void CheckDirection(int ID, int segIdx)
    {
        // Si el jugador va en dirección contraria 
        if ((m_Players[ID].CurrentSegment - segIdx == 1
            || m_Players[ID].CurrentSegment - segIdx == -(m_CircuitController.m_CircuitPath.positionCount - 2))
            && m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            new Task(() => OnWrongDirectionDelegate("Wrong direction! T^T")).Start();
        }

        // Se modifica el segmento del jugador si avanza o retrocede, o si pasa por la meta
        // (Se evitan errores en el cálculo del segemento)
        if (m_Players[ID].CurrentSegment - segIdx == 1 || m_Players[ID].CurrentSegment - segIdx == -1
            || m_Players[ID].CurrentSegment - segIdx == (m_CircuitController.m_CircuitPath.positionCount - 2)
            || m_Players[ID].CurrentSegment - segIdx == -(m_CircuitController.m_CircuitPath.positionCount - 2))
        {
            m_Players[ID].CurrentSegment = segIdx;
        }
    }

    /// <summary>
    /// Método que formatea un número en segundos a minutos:segundos:milisegundos
    /// </summary>
    /// <param name="s">Número recibido en segundos</param>
    /// <returns>Cadena de caracteres con el tiempo formateado</returns>
    public string FloatToTime(float s)
    {
        string time;
        int mins, secs, ms;
        mins = (int)(s % 3600) / 60;
        secs = (int)(s % 60);
        ms = (int)((s % 1) * 1000);
        time = mins + ":" + secs + ":" + ms;
        return time;
    }

    #endregion Methods

    #region Commands

    /// <summary>
    /// Command, llamado desde la clase SetupPlayer para notificar que hay un nuevo jugador listo
    /// </summary>
    /// <param name="player">Jugador que está listo</param>
    public void NewPlayerReady(GameObject player)
    {
        // Mutex que evita que se añada un nuevo jugador mientras se aumentan los jugadores listos
        mutex.WaitOne();
        PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
        playerInfo.IsReadyToStart = true;
        numPlayersReady++;

        if (!gameStarted)
        {
            // Si todos los jugadores están listos, el servidor inicia la cuenta atrás
            if ((numPlayersReady == numPlayers) && (numPlayers >= minPlayers))
            {
                gameStarted = true;
                if (isServer)
                {
                    timersStartTime.Add(Time.time);
                    CheckTimerDelegate += TimerCountDown;
                }
            }
        }
        mutex.ReleaseMutex();
    }

    #endregion Commands

    #region ClientRPCS

    /// <summary>
    /// RPC que notifica a los clientes con la cuenta atrás
    /// </summary>
    /// <param name="timeText">Tiempo restante de la cuenta atrás</param>
    /// <param name="startTime">Tiempo de inicio de la carrera</param>
    [ClientRpc]
    public void RpcStartCountDown(string timeText, float startTime)
    {
        uiManager.panelWaiting.gameObject.SetActive(false);
        OnCountDownDelegate(timeText);
        if (timeText.Equals("GO!"))
        {
            for (int i = 0; i < m_Players.Count; i++)
            {
                m_Players[i].StartTime = startTime + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                m_Players[i].LapTime = startTime + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                m_Players[i].FinishTime = startTime + 3; // Se suma 3 por los 3 segundos de cuenta atrás
                m_Players[i].gameObject.GetComponent<SetupPlayer>().StartPlayer();
            }
        }
    }

    /// <summary>
    /// RPC que comunica a los clientes que un jugador ha pasado por la meta
    /// </summary>
    /// <param name="lap">Nueva vuelta del jugador</param>
    /// <param name="player">Jugador que ha pasado la meta</param>
    [ClientRpc]
    public void RpcNewLap(int lap, GameObject player)
    {
        mutex.WaitOne();
        PlayerInfo p_Info = player.GetComponent<PlayerInfo>();
        p_Info.CurrentLap = lap;
        Debug.Log(p_Info.Name + ": " + p_Info.CurrentLap + ", "
            + player.GetComponent<NetworkIdentity>().isLocalPlayer);
        if (player.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            OnUpdateLapDelegate("LAP: " + p_Info.CurrentLap.ToString() + "/" + (maxLaps - 1));
        }
        mutex.ReleaseMutex();
    }

    /// <summary>
    /// RPC que comunica a los clientes el tiempo por vuelta de un jugador que ha pasado por la meta
    /// </summary>
    /// <param name="time">Nuevo tiempo por vuleta del jugador</param>
    /// <param name="player">Jugador que ha pasado la meta</param>
    [ClientRpc]
    public void RpcNewLapTime(float time, GameObject player)
    {
        mutex.WaitOne();
        PlayerInfo p_Info = player.GetComponent<PlayerInfo>();
        p_Info.LapTime = time;
        p_Info.FinishTime = Time.time;
        Debug.Log(p_Info.Name + ": " + p_Info.LapTime + ", "
            + player.GetComponent<NetworkIdentity>().isLocalPlayer);
        if (player.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            OnLapTimeDelegate(FloatToTime(p_Info.LapTime));
        }
        mutex.ReleaseMutex();
    }

    /// <summary>
    /// RPC que notifica a los clientes cuando un jugador termina la carrera
    /// </summary>
    /// <param name="endTime">Tiempo total de carrera del jugador</param>
    /// <param name="nPlayersFinished">Número de jugadores que han terminado</param>
    /// <param name="player">Jugador que ha terminado la carrera</param>
    [ClientRpc]
    public void RpcEndRace(float endTime, int nPlayersFinished, GameObject player)
    {
        mutex.WaitOne();
        PlayerInfo p_Info = player.GetComponent<PlayerInfo>();
        p_Info.FinishTime = endTime;
        numPlayersFinished = nPlayersFinished;
        finishOrder.Add(p_Info);
        if (player.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            player.GetComponent<PlayerController>().enabled = false;
            uiManager.ActivateRankingHUD();
            Debug.Log(p_Info.FinishTime);
        }
        mutex.ReleaseMutex();
    }

    #endregion ClientRPCS

}