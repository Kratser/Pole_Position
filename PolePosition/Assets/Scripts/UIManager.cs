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

    private NetworkManager m_NetworkManager;

    [Header("Main Menu")] [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonClient;
    [SerializeField] private Button buttonServer;
    [SerializeField] private InputField inputFieldIP;

    [Header("Select Menu")] [SerializeField] private GameObject selectMenu;
    [SerializeField] public Button changeColorButton;
    [SerializeField] public Button readyButton;
    [SerializeField] private InputField inputFieldName;
    [SerializeField] public string PlayerUserName { get; set; }
    [SerializeField] public int PlayerColor { get; set; }
    [SerializeField] private Text errorInsertNameText;

    [Header("In-Game HUD")] [SerializeField]
    private GameObject inGameHUD;

    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] public Text textCountDown;


    [Header("Ranking")] [SerializeField] private GameObject rankingHUD;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text testRank;
    [SerializeField] private Text textFinish;

    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
    }

    private void Start()
    {
        buttonHost.onClick.AddListener(() => StartSelectMenu(0));
        buttonClient.onClick.AddListener(() => StartSelectMenu(1));
        buttonServer.onClick.AddListener(() => StartSelectMenu(2));
        ActivateMainMenu();
    }

    private void ActivateMainMenu()
    {
        mainMenu.SetActive(true);
        inGameHUD.SetActive(false);
    }

    private void ActivateInGameHUD()
    {
        selectMenu.SetActive(false);
        inGameHUD.SetActive(true);
    }

    private void ActivateSelectHUD()
    {
        mainMenu.SetActive(false);
        selectMenu.SetActive(true);
    }
    public void ActivateRankingHUD()
    {
        rankingHUD.SetActive(true);
        inGameHUD.SetActive(false);
    }
    /// <summary>
    /// Comienza la partida (cuando estén todos listos)
    /// </summary>
    /// <param name="c">Tipo de conexión (Host, Cliente o Servidor)</param>
    public void StartConnection(int c)
    {
        switch (c)
        {
            case 0:
                StartHost();
                break;
            case 1:
                StartClient();
                break;
            case 2:
                StartServer();
                break;
            default:
                break;
        }
    }

    public void CheckData(int c)
    {
        // Comprobar si los datos introducidos son correctos
        if (inputFieldName.text.Length != 0)
        {
            // Comprobar el nombre con los demás usuarios
            PlayerUserName = inputFieldName.text;
            ActivateInGameHUD();
        }
        else
        {

            // Mensaje "Introduce Nombre de Usuario"
            errorInsertNameText.enabled = true;
        }
    }

    /// <summary>
    /// Se inicia el menu de selección, y cuando se han elegido el coche y el nombre de usuario,
    /// se inicia la conexión
    /// </summary>
    /// <param name="connection">Parámetro que guarda la opción seleccionada del Menú Principal</param>
    private void StartSelectMenu(int connection)
    {
        readyButton.onClick.AddListener(() => CheckData(connection));
        ActivateSelectHUD();
        StartConnection(connection);
    }

    private void StartHost()
    {
        m_NetworkManager.StartHost();
    }

    private void StartClient()
    {
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
    }

    #region UI Delegates

    public void UpdateSpeed(int speed)
    {
        textSpeed.text = "Speed " + speed + " Km/h";
    }

    public void ChangeOrder(string newOrder)
    {
        textPosition.text = newOrder;
    }

    public void CountDown(string time)
    {
        textCountDown.text = time;
    }

    public void UpdateLap (string laps)
    {
        textLaps.text = "LAP: " + laps + "/ 3";
        textCountDown.fontSize = 45;
        textCountDown.text = "LAP: " + laps + "/3";
        Thread.Sleep(1500);
        textCountDown.text = "";
        textCountDown.fontSize = 100;
    }

    public void WrongDirection(string msg)
    {
        textCountDown.text = msg;
        Thread.Sleep(500);
        textCountDown.text = "";
    }

    #endregion
}