using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;

public class PolePositionManager : NetworkBehaviour
{

    #region Variables

    public bool[] playersConnected = new bool[4];
    public int numPlayers;
    public int minPlayers = 2;
    public int maxLaps = 4;
    public NetworkManager networkManager;
    public UIManager uiManager;
  
    public delegate void OnUIChangeEvent(string newOrder);
    public OnUIChangeEvent OnOrderChangeDelegate;
    public OnUIChangeEvent OnCountDownDelegate;
    public OnUIChangeEvent OnUpdateLapDelegate;
    public OnUIChangeEvent OnWrongDirectionDelegate;

    public List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    public CircuitController m_CircuitController;
    public GameObject[] m_DebuggingSpheres;

    [SyncVar(hook = nameof(CheckPlayersReady))] public int numPlayersReady;
    
    // "true" si ya ha comezado la vuelta atrás, "false" en caso contrario
    public bool GameStarted { get; set; }

    // Espera a que todos los jugadores (menos el últimmo) hayan terminado la carrera para poner el ranking
    public int numPlayersFinished;

    public delegate void CheckTimerEvent(float s);
    public CheckTimerEvent CheckTimerDelegate;
    public List<float> timersStartTime = new List<float>();

    #endregion Variables

    #region Start And Update

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        //Delegados UI
        OnOrderChangeDelegate += uiManager.ChangeOrder;
        OnOrderChangeDelegate += uiManager.ChangeOrderServer;
        OnCountDownDelegate += uiManager.CountDown;
        OnUpdateLapDelegate += uiManager.UpdateLap;
        OnWrongDirectionDelegate += uiManager.WrongDirection;

        if (m_CircuitController == null) m_CircuitController = FindObjectOfType<CircuitController>();

        m_DebuggingSpheres = new GameObject[networkManager.maxConnections];
        for (int i = 0; i < networkManager.maxConnections; ++i)
        {
            m_DebuggingSpheres[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_DebuggingSpheres[i].GetComponent<SphereCollider>().enabled = false;
        }
        for (int i = 0; i < playersConnected.Length; i++)
        {
            playersConnected[i] = false;
        }
        GameStarted = false;
    }

    private void Update()
    {
        if (m_Players.Count == 0)
            return;

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
    /// <param name="player"></param>
    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
        playersConnected[player.ID] = true;
        numPlayers++;
    }
    
    /// <summary>
    /// Método que elimina a un jugador de la lista m_Players
    /// y comprueba si hay que acabar la partida
    /// </summary>
    /// <param name="player"></param>
    public void RemovePlayer(PlayerInfo player)
    {
        bool playerRemoved = m_Players.Remove(player);
        
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
        if (numPlayers <= 1)
        {
            ResetGame();
        }
        else
        {
            //Comprobar jugadores listos
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
        if (isServer)
        {
            if (isServerOnly)
            {
                NetworkManager.singleton.StopServer();
                Debug.LogWarning("Tas quedao solo server, te echo");
            }
            else
            {
                NetworkManager.singleton.StopHost();
                Debug.LogWarning("Tas quedao solo host, te echo");
            }
        }
        else
        {
            NetworkManager.singleton.StopClient();
            Debug.LogWarning("Tas quedao sin server, a tomar por culo todos");
        }

        m_Players.Clear();
        numPlayers = 0;
        numPlayersReady = 0;
        numPlayersFinished = 0;
        GameStarted = false;
        for (int i = 0; i < playersConnected.Length; i++)
        {
            playersConnected[i] = false;
        }
        Camera.main.gameObject.GetComponent<CameraController>().ResetCamera();
        uiManager.ActivateMainMenu();
    }

    #endregion Players Exit Management

    #region Methods

    #region Players are ready --> CountDown

    /// <summary>
    /// The Hook method must have two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
    /// The Hook is always called after the property value is set. You don't need to set it yourself.
    /// The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    /// </summary>
    /// <param name="oldPlayersReady"></param>
    /// <param name="newPlayersReady"></param>
    public void CheckPlayersReady(int oldPlayersReady, int newPlayersReady)
    {
        if (!GameStarted)
        {
            if ((newPlayersReady == numPlayers) && (numPlayers >= minPlayers))
            {
                GameStarted = true;
                if (isServer)
                {
                    RpcStartCountDown();
                }
            }
        }
    }

    [ClientRpc]
    public void RpcStartCountDown()
    {
        float startTime = Time.time;
        timersStartTime.Add(startTime);
        CheckTimerDelegate += TimerCountDown;
        //new Task(() => CountDown(startTime)).Start();
    }

    public void CountDown(float time)
    {
        Debug.Log("3!");
        new Task(() => OnCountDownDelegate("3!")).Start();
        Thread.Sleep(1000);
        Debug.Log("2!");
        new Task(() => OnCountDownDelegate("2!")).Start();
        Thread.Sleep(1000);
        Debug.Log("1!");
        new Task(() => OnCountDownDelegate("1!")).Start();
        Thread.Sleep(1000);
        for (int i = 0; i < m_Players.Count; i++)
        {
            m_Players[i].StartTime = time;
            m_Players[i].gameObject.GetComponent<SetupPlayer>().StartPlayer();
        }
        Debug.Log("GO!");
        new Task(() => OnCountDownDelegate("GO!")).Start();
        Thread.Sleep(1000);
        new Task(() => OnCountDownDelegate("")).Start();
    }

    public void TimerCountDown(float s)
    {
        float elapsedTime = s - timersStartTime[0];
        if (elapsedTime >= 0 && elapsedTime < 1)
        {
            OnCountDownDelegate("3!");
        }
        else if (elapsedTime >= 1 && elapsedTime < 2)
        {
            OnCountDownDelegate("2!");
        }
        else if(elapsedTime >= 2 && elapsedTime < 3)
        {
            OnCountDownDelegate("1!");
        }
        else if (elapsedTime >= 3 && elapsedTime < 4)
        {
            OnCountDownDelegate("GO!");
            for (int i = 0; i < m_Players.Count; i++)
            {
                m_Players[i].StartTime = timersStartTime[0];
                m_Players[i].gameObject.GetComponent<SetupPlayer>().StartPlayer();
            }
        }
        else
        {
            OnCountDownDelegate("");
            CheckTimerDelegate -= TimerCountDown;
            timersStartTime.RemoveAt(0);
        }
    }

    #endregion Players are ready --> CountDown

    /// <summary>
    /// Método que comprueba qué jugador va delante, y que ordena la lista m_Players
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
            ComputeCarArcLength(i);
        }

        m_Players.Sort((P1, P2) => ComparePlayers(P1, P2));

        string myRaceOrder = "";
        for (int i = 0; i < m_Players.Count; i++)
        {
            myRaceOrder += m_Players[i].Name + " ";
        }
        OnOrderChangeDelegate(myRaceOrder);

        /* 
         * Cuando han terminado todos los jugadores menos el último activamos el HUD
         * con el Ranking y mostramos las posiciones en las que han quedado y cuánto han tardado
         */
        if (numPlayersFinished == numPlayers - 1 && numPlayers > 1)
        {
            numPlayersFinished++;

            // Cambiar HUD
            string[] positions = new string[numPlayers];
            string[] times = new string[numPlayers];
            for (int i = 0; i < m_Players.Count; i++)
            {
                positions[i] = m_Players[i].Name;
                times[i] = FloatToTime(m_Players[i].FinishTime);
                // El jugador que no consigue llegar a la meta no tiene tiempo
                times[times.Length - 1] = "--:--:--";
            }

            uiManager.ActivateRankingHUD();
            uiManager.ChangeRankingHUD(positions, times);
        }
    }

    /// <summary>
    /// Método que calcula a qué distancia se encuentra el jugador de la meta
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    void ComputeCarArcLength(int ID)
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

        float distance = minArcL - m_Players[ID].TotalDistance;

        CheckLaps(ID, distance, minArcL);

        CheckDirection(ID, segIdx);
    }

    /// <summary>
    /// Método que comrpueba si el jugador pasa por la meta
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    /// <param name="distance">Distancia recorrida desde el update anterior</param>
    /// <param name="minArcL">Distancia de la meta al jugador</param>
    public void CheckLaps(int ID, float distance, float minArcL)
    {
        // Vuelta hacia atrás
        if (distance > 300)
        {
            m_Players[ID].CurrentLap--;
            // Para que no empiece en una vuelta menor que 0, y que no se puedan acumular vueltas negativas
            if (m_Players[ID].CurrentLap < 0)
            {
                m_Players[ID].CurrentLap = 0;
            }
            if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps - 1));
            }
            m_Players[ID].TotalDistance = minArcL;
        }
        // Vuelta hacia delante
        else if (distance < -300)
        {
            m_Players[ID].CurrentLap++;

            if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps - 1));
            }

            m_Players[ID].TotalDistance = minArcL;
            distance = minArcL;

            //Acabar partida para cada jugador
            if (m_Players[ID].CurrentLap == maxLaps)
            {
                m_Players[ID].FinishTime = Time.time - m_Players[ID].StartTime;
                numPlayersFinished++;

                if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
                {
                    m_Players[ID].GetComponent<PlayerController>().enabled = false;
                    uiManager.ActivateRankingHUD();
                    //esperar al resto de players para mostrar ranqueen
                    Debug.Log(m_Players[ID].FinishTime);
                }
            }
        }
        else
        {
            m_Players[ID].TotalDistance += distance;
        }
    }

    /// <summary>
    /// Método que comprueba si el jugador va en dirección correcta
    /// </summary>
    /// <param name="ID">Id del jugador dentro de la lista m_Players</param>
    /// <param name="segIdx">Id del segmento en el que se encuentra el jugador</param>
    public void CheckDirection(int ID, int segIdx)
    {
        if ((m_Players[ID].CurrentSegment - segIdx == 1
            || m_Players[ID].CurrentSegment - segIdx == -(m_CircuitController.m_CircuitPath.positionCount - 2))
            && m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            new Task(() => OnWrongDirectionDelegate("Wrong direction! T^T")).Start();
        }

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
        ms = (int)((s % (int)s) * 1000);
        time = mins + ":" + secs + ":" + ms;
        return time;
    }

    #endregion Methods

    #region Commands

    public void NewPlayerReady(GameObject player)
    {
        PlayerInfo playerInfo = player.GetComponent<PlayerInfo>();
        playerInfo.IsReadyToStart = true;
        numPlayersReady++;
    }

    #endregion Commands

}