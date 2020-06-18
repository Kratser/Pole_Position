using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    public class PolePositionNetworkManager : NetworkManager
    {

        public PolePositionManager m_PolePositionManager;

        /// <summary>
        /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
        /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            
            /**/
            if (!m_PolePositionManager.gameStarted && m_PolePositionManager.numPlayers < 4)
            {
                Transform startPos = GetStartPosition();
                GameObject player = startPos != null
                    ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
                    : Instantiate(playerPrefab);

                NetworkServer.AddPlayerForConnection(conn, player);
            }
            else
            {
                GameObject player = Instantiate(playerPrefab);

                player.SetActive(false);

                NetworkServer.AddPlayerForConnection(conn, player);
            }
            /**/
        }
    }
}