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
    

    public int numPlayers;
    public NetworkManager networkManager;
    public UIManager uiManager;

  
    public delegate void OnUIChangeEvent(string newOrder);
    public OnUIChangeEvent OnOrderChangeDelegate;
    public OnUIChangeEvent OnCountDownDelegate;
    public OnUIChangeEvent OnUpdateLapDelegate;

    private readonly List<PlayerInfo> m_Players = new List<PlayerInfo>(4);
    private CircuitController m_CircuitController;
    private GameObject[] m_DebuggingSpheres;
    //Barrera de seleccion de color de coche y nombre de usuario
    public Semaphore PlayersNotReadyBarrier = new Semaphore(0, 4);
    [SyncVar(hook = nameof(CheckPlayersReady))] public int numPlayersReady;

    public CountdownEvent countDown = new CountdownEvent(3);

    private void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();

        OnOrderChangeDelegate += uiManager.ChangeOrder;
        OnCountDownDelegate += uiManager.CountDown;
        OnUpdateLapDelegate += uiManager.UpdateLap;

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

    public void CheckPlayersReady(int oldPlayersReady, int newPlayersReady)
    {
        Debug.Log(numPlayersReady);
        if ((newPlayersReady == numPlayers) /*&& (numPlayers >= 2)*/)
        {
            PlayersNotReadyBarrier.Release(newPlayersReady);
            RpcStartCountDown();
        }
    }

    [ClientRpc]
    public void RpcStartCountDown()
    {
        new Task(() => CountDown()).Start();
    }

    public void CountDown()
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
        countDown.Signal();
        Debug.Log("GO!");
        new Task(() => OnCountDownDelegate("GO!")).Start();
        Thread.Sleep(1000);
        new Task(() => OnCountDownDelegate("")).Start();
    }

    private class PlayerInfoComparer : Comparer<PlayerInfo>
    {
        float[] m_ArcLengths;

        public PlayerInfoComparer(float[] arcLengths)
        {
            m_ArcLengths = arcLengths;
        }

        public override int Compare(PlayerInfo x, PlayerInfo y)
        {
            if (/*this.m_ArcLengths[x.ID]*/x.TotalDistance * x.CurrentLap < /*m_ArcLengths[y.ID]*/y.TotalDistance * y.CurrentLap)
                return 1;
            else return -1;
        }
    }

    public void UpdateRaceProgress()
    {
        // Update car arc-lengths
        float[] arcLengths = new float[m_Players.Count];

        for (int i = 0; i < m_Players.Count; ++i)
        {
            arcLengths[i] = ComputeCarArcLength(m_Players[i].ID);
        }

        m_Players.Sort(new PlayerInfoComparer(arcLengths));

        string myRaceOrder = "";
        for (int i = 0; i < m_Players.Count; i++)
        {
            myRaceOrder += i+1 + ". " + m_Players[i].Name + "\n";
            //myRaceOrder += "["+m_Players[i].ID+"] - "+m_Players[i].Name +": "+arcLengths[m_Players[i].ID] +"\n";
        }
        new Task(() => OnOrderChangeDelegate(myRaceOrder)).Start();
        //OnOrderChangeDelegate(myRaceOrder);
        Debug.Log("El orden de carrera es: " + myRaceOrder);
    }

    float ComputeCarArcLength(int ID)
    {
        // Compute the projection of the car position to the closest circuit 
        // path segment and accumulate the arc-length along of the car along
        // the circuit.
        Vector3 carPos = this.m_Players[ID].transform.position;

        int segIdx;
        float carDist;
        Vector3 carProj;

        float minArcL =
            this.m_CircuitController.ComputeClosestPointArcLength(carPos, out segIdx, out carProj, out carDist);

        this.m_DebuggingSpheres[ID].transform.position = carProj;
        /*
        if (this.m_Players[ID].CurrentLap == 0)
        {
            minArcL -= m_CircuitController.CircuitLength;
        }
        else
        {
            //minArcL += m_CircuitController.CircuitLength * (m_Players[ID].CurrentLap - 1);
            minArcL *= m_Players[ID].CurrentLap;
        }
        */
        float distance = minArcL - m_Players[ID].TotalDistance;
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
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap + "/ 3");
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
                OnUpdateLapDelegate("LAP: " + m_Players[ID].CurrentLap + "/ 3");
            }
            m_Players[ID].TotalDistance = minArcL;
        }
        else
        {
            m_Players[ID].TotalDistance += distance;
        }
        /**/
        Debug.Log(m_Players[ID].CurrentLap);
        return minArcL;
    }
}