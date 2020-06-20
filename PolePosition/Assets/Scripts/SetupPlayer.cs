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
    #region Variables
    [SyncVar] private int m_ID;

    // The hook attribute can be used to specify a function to be called when the SyncVar changes value on the client.
    [SyncVar(hook = nameof(SetName))] private string m_Name;
    [SyncVar(hook = nameof(SetColor))] private int m_Color;

    private UIManager m_UIManager;
    private PolePositionNetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private NameController m_NameController;
    private PolePositionManager m_PolePositionManager;

    // Almacenamos los distintos prefabs que tienen los colores de los diferentes bodies
    public GameObject[] raceCarColors = new GameObject[4];

    #endregion Variables

    #region NAME

    /// <summary>
    /// Para que un jugador esté listo, tiene que haber introducido un nombre correcto
    /// y haber pulsado el botón de que está listo. Cuando el jugador modifica su nombre,
    /// se envía al servidor esta nueva información, y cuando está listo para empezar
    /// también le enviamos al servidor esta información para que se lo notifique a los otros clientes
    /// </summary>
    public void PlayerReady()
    {
        if (isLocalPlayer)
        {
            m_UIManager.CheckData();
            m_PlayerInfo.Name = m_UIManager.inputFieldName.text;
            CmdNameToServer(m_PlayerInfo.Name);

            if (m_PlayerInfo.Name.Length != 0)
            {
                m_UIManager.panelWaiting.gameObject.SetActive(true);
                CmdNewPlayerReady();
            }
        }
    }

    /// <summary>
    /// The Hook method must have two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
    /// The Hook is always called after the property value is set. You don't need to set it yourself.
    /// The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    /// </summary>
    /// <param name="oldName">Nombre del jugador antes de ser modificada la variable m_Name</param>
    /// <param name="newName">Nombre del jugador después de ser modificada la variable m_Name</param>
    public void SetName(string oldName, string newName)
    {
        m_NameController.PlayerName.text = newName;
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
    /// The Hook method must have two parameters of the same type as the SyncVar property. One for the old value, one for the new value.
    /// The Hook is always called after the property value is set. You don't need to set it yourself.
    /// The Hook only fires for changed values, and changing a value in the inspector will not trigger an update.
    /// </summary>
    /// <param name="oldColor">color del coche del jugador antes de ser modificada la variable m_Color</param>
    /// <param name="newColor">color del coche del jugador después de ser modificada la variable m_Color</param>
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
        // Mutex para los jugadores que estén intentando entrar al mismo tiempo que un jugador trata de ponerse en listo
        m_PolePositionManager.mutex.WaitOne();
        m_ID = connectionToClient.connectionId;
        if (!m_PolePositionManager.gameStarted)
        {
            // Se busca una posición disponible para colocar al jugador, y si se encuentra se inicializa y se añade a la lista
            for (int i = 0; i < m_PolePositionManager.playersConnected.Length; i++)
            {
                if (!m_PolePositionManager.playersConnected[i])
                {
                    m_PlayerInfo.ID = i;
                    m_PlayerInfo.CurrentLap = 0;
                    if (isClient)
                    {
                        m_UIManager.readyButton.onClick.AddListener(() => PlayerReady());
                        m_UIManager.changeColorButton.onClick.AddListener(() => ChangeColor());
                        if (isLocalPlayer)
                        {
                            m_UIManager.ActivateSelectHUD();
                        }
                    }
                    
                    this.gameObject.transform.position = NetworkManager.startPositions[i].position;
                    this.gameObject.transform.rotation = NetworkManager.startPositions[i].rotation;
                    m_PolePositionManager.AddPlayer(m_PlayerInfo);

                    m_PolePositionManager.mutex.ReleaseMutex();
                    return;
                }
            }
            // Si llega aquí es porque están todos los huecos ocupados
            Debug.Log("No pueden entrar más jugadores a la partida");
        }
        else
        {
            Debug.Log("Partida Empezada");
        }
        m_PolePositionManager.mutex.ReleaseMutex();
    }

    /// <summary>
    /// Called on every NetworkBehaviour when it is activated on a client.
    /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isClientOnly)
        {
            if (!m_PolePositionManager.gameStarted)
            {
                // Se busca una posición disponible para colocar al jugador, y si se encuentra se inicializa y se añade a la lista
                for (int i = 0; i < m_PolePositionManager.playersConnected.Length; i++)
                {
                    if (!m_PolePositionManager.playersConnected[i])
                    {
                        m_PlayerInfo.ID = i;
                        m_PlayerInfo.CurrentLap = 0;

                        m_UIManager.readyButton.onClick.AddListener(() => PlayerReady());
                        m_UIManager.changeColorButton.onClick.AddListener(() => ChangeColor());

                        this.gameObject.transform.position = NetworkManager.startPositions[i].position;
                        this.gameObject.transform.rotation = NetworkManager.startPositions[i].rotation;
                        m_PolePositionManager.AddPlayer(m_PlayerInfo);
                        if (isLocalPlayer)
                        {
                            m_UIManager.ActivateSelectHUD();
                        }
                        return;
                    }
                }
                // Si llega aquí es porque están todos los huecos ocupados
                Debug.Log("No pueden entrar más jugadores a la partida");
                if (isLocalPlayer)
                {
                    m_UIManager.StartErrorMenu("The game is full");
                }
            }
            else
            {
                if (isLocalPlayer)
                {
                    Debug.Log("Partida Empezada");
                    m_UIManager.StartErrorMenu("The game has already started");
                }
            }
        }
    }

    /// <summary>
    /// Cuando se destruye al jugador (al salir de la partida, o al cerrar el servidor) se elimina de la lista y de la partida
    /// para el resto de jugadores
    /// </summary>
    private void OnDestroy()
    {
        m_PolePositionManager.RemovePlayer(m_PlayerInfo);
    }

    #endregion Start & Stop Callbacks

    #region START

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<PolePositionNetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
        m_NameController = GetComponent<NameController>();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            ConfigureCamera();
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Se le da control al jugador
    /// </summary>
    public void StartPlayer()
    {
        if (isLocalPlayer)
        {
            m_PlayerController.enabled = true;
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            m_PlayerController.OnCrashDelegate += m_UIManager.ShowCrashError;
        }
    }

    // Actualización del UI de la velocidad
    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int)speed * 5); // 5 for visualization purpose (km/h)
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }

    #endregion Methods

    #region Commands

    // For security, Commands can only be sent from YOUR player object by default, 
    // so you cannot control the objects of other players.

    //////////////////
    ///////NAME///////
    //////////////////
    /// <summary>
    /// Cuando un jugador modifica su nombre se envía esta información al servidor 
    /// se modifica la variable compartida en el servidor y se comunica el cambio al resto de clientes
    /// </summary>
    /// <param name="name">Nombre recibido en el command por el cliente</param>
    [Command]
    private void CmdNameToServer(string name)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Name = name;
        m_NameController.PlayerName.text = name;
        m_PlayerInfo.Name = name;
    }

    //////////////////
    ///PLAYER READY///
    //////////////////
    /// <summary>
    /// Cuando un jugador está preparado se aumenta en el servidor la varíable que cuenta
    /// el número de jugadores preparados y se comunica el cambio al resto de clientes.
    /// </summary>
    [Command]
    public void CmdNewPlayerReady()
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_PolePositionManager.NewPlayerReady(this.gameObject);
    }

    //////////////////
    ///////COLOR//////
    //////////////////
    /// <summary>
    /// Cuando un jugador modifica su color se envía esta información al servidor 
    /// se modifica la variable compartida en el servidor y se comunica el cambio al resto de clientes
    /// </summary>
    /// <param name="color">Color recibido en el command del cliente que lo invoca</param>
    [Command]
    private void CmdColorToServer(int color)
    {
        GetComponent<NetworkIdentity>().AssignClientAuthority(this.GetComponent<NetworkIdentity>().connectionToClient);
        m_Color = color;
        this.GetComponentInChildren<MeshRenderer>().materials = raceCarColors[color].GetComponent<MeshRenderer>().sharedMaterials;
    }

    #endregion Commands

}