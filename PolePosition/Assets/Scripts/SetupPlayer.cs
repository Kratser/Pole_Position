using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using UnityEngine.UI;
using System.Threading;
using System.Threading.Tasks;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    [SyncVar] private int m_ID;

    // The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.
    [SyncVar(hook = nameof(SetName))] private string m_Name;
    [SyncVar(hook = nameof(SetColor))] private int m_Color;

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;

    // !! Estaría guay con materiales
    // Almacenamos los distintos prefabs que tienen los colores de los diferentes bodies
    public GameObject[] raceCarColors = new GameObject[4];

    #region NAME

    /// <summary>
    /// Para que un jugador esté listo, tiene que haber introducido un nombre correcto
    /// y haber pulsado el botón de que está listo. Cuando el jugador modifica su nombre,
    /// se envía al servidor esta nueva información, y cuando está listo para empezar
    /// también le enviamos al servidor esta información para que se lo notifique a los otro clientes
    /// </summary>
    public void PlayerReady()
    {
        if (isLocalPlayer)
        {
            m_PlayerInfo.Name = m_UIManager.PlayerUserName;
            CmdNameToServer(m_PlayerInfo.Name);

            if (m_PlayerInfo.Name != null)
            {
                CmdNewPlayerReady();
            }
        }
    }

    /// <summary>
    /// Cuando un jugador modifica su nombre se envía esta información al servidor 
    /// se modifica la variable compartida en el servidor y se comunica el cambio al resto de clientes
    /// </summary>
    /// <param name="name"></param>
    [Command]
    private void CmdNameToServer(string name)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Name = name;
    }

    /// <summary>
    /// Cuando un jugador está preparado se aumenta en el servidor la varíable que cuenta 
    /// el número de jugadores preparados y se comunica el cambio al resto de clientes.
    /// </summary>
    [Command]
    public void CmdNewPlayerReady()
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_PolePositionManager.numPlayersReady++;
    }

    /// <summary>
    /// The Hook method must have two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
    /// The Hook is always called after the property value is set. You don't need to set it yourself.
    /// The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    /// </summary>
    /// <param name="oldName"></param>
    /// <param name="newName"></param>
    public void SetName(string oldName, string newName)
    {
        m_PlayerController.PlayerName.text = newName;
        m_PlayerInfo.Name = newName;
    }

    #endregion

    #region COLOR

    /// <summary>
    /// Mediante changeColorButton, vamos cambiando el color del coche cada vez que se pulsa
    /// </summary>
    public void ChangeColor()
    {
        if (isLocalPlayer)
        {
            if (m_PlayerInfo.Color == 3)
            {
                m_PlayerInfo.Color = 0;
            }
            else
            {
                m_PlayerInfo.Color++;
            }
            CmdColorToServer(m_PlayerInfo.Color);
        }
    }

    /// <summary>
    /// Cuando un jugador modifica su color se envía esta información al servidor 
    /// se modifica la variable compartida en el servidor y se comunica el cambio al resto de clientes
    /// </summary>
    /// <param name="color"></param>
    [Command]
    private void CmdColorToServer(int color)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        
        m_Color = color;
    }

    /// <summary>
    /// The Hook method must have two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
    /// The Hook is always called after the property value is set. You don't need to set it yourself.
    /// The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    /// </summary>
    /// <param name="oldColor"></param>
    /// <param name="newColor"></param>
    public void SetColor(int oldColor, int newColor)
    {
        this.GetComponentInChildren<MeshRenderer>().materials = raceCarColors[newColor].GetComponent<MeshRenderer>().sharedMaterials;
        m_PlayerInfo.Color = newColor;
    }

    #endregion

    #region Start & Stop Callbacks

    /// <summary>
    /// This is invoked for NetworkBehaviour objects when they become active on the server.
    /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
    /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        m_ID = connectionToClient.connectionId;
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        m_PlayerInfo.ID = m_ID;
        // m_PlayerInfo.Name = "Player" + m_ID;
        m_PlayerInfo.CurrentLap = 0;

        m_UIManager.readyButton.onClick.AddListener(() => PlayerReady());
        m_UIManager.changeColorButton.onClick.AddListener(() => ChangeColor());

        // Añadir jugador a la partida
        m_PolePositionManager.AddPlayer(m_PlayerInfo);     
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
    }

    #endregion

    #region START

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            ConfigureCamera();
            m_PlayerInfo.IsReady = false;
            new Task(() => WaitPlayersReady()).Start();

            m_PlayerController.enabled = true;
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            m_PlayerController.OnCrashDelegate += m_UIManager.ShowCrashError;

        }
    }

    /// <summary>
    /// Se espera a que estén todos los jugadores listos y que haya acabado la 
    /// cuenta atrás para activar el controlador del juego.
    /// </summary>
    public void WaitPlayersReady()
    {
        // Barrera para esperar a que todos los jugadores estén listos.
        m_PolePositionManager.PlayersNotReadyBarrier.WaitOne();
        //Cuando todos estan listos: Cuenta atrás para empezar  3... 2....1... lets go!
        m_PolePositionManager.countDown.Wait();

        //m_PlayerController.enabled = true;
        //m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
        //m_PlayerController.OnCrashDelegate += m_UIManager.ShowCrashError;
        m_PlayerInfo.IsReady = true;
    }

    #endregion

    // Actualización del UI de la velocidad ¿POR QUÉ SE MULTIPLICA X5?
    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int)speed * 5); // 5 for visualization purpose (km/h)
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }
}