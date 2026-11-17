using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

// Digital twin of the steering:
//  - Updates the controlled variable with every S: report from the Arduino.
//  - Sends A: only when the USER moves the slider (it never re sends what it received).
//  - Shows which control owns the servo (source POT or DIG in the report).
public class SteeringTwin : MonoBehaviour
{
    [Header("Connection")]
    [FormerlySerializedAs("puente")]
    public SerialBridge bridge;

    [Header("Digital control")]
    [FormerlySerializedAs("deslizador")]
    public Slider slider;
    [FormerlySerializedAs("anguloMin")]
    public int minAngle = 45;               // same as ANGLE_MIN in the firmware
    [FormerlySerializedAs("anguloMax")]
    public int maxAngle = 135;              // same as ANGLE_MAX in the firmware

    [Header("3D model")]
    [FormerlySerializedAs("pivoteIzq")]
    public Transform leftPivot;             // pivot of the front left wheel
    [FormerlySerializedAs("pivoteDer")]
    public Transform rightPivot;            // pivot of the front right wheel
    [FormerlySerializedAs("brazoServo")]
    public Transform servoArm;              // optional: servo arm
    [FormerlySerializedAs("centro")]
    public int center = 90;                 // servo angle with the wheels straight
    [FormerlySerializedAs("relacion")]
    public float ratio = 0.5f;              // wheel degrees per servo degree
    [FormerlySerializedAs("suavizadoVisual")]
    public float visualSmoothing = 20f;     // only cosmetic, between reports

    [Header("Interface")]
    [FormerlySerializedAs("etiqueta")]
    public TMP_Text label;

    // Servo angle received from the Arduino (last S:), the controlled variable
    public int ServoAngle { get; private set; }

    // Smoothed wheel angle in degrees, positive turns right
    public float WheelAngle => (visualAngle - center) * ratio;

    // True when the knob is released (the last report came from a digital command)
    public bool KnobReleased { get; private set; }

    float visualAngle;
    int repeated;                           // consecutive S: reports with the same value
    float lastUserChange = -10f;
    float lastReport;                       // time of the last valid S: report

    void Start()
    {
        ServoAngle = center;
        visualAngle = center;

        // Configure the slider BEFORE listening to it, so no command is sent at start
        slider.wholeNumbers = true;
        slider.minValue = minAngle;
        slider.maxValue = maxAngle;
        slider.SetValueWithoutNotify(center);
        slider.onValueChanged.AddListener(OnSliderMoved);
    }

    void Update()
    {
        ReadMessages();
        AnimateModel();
        UpdateLabel();
    }

    // Digital control: only the user triggers this
    void OnSliderMoved(float value)
    {
        lastUserChange = Time.time;
        bridge.Send("A:" + Mathf.RoundToInt(value));
    }

    // Messages from the Arduino, format S:<angle>,<source>
    void ReadMessages()
    {
        while (bridge.TryRead(out string line))
        {
            if (!line.StartsWith("S:")) continue;

            string[] parts = line.Substring(2).Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int angle)) continue;

            lastReport = Time.time;
            KnobReleased = parts[1] == "DIG";
            OnStateReceived(angle);
        }
    }

    void OnStateReceived(int angle)
    {
        repeated = (angle == ServoAngle) ? repeated + 1 : 0;
        ServoAngle = angle;                                   // updated with EVERY S:

        // The slider follows the car only if the servo is still
        // and the user has not touched it recently (for example after moving the knob)
        bool servoStill = repeated >= 3;                      // about 150 ms without changes
        bool userStill = Time.time - lastUserChange > 0.5f;

        if (servoStill && userStill && Mathf.RoundToInt(slider.value) != angle)
            slider.SetValueWithoutNotify(angle);              // does NOT send a command (no echo)
    }

    // Visualization
    void AnimateModel()
    {
        visualAngle = Mathf.Lerp(visualAngle, ServoAngle,
                                 1 - Mathf.Exp(-visualSmoothing * Time.deltaTime));

        float wheelTurn = WheelAngle;
        if (leftPivot) leftPivot.localRotation = Quaternion.Euler(0, wheelTurn, 0);
        if (rightPivot) rightPivot.localRotation = Quaternion.Euler(0, wheelTurn, 0);

        if (servoArm)
            servoArm.localRotation = Quaternion.Euler(0, visualAngle - center, 0);
    }

    void UpdateLabel()
    {
        if (!label) return;

        string text = bridge.Connected
            ? $"Servo: {ServoAngle}°  |  Control: {slider.value:0}°"
            : "No connection with the Arduino";

        // Diagnostics: port open but no valid S: report yet
        if (bridge.Connected && Time.time - lastReport > 2f)
        {
            text += bridge.LinesReceived == 0
                ? "\nNo data received: check the firmware, the baud rate (9600) and the port"
                : "\nData received but not in the S:<angle>,<source> format";
        }

        if (bridge.Connected && KnobReleased)
            text += $"\nKnob released: move it to {slider.value:0}° to take control back";

        label.text = text;
    }
}
