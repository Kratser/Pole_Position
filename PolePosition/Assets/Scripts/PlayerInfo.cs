using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    public string Name { get; set; }

    public int ID { get; set; }

    public int CurrentPosition { get; set; }

    public float TotalDistance { set; get; }

    public int CurrentLap { get; set; }

    public int Color { get; set; }

    public float StartTime { get; set; }
    public float FinishTime { get; set; }

    public bool IsReady { get; set; }

    public override string ToString()
    {
        return Name;
    }
}