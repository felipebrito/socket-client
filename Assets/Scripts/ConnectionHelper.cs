using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SocketIOClient;
using Newtonsoft.Json;

public class ConnectionHelper : MonoBehaviour
{
    private SocketIOUnity socket;
    private string serverUri = "ws://localhost:8181";
    private bool isConnected = false;
    private Queue<string> messageQueue = new Queue<string>();
    private float reconnectInterval = 5f;
    private float messageProcessingInterval = 0.1f;

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public bool IsConnected => isConnected;
    
    public void Initialize(string uri)
    {
        serverUri = uri;
        ConnectToServer();
        StartCoroutine(ProcessMessageQueue());
    }

    private void ConnectToServer()
    {
        try
        {
            Debug.Log($"Connecting to WebSocket server: {serverUri}");
            
            // Configuração do socket
            var uri = new Uri(serverUri);
            socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                AutoConnect = true,
                Query = new Dictionary<string, string>
                {
                    {"token", "UNITY"}
                }
            });

            // Eventos do socket
            socket.OnConnected += (sender, args) =>
            {
                Debug.Log("Connected to server");
                isConnected = true;
                OnConnected?.Invoke();
            };

            socket.OnDisconnected += (sender, args) =>
            {
                Debug.Log("Disconnected from server");
                isConnected = false;
                OnDisconnected?.Invoke();
                StartCoroutine(TryReconnect());
            };

            socket.OnError += (sender, args) =>
            {
                Debug.LogError($"Socket error: {args}");
                OnError?.Invoke(args);
            };

            // Configurando eventos para receber mensagens do servidor
            socket.On("message", (response) =>
            {
                string data = response.ToString();
                Debug.Log($"Received message: {data}");
                EnqueueMessage(data);
            });

            socket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error connecting to server: {ex.Message}");
            OnError?.Invoke(ex.Message);
            StartCoroutine(TryReconnect());
        }
    }

    private IEnumerator TryReconnect()
    {
        yield return new WaitForSeconds(reconnectInterval);
        
        if (!isConnected)
        {
            Debug.Log("Attempting to reconnect...");
            ConnectToServer();
        }
    }

    private void EnqueueMessage(string message)
    {
        lock (messageQueue)
        {
            messageQueue.Enqueue(message);
        }
    }

    private IEnumerator ProcessMessageQueue()
    {
        while (true)
        {
            if (messageQueue.Count > 0)
            {
                string message;
                lock (messageQueue)
                {
                    message = messageQueue.Dequeue();
                }
                
                OnMessageReceived?.Invoke(message);
            }
            
            yield return new WaitForSeconds(messageProcessingInterval);
        }
    }

    public void SendMessage(string eventName, string message)
    {
        if (!isConnected)
        {
            Debug.LogWarning("Cannot send message: not connected to server");
            return;
        }

        try
        {
            socket.Emit(eventName, message);
            Debug.Log($"Sent message to server: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending message: {ex.Message}");
            OnError?.Invoke(ex.Message);
        }
    }

    public void SendCommand(string command)
    {
        SendMessage("command", command);
    }

    public void Disconnect()
    {
        if (socket != null && isConnected)
        {
            socket.Disconnect();
            isConnected = false;
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
} 