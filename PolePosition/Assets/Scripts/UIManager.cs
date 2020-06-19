﻿using System;
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

    private void Awake()
    {
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
    }

    #endregion Activate Menus

    /// <summary>
    /// Se inicia el menu de selección y le asignamos al botón ready 
    /// del menú de selección la comprobación de que se haya introducido 
    /// el campo username bien para poder empezar con la carrera
    /// </summary>
    public void StartSelectMenu()
    {
        ActivateSelectHUD();
    }

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

    public void StartErrorMenu(string errorMsg)
    {
        returnButton.onClick.AddListener(() => CloseConnection());
        textErrorMsg.text = errorMsg;
        ActivateErrorMenu();
    }

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
        if (inputFieldName.text.Length != 0)
        {
            // Comprobar el nombre con los demás usuarios JAJA
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

    public void ChangeOrder(string newOrder)
    {
        string[] order = newOrder.Split(' ');
        newOrder = "";
        for (int i = 0; i < order.Length-1; i++)
        {
            newOrder += (i + 1) + ". " + order[i]+ "\n";
        }
        textPosition.text = newOrder;
        
    }
    public void ChangeOrderServer(string newOrder)
    {
        string[] order = newOrder.Split(' ');
        newOrder = "";
        for (int i = 0; i < order.Length-1; i++)
        {
            newOrder += (i + 1) + "# " + order[i] + "\n\n";
        }
        textOrder.text = newOrder;
    }

    public void CountDown(string time)
    {
        textCountDown.text = time;
    }

    public void UpdateLap (string laps)
    {
        textLaps.text = laps;
    }

    public void WrongDirection(string msg)
    {
        textCountDown.fontSize = 45;
        textCountDown.text = msg;
        Thread.Sleep(1000);
        textCountDown.text = "";
        textCountDown.fontSize = 100;
    }

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

    public void ShowCrashError(string msg)
    {
        textCountDown.text = msg;
    }

    public void UpdateTimeLap (string time)
    {
        textTimeLaps.text = time;
    }

    #endregion UI Delegates
}