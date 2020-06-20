using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public bool showGUI = true;

    private PolePositionNetworkManager m_NetworkManager;
    public PolePositionManager m_PolePositionManager;

    #region Unity Canvas Variables

    [Header("Main Menu")] [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;

    [Header("Select Menu")] [SerializeField] private GameObject selectMenu;
    [SerializeField] public Button changeColorButton;
    [SerializeField] public Button readyButton;
    [SerializeField] public InputField inputFieldName;
    [SerializeField] public int PlayerColor { get; set; }
    [SerializeField] private Text errorInsertNameText;

    [Header("In-Game HUD")] [SerializeField] private GameObject inGameHUD;
    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] public Text textCountDown;
    [SerializeField] public Text textTimeLaps;
    [SerializeField] public Image panelWaiting;

    [Header("RankingHUD")] [SerializeField] private GameObject rankingHUD;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text textRanking;
    [SerializeField] private Text textTimes;
    [SerializeField] private Text textFinish;
    [SerializeField] private Text textWaitingPlayers;

    [Header("ServerHUD")] [SerializeField] private GameObject serverHUD;
    [SerializeField] private Text textOrder;
    [SerializeField] private Button closeButton;

    [Header("ErrorMenu")] [SerializeField] private GameObject errorMenu;
    [SerializeField] private Text textErrorMsg;
    [SerializeField] private Button returnButton;

    #endregion Unity Canvas Variables

    #region Awake & Start

    private void Awake()
    {
        // Se buscan las referencias del PolePositionNetworkManager y del PolePositionManager
        if (m_NetworkManager == null) m_NetworkManager = FindObjectOfType<PolePositionNetworkManager>();
        if (m_PolePositionManager == null) m_PolePositionManager = FindObjectOfType<PolePositionManager>();
    }

    private void Start()
    {
        ActivateMainMenu();
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
    }

    #endregion Awake & Start

    #region Activate Menus

    public void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
        rankingHUD.SetActive(false);
        selectMenu.SetActive(false);
        serverHUD.SetActive(false);
        errorMenu.SetActive(false);
    }

    private void ActivateInGameHUD()
    {
        selectMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    public void ActivateSelectHUD()
    {
        mainMenu.SetActive(false);
        selectMenu.SetActive(true);
    }

    public void ActivateRankingHUD()
    {
        rankingHUD.SetActive(true);
        inGameHUD.SetActive(false);
    }
    public void ActivateServerHUD()
    {
        serverHUD.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void ActivateErrorMenu()
    {
        errorMenu.SetActive(true);
        mainMenu.SetActive(false);
        selectMenu.SetActive(false);
        inGameHUD.SetActive(false);
    }

    #endregion Activate Menus

    #region Methods

    /// <summary>
    /// Se inicia el menú del servidor y asignamos al botón "close"
    /// la función para cerrar el servidor
    /// </summary>
    private void StartServerMenu()
    {
        closeButton.onClick.AddListener(() => CloseConnection());
        ActivateServerHUD();
        Camera.main.transform.position = new Vector3(-106, 13, 87);
        Camera.main.transform.rotation = Quaternion.Euler(28, 54, 0);
    }

    /// <summary>
    /// Se activa el menú de error y asignamos al botón de "return"
    /// la función de volver al menú inical
    /// </summary>
    /// <param name="errorMsg"></param>
    public void StartErrorMenu(string errorMsg)
    {
        returnButton.onClick.AddListener(() => CloseConnection());
        textErrorMsg.text = errorMsg;
        ActivateErrorMenu();
    }

    /// <summary>
    /// Se cierra la conexión y se reinicia el juego
    /// </summary>
    private void CloseConnection()
    {
        m_PolePositionManager.ResetGame();
    }

    /// <summary>
    /// Para el campo dónde el jugador introduce su nombre, queremos comprobar
    /// que ha introducido un nombre, que no se repite con otro jugador y que
    /// no se pasa de la longitud máxima establecida.
    /// </summary>
    public void CheckData()
    {
        // Si se ha escrito un nombre válido, se inicia el HUD del juego
        if (inputFieldName.text.Length != 0)
        {
            ActivateInGameHUD();
        }
        else
        {
            // Mensaje "Introduce Nombre de Usuario Válido"
            errorInsertNameText.enabled = true;
        }
    }

    #endregion Methods

    #region Start Network

    private void StartHost()
    {
        m_NetworkManager.StartHost();
        ActivateSelectHUD();
    }

    private void StartClient()
    {
        if (inputFieldIP.text == "")
        {
            m_NetworkManager.networkAddress = "localhost";
        }
        else
        {
            m_NetworkManager.networkAddress = inputFieldIP.text;
        }
        m_NetworkManager.StartClient();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        StartServerMenu();
    }

    #endregion Start Network

    #region UI Delegates

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    /// <summary>
    /// Método que se encarga de modificar el texto de la GUI que muestra 
    /// el orden de los jugadores
    /// </summary>
    /// <param name="newOrder">Orden de la carrera, separando los nombres por " "</param>
    public void ChangeOrder(string newOrder)
    {
        string[] order = newOrder.Split(',');
        newOrder = "";
        for (int i = 0; i < order.Length-1; i++)
        {
            newOrder += (i + 1) + ". " + order[i]+ "\n";
        }
        textPosition.text = newOrder;
        
    }

    /// <summary>
    /// Método que se encarga de modificar el texto de la GUI que muestra
    /// el orden de los jugadores en el servidor
    /// </summary>
    /// <param name="newOrder">Orden de la carrera, separando los nombres por " "</param>
    public void ChangeOrderServer(string newOrder)
    {
        string[] order = newOrder.Split(',');
        newOrder = "";
        for (int i = 0; i < order.Length-1; i++)
        {
            newOrder += (i + 1) + "# " + order[i] + "\n\n";
        }
        textOrder.text = newOrder;
    }

    /// <summary>
    /// Método que modifica el texto de cuenta atrás
    /// </summary>
    /// <param name="time">string que muestra la cuenta atrás</param>
    public void CountDown(string time)
    {
        textCountDown.text = time;
    }

    /// <summary>
    /// Método que modifica el texto que indica la vuelta del jugador
    /// </summary>
    /// <param name="laps">string que contiene el siguiente formato: "LAP: vuelta / vueltaMax"</param>
    public void UpdateLap (string laps)
    {
        textLaps.text = laps;
    }

    /// <summary>
    /// Texto emergente que aparece si el jugador va en dirección conraria
    /// </summary>
    /// <param name="msg">Mensaje de error</param>
    public void WrongDirection(string msg)
    {
        textCountDown.fontSize = 45;
        textCountDown.text = msg;
        Thread.Sleep(1000);
        textCountDown.text = "";
        textCountDown.fontSize = 100;
    }

    /// <summary>
    /// Método que modifica el HUD del ranking cuando todos los jugadores han acabado la carrera,
    /// mostrando las posiciones y los tiempos totales
    /// </summary>
    /// <param name="positions">Vector con las posiciones</param>
    /// <param name="times">Vector con los tiempos totales de los jugadores</param>
    public void ChangeRankingHUD(string[] positions, string[] times)
    {
        exitButton.onClick.AddListener(() => CloseConnection());
        textFinish.fontSize = 80;
        textFinish.text = "Ranking";
        exitButton.gameObject.SetActive(true);
        textWaitingPlayers.gameObject.SetActive(false);
        textRanking.gameObject.SetActive(true);
        textTimes.gameObject.SetActive(true);

        string positionsText = "";
        string timesText = "";
        for (int i = 0; i < positions.Length; i++)
        {
            positionsText += (i+1)+"# "+positions[i];
            timesText += times[i];
            positionsText += "\n" + "\n";
            timesText += "\n" + "\n";
        }
        textRanking.text = positionsText;
        textTimes.text = timesText;
    }

    /// <summary>
    /// Método que se encarga de reiniciar el HUD del ranking a su estado normal
    /// </summary>
    public void ResetRankingHUD()
    {
        textFinish.fontSize = 100;
        textFinish.text = "Finish";
        exitButton.gameObject.SetActive(false);
        textWaitingPlayers.gameObject.SetActive(true);
        textRanking.gameObject.SetActive(false);
        textTimes.gameObject.SetActive(false);
        textRanking.text = "";
        textTimes.text = "--:--:--";
    }

    /// <summary>
    /// Método que muestra un mensaje para dar la vuelta al jugador
    /// en caso de que se vuelque el coche
    /// </summary>
    /// <param name="msg">Mensaje de error</param>
    public void ShowCrashError(string msg)
    {
        textCountDown.text = msg;
    }

    /// <summary>
    /// Método que modifica el texto que muestra el tiempo por vuelta del jugador
    /// </summary>
    /// <param name="time">Tiempo por vuelta</param>
    public void UpdateTimeLap (string time)
    {
        textTimeLaps.text = time;
    }

    #endregion UI Delegates
}