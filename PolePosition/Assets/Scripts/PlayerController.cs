﻿using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.UI;

/*
	Documentation: https://mirror-networking.com/docs/Guides/NetworkBehaviour.html
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkBehaviour.html
*/

public class PlayerController : NetworkBehaviour
{
    #region Variables

    [Header("Movement")] public List<AxleInfo> axleInfos;
    public float forwardMotorTorque = 100000;
    public float backwardMotorTorque = 50000;
    public float maxSteeringAngle = 15;
    public float engineBrake = 1e+12f;
    public float footBrake = 1e+24f;
    public float topSpeed = 200f;
    public float downForce = 100f;
    public float slipLimit = 0.2f;

    private float CurrentRotation { get; set; }
    private float InputAcceleration { get; set; }
    private float InputSteering { get; set; }
    private float InputBrake { get; set; }

    private PlayerInfo m_PlayerInfo;

    private Rigidbody m_Rigidbody;
    private Transform m_Transform;
    private float m_SteerHelper = 0.8f;

    private float m_CurrentSpeed = 0;

    private float Speed
    {
        get { return m_CurrentSpeed; }
        set
        {
            if (Math.Abs(m_CurrentSpeed - value) < float.Epsilon)
            {
                return;
                m_CurrentSpeed = 0.0f;
            }
            m_CurrentSpeed = value;
            // Si el delegado tiene alguna función asociada (void Func(float valor);)
            if (OnSpeedChangeEvent != null)
                // Se llama a las funciones asociadas al delegado con la nueva velocidad
                OnSpeedChangeEvent(m_CurrentSpeed);
        }
    }

    // Delegados
    public delegate void OnSpeedChangeDelegate(float newVal);
    public event OnSpeedChangeDelegate OnSpeedChangeEvent;

    public delegate void OnCrashEvent(string msg);
    public OnCrashEvent OnCrashDelegate;

    #endregion Variables

    #region Unity Callbacks

    public void Awake()
    {
        m_Transform = GetComponent<Transform>();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_PlayerInfo = GetComponent<PlayerInfo>();
    }

    public void Update()
    {
        //InputAcceleration = Input.GetAxis("Vertical");
        //InputSteering = Input.GetAxis("Horizontal");
        //InputBrake = Input.GetAxis("Jump");
        Speed = m_Rigidbody.velocity.magnitude;
    }

    public void FixedUpdate()
    {
        InputAcceleration = Mathf.Clamp(Input.GetAxis("Vertical"), -1, 1);
        InputSteering = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
        InputBrake = Mathf.Clamp(Input.GetAxis("Jump"), 0, 1);

        // Para resetear la posición del jugador si ha volcado, se debe pulsar la barra espaciadora
        if (Input.GetKey(KeyCode.Space))
        {
            ResetPlayer();
        }

        float steering = maxSteeringAngle * InputSteering;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor)
            {
                if (InputAcceleration > float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = forwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = forwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (InputAcceleration < -float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.leftWheel.brakeTorque = 0;
                    axleInfo.rightWheel.motorTorque = -backwardMotorTorque;
                    axleInfo.rightWheel.brakeTorque = 0;
                }

                if (Math.Abs(InputAcceleration) < float.Epsilon)
                {
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.leftWheel.brakeTorque = engineBrake;
                    axleInfo.rightWheel.motorTorque = 0;
                    axleInfo.rightWheel.brakeTorque = engineBrake;
                }

                if (InputBrake > 0)
                {
                    axleInfo.leftWheel.brakeTorque = footBrake;
                    axleInfo.rightWheel.brakeTorque = footBrake;
                }
            }

            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }

        SteerHelper();
        SpeedLimiter();
        AddDownForce();
        TractionControl();
        CheckCrash();

    }

    #endregion Unity Callbacks

    #region Methods

    // crude traction control that reduces the power to wheel if the car is wheel spinning too much
    private void TractionControl()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit wheelHitLeft;
            WheelHit wheelHitRight;
            axleInfo.leftWheel.GetGroundHit(out wheelHitLeft);
            axleInfo.rightWheel.GetGroundHit(out wheelHitRight);

            if (wheelHitLeft.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitLeft.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.leftWheel.motorTorque -= axleInfo.leftWheel.motorTorque * howMuchSlip * slipLimit;
            }

            if (wheelHitRight.forwardSlip >= slipLimit)
            {
                var howMuchSlip = (wheelHitRight.forwardSlip - slipLimit) / (1 - slipLimit);
                axleInfo.rightWheel.motorTorque -= axleInfo.rightWheel.motorTorque * howMuchSlip * slipLimit;
            }
            //axleInfo.leftWheel.sidewaysFriction.extremumSlip
        }
    }

    // this is used to add more grip in relation to speed
    private void AddDownForce()
    {
        foreach (var axleInfo in axleInfos)
        {
            axleInfo.leftWheel.attachedRigidbody.AddForce(
                -transform.up * (downForce * axleInfo.leftWheel.attachedRigidbody.velocity.magnitude));
        }
    }

    private void SpeedLimiter()
    {
        float speed = m_Rigidbody.velocity.magnitude;
        if (speed > topSpeed)
            m_Rigidbody.velocity = topSpeed * m_Rigidbody.velocity.normalized;
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider col)
    {
        if (col.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = col.transform.GetChild(0);
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);
        var myTransform = visualWheel.transform;
        myTransform.position = position;
        myTransform.rotation = rotation;
    }

    private void SteerHelper()
    {
        foreach (var axleInfo in axleInfos)
        {
            WheelHit[] wheelHit = new WheelHit[2];
            axleInfo.leftWheel.GetGroundHit(out wheelHit[0]);
            axleInfo.rightWheel.GetGroundHit(out wheelHit[1]);
            foreach (var wh in wheelHit)
            {
                if (wh.normal == Vector3.zero)
                    return; // wheels arent on the ground so dont realign the rigidbody velocity
            }
        }

        // this if is needed to avoid gimbal lock problems that will make the car suddenly shift direction
        if (Mathf.Abs(CurrentRotation - transform.eulerAngles.y) < 10f)
        {
            var turnAdjust = (transform.eulerAngles.y - CurrentRotation) * m_SteerHelper;
            Quaternion velRotation = Quaternion.AngleAxis(turnAdjust, Vector3.up);
            m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
        }

        CurrentRotation = transform.eulerAngles.y;
    }

    #region Crash

    /// <summary>
    /// Método que comrpueba si el coche se ha girado, y muestra un mensaje
    /// por pantalla para avisar al jugador
    /// </summary>
    public void CheckCrash()
    {
        // Si está sufriendo una rotación muy elevada en x o z significa que el coche ha volcado
        if ((m_Rigidbody.rotation.eulerAngles.x > 45 && m_Rigidbody.rotation.eulerAngles.x < 315)
         || (m_Rigidbody.rotation.eulerAngles.z > 45 && m_Rigidbody.rotation.eulerAngles.z < 315))
        {
            // Se muestra el mensaje para dar la vuelta al jugador y volver a la carrera
            OnCrashDelegate("Press Space bar to recover");
        }
    }

    /// <summary>
    /// Método que reinicia al jugador en la misma posición que su esfera, para
    /// que pueda volver a la carrera en caso de accidente
    /// </summary>
    public void ResetPlayer()
    {
        PolePositionManager m_PolePositionManager = FindObjectOfType<PolePositionManager>();

        // Accedemos a las esferas de debug para que a la hora de resetear la posición, el coche se centre en la carretera
        Vector3 newPos = m_PolePositionManager.m_DebuggingSpheres[m_PolePositionManager.m_Players.IndexOf(m_PlayerInfo)].GetComponent<Transform>().position;
        m_Transform.position = newPos + new Vector3(0, 1, 0);

        // Reseteamos la posición del jugador haciendo que mire en la dirección en la que se desarrolla la carrera
        Vector3 newRotation = m_PolePositionManager.m_CircuitController.m_PathPos[m_PlayerInfo.CurrentSegment + 1] - m_PolePositionManager.m_CircuitController.m_PathPos[m_PlayerInfo.CurrentSegment];
        m_Transform.rotation = Quaternion.LookRotation(newRotation, new Vector3(0, 1, 0));
        OnCrashDelegate("");
    }

    #endregion Crash

    #endregion
}