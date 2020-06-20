using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NameController : MonoBehaviour
{
    // Texto que aparece sobre el coche del juegador
    public Text PlayerName;
    // Altura a la que se muestra el texto sobre el jugador
    public float height = 55;

    // Update is called once per frame
    void FixedUpdate()
    {
        // Nombre del jugador que aparece sobre su coche. Cambiamos posiciones con respecto a la cámara.
        Vector3 namePos = Camera.main.WorldToScreenPoint(this.transform.position);
        PlayerName.transform.position = namePos + new Vector3(0, height, 0);
    }
}
