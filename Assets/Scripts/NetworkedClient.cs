using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkedClient : MonoBehaviour
{
    int connectionID;
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    byte error;
    bool isConnected = false;
    int ourClientID;

    GameObject gameSystemManager;
    [SerializeField] GameObject gameboard;

    // Start is called before the first frame update
    void Start()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<SystemManager>() != null)
                gameSystemManager = go;
        }
        Connect();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateNetworkConnection();
    }

    private void UpdateNetworkConnection()
    {
        if (isConnected)
        {
            int recHostID;
            int recConnectionID;
            int recChannelID;
            byte[] recBuffer = new byte[1024];
            int bufferSize = 1024;
            int dataSize;
            NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

            switch (recNetworkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    Debug.Log("connected.  " + recConnectionID);
                    ourClientID = recConnectionID;
                    break;
                case NetworkEventType.DataEvent:
                    string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                    ProcessRecievedMsg(msg, recConnectionID);
                    //Debug.Log("got msg = " + msg);
                    break;
                case NetworkEventType.DisconnectEvent:
                    isConnected = false;
                    Debug.Log("disconnected.  " + recConnectionID);
                    break;
            }
        }
    }

    private void Connect()
    {

        if (!isConnected)
        {
            Debug.Log("Attempting to create connection");

            NetworkTransport.Init();

            ConnectionConfig config = new ConnectionConfig();
            reliableChannelID = config.AddChannel(QosType.Reliable);
            unreliableChannelID = config.AddChannel(QosType.Unreliable);
            HostTopology topology = new HostTopology(config, maxConnections);
            hostID = NetworkTransport.AddHost(topology, 0);
            Debug.Log("Socket open.  Host ID = " + hostID);

            connectionID = NetworkTransport.Connect(hostID, "192.168.86.138", socketPort, 0, out error); // server is local on network

            if (error == 0)
            {
                isConnected = true;

                Debug.Log("Connected, id = " + connectionID);
            }
        }
    }

    public void Disconnect()
    {
        NetworkTransport.Disconnect(hostID, connectionID, out error);
    }

    public void SendMessageToHost(string msg)
    {
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, connectionID, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        
        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ServerToClientSignifiers.AccountCreationComplete)
        {
            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.MainMenu);
        }
        else if (signifier == ServerToClientSignifiers.LoginComplete)
        {
            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.MainMenu);
        }
        else if (signifier == ServerToClientSignifiers.GameStart)
        {
            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.Game);

            Debug.Log("Game Started");

            SendMessageToHost(ClientToServerSignifiers.PlayGame + "");
        }

        else if (signifier == ServerToClientSignifiers.FirstPlayerSet)
        {
            Debug.Log("First Player");
            gameboard.GetComponent<Gameboard>().SetTile(1);
        }

        else if (signifier == ServerToClientSignifiers.SecondPlayerSet)
        {
            Debug.Log("Second Player");
            gameboard.GetComponent<Gameboard>().SetTile(2);
        }

        else if (signifier == ServerToClientSignifiers.PlayersTurn)
        {
            gameboard.GetComponent<Gameboard>().IsPlayersTurn = true;
        }

        else if (signifier == ServerToClientSignifiers.OpponentNode)
        {
            string node = csv[1];

            gameboard.GetComponent<Gameboard>().PlaceOpponentNode(int.Parse(node));
        }

        else if (signifier == ServerToClientSignifiers.WinConditionForPlayer)
        {
            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.GameWin);
        }

        else if (signifier == ServerToClientSignifiers.LoseConditionForPlayer)
        {
            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.GameLose);
        }

        else if (signifier == ServerToClientSignifiers.DisplayPlayerMessage)
        {
            string playerMsg = csv[1];

            StartCoroutine(gameSystemManager.GetComponent<SystemManager>().DisplayPlayerMessage(playerMsg));

            Debug.Log("Player Message Recieved" + playerMsg);
        }

        else if (signifier == ServerToClientSignifiers.DisplayOpponentMessage)
        {
            string opponentMsg = csv[1];

            StartCoroutine(gameSystemManager.GetComponent<SystemManager>().DisplayOpponentMessage(opponentMsg));

            Debug.Log("Opponent Msg Recieved" + opponentMsg);
        }

        else if (signifier == ServerToClientSignifiers.OpponentPlayed)
        {
            Debug.Log("Opponent Played");
        }

        else if (signifier == ServerToClientSignifiers.JoinAsObserver)
        {
            Debug.Log("JoiningAsObserver");

            gameSystemManager.GetComponent<SystemManager>().ChangeState(GameStates.Observer);
        }

        else if (signifier == ServerToClientSignifiers.UpdateObservers)
        {
            string nodeId = csv[1];
            string nodeSig = csv[2];

            gameboard.GetComponent<Gameboard>().PlaceNodeAsObserver(int.Parse(nodeId), int.Parse(nodeSig));
        }

        else if (signifier == ServerToClientSignifiers.ReplayMove)
        {

            string playerID = csv[1];
            string nodeID = csv[2];
            string nodeSig = csv[3];

            gameSystemManager.GetComponent<SystemManager>().DisplayReplayBlock(playerID, nodeID, nodeSig);
        }
    }

    public bool IsConnected()
    {
        return isConnected;
    }
}


public static class ClientToServerSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;

    public const int JoinQueueForGameRoom = 3;

    public const int PlayGame = 4;

    public const int TurnTaken = 5;

    public const int PlayerWin = 6;

    public const int PlayerMessage = 7;

    public const int RequestReplayMove = 8;
}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;

    public const int GameStart = 5;

    //Addition
    public const int FirstPlayerSet = 6;

    public const int SecondPlayerSet = 7;

    public const int PlayersTurn = 8;

    public const int OpponentNode = 9;

    public const int WinConditionForPlayer = 10;

    public const int LoseConditionForPlayer = 11;

    public const int DisplayPlayerMessage = 12;

    public const int DisplayOpponentMessage = 13;

    public const int OpponentPlayed = 14;

    public const int JoinAsObserver = 15;

    public const int UpdateObservers = 16;

    public const int ReplayMove = 17;
}

