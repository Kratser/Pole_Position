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

    #region Unity Canvas Variables
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

    [Header("In-Game HUD")] [SerializeField] private GameObject inGameHUD;
    [SerializeField] private Text textSpeed;
    [SerializeField] private Text textLaps;
    [SerializeField] private Text textPosition;
    [SerializeField] public Text textCountDown;

    [Header("Ranking")] [SerializeField] private GameObject rankingHUD;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text textRanking;
    [SerializeField] private Text textTimes;
    [SerializeField] private Text textFinish;
    [SerializeField] private Text textWaitingPlayers;
    #endregion

    private void Awake()
    {
        m_NetworkManager = FindObjectOfType<NetworkManager>();
    }

    private void Start()
    {
        ActivateMainMenu();
        buttonHost.onClick.AddListener(() => StartHost());
        buttonClient.onClick.AddListener(() => StartClient());
        buttonServer.onClick.AddListener(() => StartServer());
    }

    #region Activate Menus

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

    #endregion

    /// <summary>
    /// Se inicia el menu de selección y le asignamos al botón ready 
    /// del menú de selección la comprobación de que se haya introducido 
    /// el campo username bien para poder empezar con la carrera
    /// </summary>
    private void StartSelectMenu()
    {
        readyButton.onClick.AddListener(() => CheckData());
        ActivateSelectHUD();
    }

    /// <summary>
    /// Para el campo dónde el jugador introduce su nombre, queremos comprobar
    /// que ha introducido un nombre, que no se repite con otro jugador y que
    /// no se pasa de la longitud máxima establecida.
    /// </summary>
    public void CheckData()
    {
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

    #region Start Network

    private void StartHost()
    {
        m_NetworkManager.StartHost();
        StartSelectMenu();
    }

    private void StartClient()
    {
        m_NetworkManager.StartClient();
        m_NetworkManager.networkAddress = inputFieldIP.text;
        StartSelectMenu();
    }

    private void StartServer()
    {
        m_NetworkManager.StartServer();
        StartSelectMenu();
    }

    #endregion

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
        //Debug.LogWarning("LAPS BEFORE" + laps);
        textLaps.text = "LAP: " + laps + "/3";
        //textCountDown.fontSize = 45;
        //textCountDown.text = "LAP: " + laps + "/3";
        ////Debug.LogWarning("LAPS AFTER" + laps);
        //Thread.Sleep(1500);
        //textCountDown.text = "";
        //textCountDown.fontSize = 100;
    }

    public void WrongDirection(string msg)
    {
        textCountDown.fontSize = 45;
        textCountDown.text = msg;
        Thread.Sleep(500);
        textCountDown.text = "";
        textCountDown.fontSize = 100;
    }

    public void ChangeRankingHUD(string[] positions, string[] times)
    {
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

    #endregion
}