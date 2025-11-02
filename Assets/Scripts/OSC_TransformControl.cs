using UnityEngine;
using extOSC;
// We don't need System.Collections.Generic for this new version.

/// <summary>
/// Receives OSC messages to control the object's Rotation (Quaternion)
/// and/or Scale (Float).
/// Auto-creates an OSCReceiver if one is not assigned.
/// </summary>
[AddComponentMenu("extOSC/OSC Transform Control")]
public class OSC_TransformControl : MonoBehaviour
{
    //--------------------------------------------------------------------------
    // PUBLIC VARS
    //--------------------------------------------------------------------------

    [Header("OSC Settings")]
    [Tooltip("(Optional) Assign a receiver. If left null, one will be created automatically on this GameObject.")]
    public OSCReceiver Receiver;

    [Tooltip("The port to listen on. This is only used if a Receiver is not manually assigned.")]
    public int LocalPort = 7000;

    // -- ROTATION --
    [Header("Rotation Settings")]
    public bool ControlRotation = true;
    [Tooltip("The OSC address for quaternion data (4 floats).")]
    public string Address_Rotation = "/rotate/quaternion";
    public bool SmoothRotation = true;
    public float SmoothSpeed_Rotation = 10f;

    // -- SCALE --
    [Header("Scale Settings")]
    public bool ControlScale = true;
    [Tooltip("The OSC address for mic level data.")]
    public string Address_Scale = "/mic/level";

    [Tooltip("The expected input range from your OSC message (e.g., -32 to 0).")]
    public float MinInput = -32f;
    [Tooltip("The expected input range from your OSC message (e.g., -32 to 0).")]
    public float MaxInput = 0f;

    [Tooltip("The target scale when input is at MinInput.")]
    public float MinScale = 0.5f;
    [Tooltip("The target scale when input is at MaxInput.")]
    public float MaxScale = 2.0f;

    public bool SmoothScale = true;
    public float SmoothSpeed_Scale = 10f;

    //--------------------------------------------------------------------------
    // PRIVATE VARS
    //--------------------------------------------------------------------------

    // Rotation
    private Quaternion targetRotation;
    private bool newRotationReceived = false;

    // Scale
    private Vector3 initialScale;
    private float targetUniformScale = 1f;
    private bool newScaleReceived = false;

    //--------------------------------------------------------------------------
    // UNITY METHODS
    //--------------------------------------------------------------------------

    void Start()
    {
        // --- Auto-configure Receiver ---
        if (Receiver == null)
        {
            Receiver = GetComponent<OSCReceiver>();
            if (Receiver == null)
            {
                Receiver = gameObject.AddComponent<OSCReceiver>();
                Receiver.LocalPort = LocalPort;
            }
        }

        // --- Bind Handlers ---
        // This is where the script binds to your TWO DIFFERENT addresses
        if (ControlRotation)
        {
            Receiver.Bind(Address_Rotation, OnReceiveQuaternion);
        }

        if (ControlScale)
        {
            Receiver.Bind(Address_Scale, OnReceiveMicLevel);
        }

        // --- Initialize States ---
        targetRotation = transform.rotation;
        initialScale = transform.localScale;
        targetUniformScale = 1f; // Start at 1x the initial scale
    }

    void Update()
    {
        // Apply Rotation
        if (ControlRotation && newRotationReceived)
        {
            if (SmoothRotation)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * SmoothSpeed_Rotation);
            }
            else
            {
                transform.rotation = targetRotation;
                newRotationReceived = false; // Reset flag
            }
        }

        // Apply Scale
        if (ControlScale && newScaleReceived)
        {
            // Calculate the target scale vector
            Vector3 targetScaleVector = initialScale * targetUniformScale;

            if (SmoothScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScaleVector, Time.deltaTime * SmoothSpeed_Scale);
            }
            else
            {
                transform.localScale = targetScaleVector;
                newScaleReceived = false; // Reset flag
            }
        }
    }

    //--------------------------------------------------------------------------
    // PRIVATE METHODS (HANDLERS)
    //--------------------------------------------------------------------------

    // --- Rotation Handler ---
    private void OnReceiveQuaternion(OSCMessage message)
    {
        // This helper (ToQuaternion) DOES exist and is correct.
        if (message.ToQuaternion(out Quaternion rotation))
        {
            targetRotation = rotation;
            newRotationReceived = true;
        }
        else
        {
            Debug.LogWarning($"[OSC_TransformControl] Invalid Quaternion at {Address_Rotation}. Expected 4 floats.");
        }
    }

    // --- Scale Handler (FIXED) ---
    private void OnReceiveMicLevel(OSCMessage message)
    {
        // Check if the message has at least two values
        if (message.Values.Count < 2)
        {
            Debug.LogWarning($"[OSC_TransformControl] Message at {Address_Scale} needs at least 2 values. Found {message.Values.Count}.");
            return;
        }

        // Check if the second value (index 1) is a float
        if (message.Values[1].Type == OSCValueType.Float)
        {
            // Get the float value directly
            float micLevel = message.Values[1].FloatValue;

            // Map the value from the input range to the output scale range
            targetUniformScale = MapValue(micLevel, MinInput, MaxInput, MinScale, MaxScale);

            newScaleReceived = true;
        }
        else
        {
            Debug.LogWarning($"[OSC_TransformControl] Second value at {Address_Scale} is not a float. Type is {message.Values[1].Type}.");
        }
    }

    /// <summary>
    /// A helper function to remap a value from one range to another.
    /// </summary>
    private float MapValue(float value, float inMin, float inMax, float outMin, float outMax)
    {
        // Use Unity's built-in math functions which are very efficient.
        // InverseLerp finds the 't' (0-1) of the value in the input range
        // Lerp finds the value at 't' in the output range

        // Clamp the value first
        value = Mathf.Clamp(value, inMin, inMax);

        float normalized = Mathf.InverseLerp(inMin, inMax, value);
        return Mathf.Lerp(outMin, outMax, normalized);
    }
}