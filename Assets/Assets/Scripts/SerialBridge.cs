using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

// Opens the serial port and reads in a separate thread so the main thread never blocks.
// Received lines are queued and consumed from Update().
public class SerialBridge : MonoBehaviour
{
    [Header("Port")]
    [FormerlySerializedAs("puerto")]
    public string port = "COM6";            // same port shown in the Arduino IDE
    [FormerlySerializedAs("baudios")]
    public int baudRate = 9600;             // same as Serial.begin() in the firmware

    [Header("Debug")]
    [FormerlySerializedAs("registrarEnvios")]
    public bool logSent = true;             // print every command sent to the console
    public bool logReceived = true;         // print the first lines received, to check the link
    int logged;

    SerialPort serial;
    Thread readThread;
    volatile bool running;
    readonly ConcurrentQueue<string> received = new ConcurrentQueue<string>();

    public bool Connected => serial != null && serial.IsOpen;

    // Number of lines received since the port was opened (for diagnostics)
    volatile int linesReceived;
    public int LinesReceived => linesReceived;

    void Start()
    {
        try
        {
            serial = new SerialPort(port, baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 100,
                WriteTimeout = 50
            };
            serial.Open();

            running = true;
            readThread = new Thread(ReadInBackground) { IsBackground = true };
            readThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not open {port}: {e.Message}");
        }
    }

    // Runs in its own thread: it can wait for data without freezing Unity
    void ReadInBackground()
    {
        while (running)
        {
            try
            {
                string line = serial.ReadLine().Trim();
                if (line.Length > 0)
                {
                    received.Enqueue(line);
                    linesReceived++;
                }
            }
            catch (TimeoutException) { }
            catch (Exception) { Thread.Sleep(50); }
        }
    }

    // Takes the next received line, if there is one
    public bool TryRead(out string line)
    {
        bool ok = received.TryDequeue(out line);
        if (ok && logReceived && logged < 15)
        {
            logged++;
            Debug.Log("Received [" + line + "]");
        }
        return ok;
    }

    // Sends a message to the Arduino (adds the '\n')
    public void Send(string message)
    {
        if (!Connected) return;
        try
        {
            serial.Write(message + "\n");
            if (logSent) Debug.Log("Sent " + message);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Send error: " + e.Message);
        }
    }

    void OnDestroy()
    {
        running = false;
        readThread?.Join(300);
        if (Connected) serial.Close();
    }
}
