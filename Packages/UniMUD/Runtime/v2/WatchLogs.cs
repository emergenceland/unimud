using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Cysharp.Threading.Tasks;
using HybridWebSocket;
using Newtonsoft.Json;
using UniRx;
// using UniRx;
using UnityEngine;

namespace v2
{
    public class BlockParams
    {
        public BlockResult result;
    }

    public class BlockResult
    {
        public string number;
    }

    public class Block
    {
        public BlockParams @params;
    }

    public class BlockStream : IDisposable
    {
        private WebSocket ws;
        Subject<Block> subject = new();

        private UniTaskCompletionSource<bool> _connected = new();
        private readonly int maxRetryAttempts = 3; 
        TimeSpan retryDelay = TimeSpan.FromSeconds(3);
        
        public IObservable<Block> WatchBlocks(string wsRpc)
        {
            ws = WebSocketFactory.CreateInstance(wsRpc);

            ws.OnOpen += () =>
            {
                Debug.Log("WS connected!");
                Debug.Log("WS state: " + ws.GetState().ToString());

                string typeStr = "newHeads";
                var subscriptionRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "eth_subscribe",
                    @params = new List<string> { typeStr }
                };

                string jsonString = JsonConvert.SerializeObject(subscriptionRequest);
                byte[] byteArray = Encoding.UTF8.GetBytes(jsonString);
                ws.Send(byteArray);
                _connected.TrySetResult(true); // Indicates that the connection has been established
            };

            ws.OnMessage += async (byte[] msg) =>
            {
                var message = Encoding.UTF8.GetString(msg);
                var block = JsonConvert.DeserializeObject<Block>(message);
                subject.OnNext(block);
            };

            ws.OnError += (string errMsg) =>
            {
                Debug.Log("WS error: " + errMsg);
                subject.OnError(new Exception(errMsg));
            };

            ws.OnClose += (WebSocketCloseCode code) =>
            {
                Debug.Log("WS closed with code: " + code.ToString());
                subject.OnCompleted();
            };

            ws.Connect();
            return subject;
        }
        
        public void Dispose()
        {
            if (ws != null && ws.GetState() == WebSocketState.Open)
            {
                ws.Close();
            }

            subject?.Dispose();
        }
    }
}
