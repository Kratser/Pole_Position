using System;
using Mirror;
using UnityEngine;
using Random = System.Random;
using UnityEngine.UI;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class SetupPlayer : NetworkBehaviour
{
    [SyncVar] private int m_ID;
    [SyncVar] private string m_Name;

    private UIManager m_UIManager;
    private NetworkManager m_NetworkManager;
    private PlayerController m_PlayerController;
    private PlayerInfo m_PlayerInfo;
    private PolePositionManager m_PolePositionManager;

    public Material[] raceCarMaterials = new Material[4];

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

        m_UIManager.readyButton.onClick.AddListener(()=>ChangeName());

        //Cambiar el color del jugador
        //this.transform.GetChild(0).gameObject.transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().materials[2] = 

        m_PolePositionManager.AddPlayer(m_PlayerInfo);
    }

    /// <summary>
    /// Actualizar el nombre sobre el coche con el nombre introducido por este
    /// </summary>
    public void ChangeName()
    {
        m_PlayerInfo.Name = m_UIManager.PlayerUserName;
        // Hacer que el nombre aparezca sobre el jugador
        m_PlayerController.PlayerName.text = m_PlayerInfo.Name;
    }

    /// <summary>
    /// Called when the local player object has been set up.
    /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
    /// </summary>
    public override void OnStartLocalPlayer()
    {
    }

    #endregion

    private void Awake()
    {
        m_PlayerInfo = GetComponent<PlayerInfo>();
        m_PlayerController = GetComponent<PlayerController>();
        m_NetworkManager = FindObjectOfType<NetworkManager>();
        m_PolePositionManager = FindObjectOfType<PolePositionManager>();
        m_UIManager = FindObjectOfType<UIManager>();
        //raceCarMaterials[0] = find
    }

    // Start is called before the first frame update
    void Start()
    {
        if (isLocalPlayer)
        {
            m_PlayerController.enabled = true;
            m_PlayerController.OnSpeedChangeEvent += OnSpeedChangeEventHandler;
            ConfigureCamera();
        }
    }

    // Actualización del UI de la velocidad CAMBIAR!!!
    void OnSpeedChangeEventHandler(float speed)
    {
        m_UIManager.UpdateSpeed((int) speed * 5); // 5 for visualization purpose (km/h)
    }

    void ConfigureCamera()
    {
        if (Camera.main != null) Camera.main.gameObject.GetComponent<CameraController>().m_Focus = this.gameObject;
    }
}