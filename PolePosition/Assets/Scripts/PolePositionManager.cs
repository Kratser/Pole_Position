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

    //Barrera de seleccion de color de coche y nombre de usuario
    public Semaphore PlayersNotReadyBarrier = new Semaphore(0, 4);
    [SyncVar(hook = nameof(CheckPlayersReady))] public int numPlayersReady;

    public CountdownEvent countDown = new CountdownEvent(3);

    // Espera a que todos los jugadores (menos el últimmo) hayan terminado la carrera para poner el ranking
    public int numPlayersFinished;

    #endregion

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        //Delegados UI
        OnOrderChangeDelegate += uiManager.ChangeOrder;
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
    }

    private void Update()
    {
        if (m_Players.Count == 0)
            return;

        UpdateRaceProgress();
    }

    public void AddPlayer(PlayerInfo player)
    {
        m_Players.Add(player);
        numPlayers++;
    }
    #region Players Exit Management
    public void RemovePlayer(PlayerInfo player)
    {
        bool playerRemoved = m_Players.Remove(player);
        
        if (playerRemoved)
        {
            Debug.LogWarning("Jugador eliminado");
            CheckPlayersRemoved(player);
        }
    }

    public void CheckPlayersRemoved(PlayerInfo player)
    {
        numPlayers--;

        if (numPlayers <= 1)
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

            ResetGame();
        }
        else
        {
            //Comprobar jugadores listossssssssssssssss
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

    public void ResetGame()
    {
        m_Players.Clear();
        numPlayers = 0;
        numPlayersReady = 0;
        numPlayersFinished = 0;
        countDown.Reset();
        uiManager.ActivateMainMenu();
    }

    #endregion

    public string FloatToTime (float s){
        string time;
        int mins, secs, ms;
        mins = (int)(s % 3600) / 60;
        secs = (int)(s % 60);
        ms = (int)((s % (int)s) * 1000);
        time = mins + ":" + secs + ":" + ms;
        return time;
    }

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
        if ((newPlayersReady == numPlayers) && (numPlayers >= minPlayers))
        {
            //PlayersNotReadyBarrier.Release(newPlayersReady);
            SetupPlayer m_setupPlayer = FindObjectOfType<SetupPlayer>();
            m_setupPlayer.CmdCallRpcCountdown();
        }

    }

    

    [ClientRpc]
    public void RpcStartCountDown()
    {
        float startTime = Time.time;
        new Task(() => CountDown(startTime)).Start();
    }

    public void CountDown(float time)
    {
        Debug.Log("3!");
        new Task(() => OnCountDownDelegate("3!")).Start();
        Thread.Sleep(1000);
        countDown.Signal();
        Debug.Log("2!");
        new Task(() => OnCountDownDelegate("2!")).Start();
        Thread.Sleep(1000);
        countDown.Signal();
        Debug.Log("1!");
        new Task(() => OnCountDownDelegate("1!")).Start();
        Thread.Sleep(1000);
        for (int i = 0; i < m_Players.Count; i++)
        {
            m_Players[i].StartTime = time;
        }
        countDown.Signal();
        Debug.Log("GO!");
        new Task(() => OnCountDownDelegate("GO!")).Start();
        Thread.Sleep(1000);
        new Task(() => OnCountDownDelegate("")).Start();
    }

    #endregion

    public  int ComparePlayers(PlayerInfo x, PlayerInfo y)
    {
        if ((m_CircuitController.CircuitLength * (x.CurrentLap - 1)) + x.TotalDistance 
            < (m_CircuitController.CircuitLength * (y.CurrentLap - 1)) + y.TotalDistance)
            return 1;
        else return -1;
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = ComputeCarArcLength(m_Players[i].ID);
            //arcLengths[i] = ComputeCarArcLength(i);
        }

        m_Players.Sort((P1, P2)=>ComparePlayers(P1, P2));

        string myRaceOrder = "";
        for (int i = 0; i < m_Players.Count; i++)
        {
            myRaceOrder += (i+1) + ". " + m_Players[i].Name + "\n";
        }
        OnOrderChangeDelegate(myRaceOrder);

        //Debug.Log("El orden de carrera es: " + myRaceOrder);

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
                //m_Players[i].ID
                positions[i] = m_Players[i].Name;
                times[i] = FloatToTime(m_Players[i].FinishTime);
                // El jugador que no consigue llegar a la meta no tiene tiempo
                times[times.Length - 1] = "--:--:--";
            }
           
            uiManager.ActivateRankingHUD();
            uiManager.ChangeRankingHUD(positions, times);
        }
    }

    float ComputeCarArcLength(int ID)
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
        
        return minArcL;
    }

    public void CheckLaps(int ID, float distance, float minArcL)
    {
        /**/
        // Vuelta hacia atrás
        if (distance > 300)
        {
            m_Players[ID].CurrentLap--;
            // Para que no empiece en una vuelta menor que 0, y que no se puedan acumular vueltas negativas
            if (m_Players[ID].CurrentLap < 0)
            {
                m_Players[ID].CurrentLap = 0;
            }
            // !!!!! HA DE CAMBIARSE LA VUELTA MAX
            if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps-1));
            }
            m_Players[ID].TotalDistance = minArcL;
        }
        // Vuelta hacia delante
        else if (distance < -300)
        {
            m_Players[ID].CurrentLap++;

            // !!!!! HA DE CAMBIARSE LA VUELTA MAX

            if (m_Players[ID].GetComponent<NetworkIdentity>().isLocalPlayer)
            {
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap.ToString() + "/" + (maxLaps-1));
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
        /**/
    }

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

}